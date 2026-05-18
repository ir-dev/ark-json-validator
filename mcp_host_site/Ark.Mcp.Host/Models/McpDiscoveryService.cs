using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using McpServiceHub.Models.ViewModels;

namespace McpServiceHub.Services;

public class McpDiscoveryService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<McpDiscoveryService> _logger;

    public McpDiscoveryService(IHttpClientFactory httpFactory, ILogger<McpDiscoveryService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task DiscoverAsync(McpDiscoveryViewModel vm)
    {
        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        ConfigureAuth(client, vm);

        // initialize is optional — some servers require it, others don't.
        try
        {
            await PostRpcAsync(client, vm.EndpointUrl, "initialize", new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "ark-mcp-hub-discovery", version = "1.0.0" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP initialize failed for {Url}; continuing", vm.EndpointUrl);
        }

        var partialErrors = new List<string>();

        try
        {
            var result = await PostRpcAsync(client, vm.EndpointUrl, "tools/list", new { });
            if (result.HasValue && result.Value.TryGetProperty("tools", out var toolsArr) && toolsArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in toolsArr.EnumerateArray())
                {
                    vm.Tools.Add(new McpToolInfo
                    {
                        Name = GetString(t, "name"),
                        Description = GetString(t, "description"),
                        InputSchema = t.TryGetProperty("inputSchema", out var schema)
                            ? JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true })
                            : string.Empty
                    });
                }
            }
        }
        catch (Exception ex)
        {
            partialErrors.Add($"tools/list: {ex.Message}");
        }

        try
        {
            var result = await PostRpcAsync(client, vm.EndpointUrl, "resources/list", new { });
            if (result.HasValue && result.Value.TryGetProperty("resources", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in arr.EnumerateArray())
                {
                    vm.Resources.Add(new McpResourceInfo
                    {
                        Uri = GetString(r, "uri"),
                        Name = GetString(r, "name"),
                        Description = GetString(r, "description"),
                        MimeType = r.TryGetProperty("mimeType", out var m) ? m.GetString() : null
                    });
                }
            }
        }
        catch (Exception ex)
        {
            partialErrors.Add($"resources/list: {ex.Message}");
        }

        try
        {
            var result = await PostRpcAsync(client, vm.EndpointUrl, "prompts/list", new { });
            if (result.HasValue && result.Value.TryGetProperty("prompts", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in arr.EnumerateArray())
                {
                    vm.Prompts.Add(new McpPromptInfo
                    {
                        Name = GetString(p, "name"),
                        Description = GetString(p, "description")
                    });
                }
            }
        }
        catch (Exception ex)
        {
            partialErrors.Add($"prompts/list: {ex.Message}");
        }

        vm.HasDiscovered = true;
        if (partialErrors.Count > 0 && vm.Tools.Count == 0 && vm.Resources.Count == 0 && vm.Prompts.Count == 0)
        {
            vm.DiscoveryError = "Discovery failed. " + string.Join(" | ", partialErrors);
        }
        else if (partialErrors.Count > 0)
        {
            vm.DiscoveryError = "Some endpoints returned errors: " + string.Join(" | ", partialErrors);
        }
    }

    private static void ConfigureAuth(HttpClient client, McpDiscoveryViewModel vm)
    {
        switch (vm.AuthenticationType)
        {
            case "ApiKey":
                if (!string.IsNullOrWhiteSpace(vm.ApiKeyHeader) && !string.IsNullOrEmpty(vm.ApiKeyValue))
                    client.DefaultRequestHeaders.TryAddWithoutValidation(vm.ApiKeyHeader, vm.ApiKeyValue);
                break;
            case "Bearer":
                if (!string.IsNullOrEmpty(vm.BearerToken))
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", vm.BearerToken);
                break;
            case "OAuth":
                if (!string.IsNullOrEmpty(vm.OAuthAccessToken))
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", vm.OAuthAccessToken);
                break;
            case "Basic":
                if (!string.IsNullOrEmpty(vm.BasicUsername))
                {
                    var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{vm.BasicUsername}:{vm.BasicPassword}"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
                }
                break;
        }
    }

    private static async Task<JsonElement?> PostRpcAsync(HttpClient client, string endpoint, string method, object parameters)
    {
        var payload = new
        {
            jsonrpc = "2.0",
            method,
            @params = parameters,
            id = Random.Shared.Next(1, int.MaxValue)
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        // Some MCP servers accept SSE; advertise both.
        if (!client.DefaultRequestHeaders.Accept.Any())
        {
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        }

        var response = await client.PostAsync(endpoint, content);
        var raw = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(raw, 300)}");

        // Strip SSE framing if present (lines beginning with "data: ").
        var json = ExtractJsonPayload(raw);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
        {
            var code = err.TryGetProperty("code", out var c) ? c.ToString() : "?";
            var msg = err.TryGetProperty("message", out var m) ? m.GetString() : err.ToString();
            throw new InvalidOperationException($"RPC error {code}: {msg}");
        }

        if (doc.RootElement.TryGetProperty("result", out var result))
            return result.Clone();

        return null;
    }

    private static string ExtractJsonPayload(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "{}";
        var trimmed = raw.TrimStart();
        if (trimmed.StartsWith("{") || trimmed.StartsWith("[")) return raw;

        // SSE: pick the last "data: ..." line that parses as JSON.
        var sb = new StringBuilder();
        foreach (var line in raw.Split('\n'))
        {
            if (line.StartsWith("data:"))
                sb.AppendLine(line["data:".Length..].Trim());
        }
        var combined = sb.ToString().Trim();
        return string.IsNullOrEmpty(combined) ? raw : combined;
    }

    private static string GetString(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty : s.Length <= max ? s : s[..max] + "…";
}
