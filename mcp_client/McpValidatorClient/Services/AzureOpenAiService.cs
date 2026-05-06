using Azure.AI.OpenAI;
using McpValidatorClient.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;

namespace McpValidatorClient.Services;

public class AzureOpenAiService(IConfiguration config, ILogger<AzureOpenAiService> logger)
{
    private readonly string _deployment = config["AzureOpenAI:DeploymentName"] ?? "gpt-4o";

    private AzureOpenAIClient CreateClient()
    {
        var endpoint = config["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
        var apiKey = config["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");
        return new AzureOpenAIClient(new Uri(endpoint), new Azure.AzureKeyCredential(apiKey));
    }

    // ── Step 1: Classify the complaint ─────────────────────────────────────
    public async Task<ClassificationResult> ClassifyComplaintAsync(string complaintText)
    {
        const string systemPrompt = """
            You are a complaint classification specialist. Analyze the user's complaint and classify it
            into exactly ONE of the following issue types. Respond with ONLY a valid JSON object — no markdown, no explanation.

            Issue types:
            - BILLING_DISPUTE: Charges, payments, invoices, billing errors, overcharges
            - ACCOUNT_ACCESS: Login failures, password reset, locked accounts, authentication issues
            - PRODUCT_DEFECT: Damaged goods, malfunctioning product, quality complaints
            - SHIPPING_DELAY: Late delivery, lost package, wrong address, tracking issues
            - SERVICE_OUTAGE: System down, unavailable service, performance degradation
            - REFUND_REQUEST: Return requests, refund demands, exchange requests
            - SAP_PURCHASE_ORDER: SAP ERP purchase orders, OData fields, procurement complaints
            - USER_REGISTRATION: New account setup, registration failures, profile issues
            - GENERAL_INQUIRY: General questions that do not fit other categories

            Response JSON schema (strict):
            {
              "issueCode": "BILLING_DISPUTE",
              "rationale": "one sentence explaining why",
              "confidence": 0.95
            }
            """;

        var client = CreateClient();
        var chat = client.GetChatClient(_deployment);

        var response = await chat.CompleteChatAsync(
        [
            ChatMessage.CreateSystemMessage(systemPrompt),
            ChatMessage.CreateUserMessage(complaintText)
        ],
        new ChatCompletionOptions { ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() });

        var raw = response.Value.Content[0].Text;
        logger.LogDebug("Classification raw: {Raw}", raw);

        var obj = JObject.Parse(raw);
        var code = obj["issueCode"]?.ToString() ?? "GENERAL_INQUIRY";

        return new ClassificationResult
        {
            IssueCode = code,
            IssueType = ParseIssueType(code),
            Rationale = obj["rationale"]?.ToString() ?? string.Empty,
            Confidence = obj["confidence"]?.Value<double>() ?? 0.5
        };
    }

    // ── Step 2: Extract entities as strict JSON ─────────────────────────────
    public async Task<EntityExtractionResult> ExtractEntitiesAsync(string complaintText, string issueCode)
    {
        var entitySchema = GetEntitySchema(issueCode);
        var systemPrompt = $"""
            You are a complaint data extraction specialist. Extract structured data from the user's complaint.
            The complaint is of type: {issueCode}.

            Extract entities and return ONLY a valid JSON object matching this schema:
            {entitySchema}

            Rules:
            - Return ONLY the JSON object — no markdown, no code blocks, no explanation
            - Use null for fields not mentioned in the complaint
            - Dates must be in ISO 8601 format (YYYY-MM-DD or YYYY-MM-DDTHH:mm:ssZ)
            - Monetary amounts must be numbers (not strings)
            - All string values must be properly escaped
            - If a field value cannot be determined, use null
            """;

        var client = CreateClient();
        var chat = client.GetChatClient(_deployment);

        var response = await chat.CompleteChatAsync(
        [
            ChatMessage.CreateSystemMessage(systemPrompt),
            ChatMessage.CreateUserMessage(complaintText)
        ],
        new ChatCompletionOptions { ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() });

        var raw = response.Value.Content[0].Text;
        logger.LogDebug("Extraction raw for {Code}: {Raw}", issueCode, raw);

        // Validate it's parseable JSON
        var obj = JObject.Parse(raw);
        var nonNullFields = obj.Properties().Count(p => p.Value.Type != JTokenType.Null);

        return new EntityExtractionResult
        {
            EntityJson = obj.ToString(Formatting.Indented),
            Entities = obj.ToObject<Dictionary<string, object?>>() ?? [],
            FieldCount = nonNullFields
        };
    }

    // ── Step 3: Select the best matching MCP template ──────────────────────
    public async Task<string> SelectTemplateAsync(
        string issueCode, string entityJson, List<McpTemplateSummary> availableTemplates)
    {
        if (!availableTemplates.Any()) return string.Empty;

        // Fast deterministic match first
        var deterministicMatch = TryDeterministicMatch(issueCode, availableTemplates);
        if (!string.IsNullOrEmpty(deterministicMatch))
        {
            logger.LogInformation("Deterministic template match: {Match}", deterministicMatch);
            return deterministicMatch;
        }

        var templateList = string.Join("\n", availableTemplates.Select(t =>
            $"- \"{t.Name}\": {t.Description} [tags: {t.Tags}] [{t.FieldRuleCount} rules]"));

        var systemPrompt = $$"""
            You are a template matching specialist. Given a complaint of type {{issueCode}} with extracted
            entities, select the most appropriate validation template from the list below.

            Respond with ONLY a JSON object — no markdown, no explanation:
            {"templateName": "exact name from list", "rationale": "why this template fits"}

            Available templates:
            {{templateList}}

            Extracted entities:
            {{entityJson}}
            """;

        var client = CreateClient();
        var chat = client.GetChatClient(_deployment);

        var response = await chat.CompleteChatAsync(
        [
            ChatMessage.CreateSystemMessage(systemPrompt),
            ChatMessage.CreateUserMessage($"Issue type: {issueCode}\nEntities: {entityJson}")
        ],
        new ChatCompletionOptions { ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() });

        var raw = response.Value.Content[0].Text;
        var obj = JObject.Parse(raw);
        var name = obj["templateName"]?.ToString() ?? availableTemplates[0].Name;

        // Validate the returned name exists
        return availableTemplates.Any(t => t.Name == name) ? name : availableTemplates[0].Name;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static string? TryDeterministicMatch(string issueCode, List<McpTemplateSummary> templates)
    {
        // Map well-known codes → template name fragments
        var hints = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["SAP_PURCHASE_ORDER"] = ["SAP Purchase Order", "SAP", "Purchase Order", "PO"],
            ["USER_REGISTRATION"]  = ["User Registration", "Registration", "User"],
            ["BILLING_DISPUTE"]    = ["Billing", "Invoice", "Payment"],
            ["ACCOUNT_ACCESS"]     = ["Account", "Login", "Access"],
            ["PRODUCT_DEFECT"]     = ["Product", "Defect", "Quality"],
            ["SHIPPING_DELAY"]     = ["Shipping", "Delivery", "Logistics"],
            ["REFUND_REQUEST"]     = ["Refund", "Return", "Exchange"],
            ["SERVICE_OUTAGE"]     = ["Service", "Outage", "Incident"],
        };

        if (!hints.TryGetValue(issueCode, out var keywords)) return null;

        foreach (var keyword in keywords)
            foreach (var t in templates)
                if (t.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    t.Tags.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return t.Name;

        return null;
    }

    private static string GetEntitySchema(string issueCode) => issueCode switch
    {
        "BILLING_DISPUTE" => """
            {
              "customer_email": "string | null",
              "customer_id": "string | null",
              "invoice_number": "string | null",
              "amount": "number | null",
              "currency": "3-letter ISO code | null",
              "billing_period": "string | null",
              "dispute_reason": "string | null",
              "account_holder_name": "string | null"
            }
            """,

        "ACCOUNT_ACCESS" => """
            {
              "email": "string | null",
              "username": "string | null",
              "account_id": "string | null",
              "issue_type": "locked|reset|2fa|other | null",
              "last_successful_login": "ISO date | null",
              "phone": "string | null"
            }
            """,

        "PRODUCT_DEFECT" => """
            {
              "order_id": "string | null",
              "product_name": "string | null",
              "product_sku": "string | null",
              "defect_description": "string | null",
              "purchase_date": "ISO date | null",
              "customer_email": "string | null",
              "phone": "string | null",
              "severity": "low|medium|high|critical | null"
            }
            """,

        "SHIPPING_DELAY" => """
            {
              "order_id": "string | null",
              "tracking_number": "string | null",
              "expected_delivery": "ISO date | null",
              "actual_status": "string | null",
              "customer_email": "string | null",
              "shipping_address": "string | null",
              "carrier": "string | null"
            }
            """,

        "REFUND_REQUEST" => """
            {
              "order_id": "string | null",
              "customer_email": "string | null",
              "amount": "number | null",
              "currency": "3-letter ISO code | null",
              "reason": "string | null",
              "purchase_date": "ISO date | null",
              "preferred_resolution": "refund|exchange|store_credit | null"
            }
            """,

        "SERVICE_OUTAGE" => """
            {
              "service_name": "string | null",
              "affected_region": "string | null",
              "reported_at": "ISO datetime | null",
              "severity": "low|medium|high|critical | null",
              "customer_email": "string | null",
              "description": "string | null"
            }
            """,

        "SAP_PURCHASE_ORDER" => """
            {
              "PurchaseOrder": "up-to-10-digit number string | null",
              "CompanyCode": "4-char alphanumeric | null",
              "Vendor": "vendor number string | null",
              "Currency": "3-letter ISO code | null",
              "PostingDate": "SAP OData date /Date(epoch)/ or ISO date | null",
              "Plant": "4-char alphanumeric | null",
              "MaterialNumber": "up-to-18-char | null",
              "CostCenter": "up-to-10-char | null"
            }
            """,

        "USER_REGISTRATION" => """
            {
              "email": "valid email | null",
              "username": "string | null",
              "phone": "phone number | null",
              "age": "number | null",
              "website": "URL | null",
              "country": "2-letter country code | null",
              "preferred_language": "language code | null"
            }
            """,

        _ => """
            {
              "description": "string describing the issue",
              "customer_email": "string | null",
              "priority": "low|medium|high | null",
              "reference_id": "string | null"
            }
            """
    };

    private static IssueType ParseIssueType(string code) => code switch
    {
        "BILLING_DISPUTE"    => IssueType.BillingDispute,
        "ACCOUNT_ACCESS"     => IssueType.AccountAccess,
        "PRODUCT_DEFECT"     => IssueType.ProductDefect,
        "SHIPPING_DELAY"     => IssueType.ShippingDelay,
        "SERVICE_OUTAGE"     => IssueType.ServiceOutage,
        "REFUND_REQUEST"     => IssueType.RefundRequest,
        "SAP_PURCHASE_ORDER" => IssueType.SapPurchaseOrder,
        "USER_REGISTRATION"  => IssueType.UserRegistration,
        _                    => IssueType.GeneralInquiry
    };
}
