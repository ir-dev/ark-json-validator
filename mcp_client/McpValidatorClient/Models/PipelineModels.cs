using System.Text.Json.Serialization;

namespace McpValidatorClient.Models;

// ── Enums ──────────────────────────────────────────────────────────────
public enum IssueType
{
    Unknown,
    BillingDispute,
    AccountAccess,
    ProductDefect,
    ShippingDelay,
    ServiceOutage,
    RefundRequest,
    SapPurchaseOrder,
    UserRegistration,
    GeneralInquiry
}

public enum StepStatus { Pending, Running, Success, Failed, Skipped }

// ── Pipeline ────────────────────────────────────────────────────────────
public class ComplaintRequest
{
    public string ComplaintText { get; set; } = string.Empty;
    public string? OverrideTemplate { get; set; }   // optional: force a specific template
}

public class PipelineResult
{
    public string ComplaintText { get; set; } = string.Empty;
    public PipelineStep Classification { get; set; } = new();
    public PipelineStep EntityExtraction { get; set; } = new();
    public PipelineStep TemplateSelection { get; set; } = new();
    public PipelineStep Validation { get; set; } = new();
    public PipelineStep ProcessExecution { get; set; } = new();
    public bool OverallSuccess =>
        Classification.Status == StepStatus.Success &&
        EntityExtraction.Status == StepStatus.Success &&
        TemplateSelection.Status == StepStatus.Success &&
        Validation.Status == StepStatus.Success &&
        ProcessExecution.Status is StepStatus.Success or StepStatus.Skipped;
}

public class PipelineStep
{
    public string Name { get; set; } = string.Empty;
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public string? Output { get; set; }
    public string? Error { get; set; }
    public string? RawJson { get; set; }      // pretty-printed for display
    public long ElapsedMs { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

// ── Step outputs ────────────────────────────────────────────────────────
public class ClassificationResult
{
    public IssueType IssueType { get; set; }
    public string IssueCode { get; set; } = string.Empty;   // "SAP_PURCHASE_ORDER"
    public string Rationale { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public class EntityExtractionResult
{
    public string EntityJson { get; set; } = string.Empty;  // validated strict JSON
    public Dictionary<string, object?> Entities { get; set; } = [];
    public int FieldCount { get; set; }
}

public class TemplateSelectionResult
{
    public string TemplateName { get; set; } = string.Empty;
    public string SelectionRationale { get; set; } = string.Empty;
    public List<string> AvailableTemplates { get; set; } = [];
}

public class ValidationStepResult
{
    public bool IsValid { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public List<ValidationError> Errors { get; set; } = [];
    public int TotalRulesChecked { get; set; }
    public List<string> Warnings { get; set; } = [];
}

public class ValidationError
{
    public string FieldPath { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ValidatorType { get; set; }
}

public class ProcessExecutionResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Command { get; set; } = string.Empty;
    public string Stdout { get; set; } = string.Empty;
    public string Stderr { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public long ElapsedMs { get; set; }
}

// ── MCP JSON-RPC ─────────────────────────────────────────────────────────
public class McpRpcRequest
{
    [JsonPropertyName("jsonrpc")] public string Jsonrpc { get; set; } = "2.0";
    [JsonPropertyName("method")] public string Method { get; set; } = string.Empty;
    [JsonPropertyName("params")] public object? Params { get; set; }
    [JsonPropertyName("id")] public int Id { get; set; } = 1;
}

public class McpRpcResponse<T>
{
    [JsonPropertyName("jsonrpc")] public string Jsonrpc { get; set; } = "2.0";
    [JsonPropertyName("result")] public T? Result { get; set; }
    [JsonPropertyName("error")] public McpRpcError? Error { get; set; }
    [JsonPropertyName("id")] public int Id { get; set; }
}

public class McpRpcError
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
}

public class McpToolResult
{
    [JsonPropertyName("content")] public List<McpContent> Content { get; set; } = [];
    [JsonPropertyName("isError")] public bool IsError { get; set; }
}

public class McpContent
{
    [JsonPropertyName("type")] public string Type { get; set; } = "text";
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
}

public class McpTemplateSummary
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("tags")] public string Tags { get; set; } = string.Empty;
    [JsonPropertyName("fieldRuleCount")] public int FieldRuleCount { get; set; }
}
