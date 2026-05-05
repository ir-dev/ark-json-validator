using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArkJsonValidator.Models;

public class McpRequest
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; set; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }
}

public class McpResponse
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; set; } = "2.0";

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpError? Error { get; set; }

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }
}

public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}

public class McpServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "ark-json-validator";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";
}

public class McpCapabilities
{
    [JsonPropertyName("tools")]
    public McpToolsCapability? Tools { get; set; }

    [JsonPropertyName("resources")]
    public McpResourcesCapability? Resources { get; set; }
}

public class McpToolsCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; } = false;
}

public class McpResourcesCapability
{
    [JsonPropertyName("subscribe")]
    public bool Subscribe { get; set; } = false;

    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; } = false;
}

public class McpTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public object InputSchema { get; set; } = new { };
}

public class McpResource
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "application/json";
}

public class McpResourceContent
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "application/json";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class McpToolResult
{
    [JsonPropertyName("content")]
    public List<McpContent> Content { get; set; } = [];

    [JsonPropertyName("isError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsError { get; set; }
}

public class McpContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
