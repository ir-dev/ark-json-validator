using McpValidatorClient.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace McpValidatorClient.Services;

public class McpClientService(HttpClient http, IConfiguration config, ILogger<McpClientService> logger)
{
    private int _idCounter = 0;

    private void ConfigureAuth()
    {
        var user = config["McpServer:Username"] ?? "admin";
        var pass = config["McpServer:Password"] ?? "ark2024!";
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}")));
    }

    private async Task<T?> CallAsync<T>(string method, object? parameters = null)
    {
        ConfigureAuth();
        var id = Interlocked.Increment(ref _idCounter);
        var req = new McpRpcRequest { Method = method, Params = parameters, Id = id };
        var body = JsonConvert.SerializeObject(req);

        logger.LogDebug("MCP → {Method} [{Id}]", method, id);

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await http.PostAsync("/mcp", content);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync();
        logger.LogDebug("MCP ← {Method} [{Id}]: {Raw}", method, id, raw.Length > 500 ? raw[..500] + "…" : raw);

        var wrapper = JObject.Parse(raw);

        if (wrapper["error"] is { Type: not JTokenType.Null } err)
            throw new InvalidOperationException($"MCP error {err["code"]}: {err["message"]}");

        var result = wrapper["result"];
        if (result is null) return default;
        return result.ToObject<T>();
    }

    // ── Initialize (handshake) ──────────────────────────────────────────────
    public async Task<bool> InitializeAsync()
    {
        try
        {
            await CallAsync<object>("initialize", new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "mcp-validator-client", version = "1.0.0" }
            });
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP initialize failed (server may not require it)");
            return false;
        }
    }

    // ── List templates ─────────────────────────────────────────────────────
    public async Task<List<McpTemplateSummary>> ListTemplatesAsync()
    {
        var result = await CallAsync<JObject>("tools/call", new
        {
            name = "list_templates",
            arguments = new { }
        });

        var text = ExtractText(result);
        if (string.IsNullOrEmpty(text)) return [];

        // The tool returns a JSON array of template summaries
        return JsonConvert.DeserializeObject<List<McpTemplateSummary>>(text) ?? [];
    }

    // ── Validate JSON against template ─────────────────────────────────────
    public async Task<ValidationStepResult> ValidateJsonAsync(string templateName, string jsonPayload)
    {
        var result = await CallAsync<JObject>("tools/call", new
        {
            name = "validate_json",
            arguments = new
            {
                template_name = templateName,
                json_payload = jsonPayload,
                stop_on_first_error = false
            }
        });

        var text = ExtractText(result);
        if (string.IsNullOrEmpty(text))
            return new ValidationStepResult { IsValid = false, Errors = [new() { ErrorMessage = "Empty response from MCP server" }] };

        var obj = JObject.Parse(text);
        return new ValidationStepResult
        {
            IsValid = obj["isValid"]?.Value<bool>() ?? false,
            TemplateName = obj["templateName"]?.ToString() ?? templateName,
            TotalRulesChecked = obj["totalRulesChecked"]?.Value<int>() ?? 0,
            Warnings = obj["warnings"]?.ToObject<List<string>>() ?? [],
            Errors = obj["errors"]?.Select(e => new ValidationError
            {
                FieldPath = e["fieldPath"]?.ToString() ?? string.Empty,
                ErrorMessage = e["errorMessage"]?.ToString() ?? string.Empty,
                ValidatorType = e["validatorType"]?.ToString()
            }).ToList() ?? []
        };
    }

    // ── Check individual field ─────────────────────────────────────────────
    public async Task<(bool IsValid, string? Error)> CheckFieldAsync(string value, string validatorType)
    {
        var result = await CallAsync<JObject>("tools/call", new
        {
            name = "check_field",
            arguments = new { value, validator_type = validatorType }
        });

        var text = ExtractText(result);
        if (string.IsNullOrEmpty(text)) return (false, "No response");
        var obj = JObject.Parse(text);
        return (obj["is_valid"]?.Value<bool>() ?? false, obj["error"]?.ToString());
    }

    // ── Get template details ───────────────────────────────────────────────
    public async Task<JObject?> GetTemplateAsync(string name)
    {
        var result = await CallAsync<JObject>("tools/call", new
        {
            name = "get_template",
            arguments = new { name }
        });

        var text = ExtractText(result);
        return string.IsNullOrEmpty(text) ? null : JObject.Parse(text);
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private static string ExtractText(JObject? result)
    {
        if (result is null) return string.Empty;

        // tools/call result has { content: [{ type: "text", text: "..." }], isError: bool }
        var content = result["content"];
        if (content is JArray arr && arr.Count > 0)
            return arr[0]["text"]?.ToString() ?? string.Empty;

        // Fallback: direct text
        return result["text"]?.ToString() ?? result.ToString();
    }
}
