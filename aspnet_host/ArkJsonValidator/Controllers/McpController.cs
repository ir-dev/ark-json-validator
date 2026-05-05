using ArkJsonValidator.Models;
using ArkJsonValidator.Services;
using ArkJsonValidator.Validators;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ArkJsonValidator.Controllers;

[ApiController]
[Route("mcp")]
public class McpController(TemplateService templateService, ValidationService validationService, ValidatorRegistry registry) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

    [HttpPost]
    public async Task<IActionResult> Handle([FromBody] McpRequest req)
    {
        var response = req.Method switch
        {
            "initialize" => HandleInitialize(req),
            "notifications/initialized" => null,
            "tools/list" => HandleToolsList(req),
            "tools/call" => await HandleToolCall(req),
            "resources/list" => await HandleResourcesList(req),
            "resources/read" => await HandleResourceRead(req),
            "resources/templates/list" => HandleResourceTemplatesList(req),
            _ => Error(req.Id, -32601, $"Method not found: {req.Method}")
        };

        if (response is null) return Ok();
        return Ok(response);
    }

    private McpResponse HandleInitialize(McpRequest req) => new()
    {
        Id = req.Id,
        Result = new
        {
            protocolVersion = "2024-11-05",
            serverInfo = new McpServerInfo(),
            capabilities = new McpCapabilities
            {
                Tools = new McpToolsCapability(),
                Resources = new McpResourcesCapability()
            }
        }
    };

    private McpResponse HandleToolsList(McpRequest req) => new()
    {
        Id = req.Id,
        Result = new { tools = GetAllTools() }
    };

    private async Task<McpResponse> HandleToolCall(McpRequest req)
    {
        if (req.Params is null) return Error(req.Id, -32602, "params required");

        var toolName = req.Params.Value.TryGetProperty("name", out var n) ? n.GetString() : null;
        var argsEl = req.Params.Value.TryGetProperty("arguments", out var a) ? a : (JsonElement?)null;
        var args = argsEl.HasValue ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsEl.Value.GetRawText()) ?? [] : [];

        McpToolResult result = toolName switch
        {
            "validate_json" => await ToolValidateJson(args),
            "list_templates" => await ToolListTemplates(args),
            "get_template" => await ToolGetTemplate(args),
            "create_template" => await ToolCreateTemplate(args),
            "delete_template" => await ToolDeleteTemplate(args),
            "get_validation_types" => ToolGetValidationTypes(args),
            "check_field" => ToolCheckField(args),
            _ => new McpToolResult { IsError = true, Content = [new() { Text = $"Unknown tool: {toolName}" }] }
        };

        return new McpResponse { Id = req.Id, Result = result };
    }

    private async Task<McpResponse> HandleResourcesList(McpRequest req)
    {
        var templates = await templateService.ListAsync();
        var resources = new List<McpResource>
        {
            new() { Uri = "ark://validation-types/all", Name = "All Validation Types", Description = "Complete list of all available field validators", MimeType = "application/json" },
            new() { Uri = "ark://validation-types/sap", Name = "SAP/OData Validators", Description = "SAP and OData specific field validators", MimeType = "application/json" },
        };
        foreach (var t in templates)
            resources.Add(new() { Uri = $"ark://templates/{Uri.EscapeDataString(t.Name)}", Name = t.Name, Description = t.Description, MimeType = "application/json" });

        return new McpResponse { Id = req.Id, Result = new { resources } };
    }

    private async Task<McpResponse> HandleResourceRead(McpRequest req)
    {
        if (req.Params is null) return Error(req.Id, -32602, "params required");
        var uri = req.Params.Value.TryGetProperty("uri", out var u) ? u.GetString() : null;
        if (string.IsNullOrEmpty(uri)) return Error(req.Id, -32602, "uri required");

        McpResourceContent content;
        if (uri == "ark://validation-types/all")
        {
            content = new() { Uri = uri, Text = JsonSerializer.Serialize(registry.GetAll(), JsonOpts) };
        }
        else if (uri == "ark://validation-types/sap")
        {
            content = new() { Uri = uri, Text = JsonSerializer.Serialize(registry.GetByCategory("SAP / OData"), JsonOpts) };
        }
        else if (uri.StartsWith("ark://templates/"))
        {
            var name = Uri.UnescapeDataString(uri["ark://templates/".Length..]);
            var t = await templateService.GetAsync(name);
            if (t is null) return Error(req.Id, -32002, $"Template '{name}' not found");
            content = new() { Uri = uri, Text = JsonSerializer.Serialize(t, JsonOpts) };
        }
        else return Error(req.Id, -32002, $"Resource not found: {uri}");

        return new McpResponse { Id = req.Id, Result = new { contents = new[] { content } } };
    }

    private McpResponse HandleResourceTemplatesList(McpRequest req) => new()
    {
        Id = req.Id,
        Result = new
        {
            resourceTemplates = new[]
            {
                new { uriTemplate = "ark://templates/{name}", name = "Validation Template", description = "Get a specific validation template by name", mimeType = "application/json" }
            }
        }
    };

    // --- Tool Implementations ---

    private async Task<McpToolResult> ToolValidateJson(Dictionary<string, JsonElement> args)
    {
        var templateName = args.TryGetValue("template_name", out var tn) ? tn.GetString() : null;
        var jsonPayload = args.TryGetValue("json_payload", out var jp) ? jp.GetString() : null;
        var stopOnFirst = args.TryGetValue("stop_on_first_error", out var sof) && sof.ValueKind == JsonValueKind.True;

        if (string.IsNullOrEmpty(templateName) || string.IsNullOrEmpty(jsonPayload))
            return ErrorResult("template_name and json_payload are required");

        var result = await validationService.ValidateAsync(new ValidationRequest
        {
            TemplateName = templateName,
            JsonPayload = jsonPayload,
            StopOnFirstError = stopOnFirst
        });

        return new McpToolResult { Content = [new() { Text = JsonSerializer.Serialize(result, JsonOpts) }] };
    }

    private async Task<McpToolResult> ToolListTemplates(Dictionary<string, JsonElement> _)
    {
        var templates = await templateService.ListAsync();
        return new McpToolResult { Content = [new() { Text = JsonSerializer.Serialize(templates, JsonOpts) }] };
    }

    private async Task<McpToolResult> ToolGetTemplate(Dictionary<string, JsonElement> args)
    {
        var name = args.TryGetValue("name", out var n) ? n.GetString() : null;
        if (string.IsNullOrEmpty(name)) return ErrorResult("name is required");
        var t = await templateService.GetAsync(name);
        return t is null
            ? ErrorResult($"Template '{name}' not found")
            : new McpToolResult { Content = [new() { Text = JsonSerializer.Serialize(t, JsonOpts) }] };
    }

    private async Task<McpToolResult> ToolCreateTemplate(Dictionary<string, JsonElement> args)
    {
        try
        {
            var req = JsonSerializer.Deserialize<TemplateCreateRequest>(JsonSerializer.Serialize(args), JsonOpts);
            if (req is null || string.IsNullOrEmpty(req.Name)) return ErrorResult("name is required");
            var (result, error) = await templateService.CreateAsync(req);
            return error is not null
                ? ErrorResult(error)
                : new McpToolResult { Content = [new() { Text = $"Template '{result!.Name}' created successfully with {result.FieldRules.Count} field rules" }] };
        }
        catch (Exception ex) { return ErrorResult($"Failed to create template: {ex.Message}"); }
    }

    private async Task<McpToolResult> ToolDeleteTemplate(Dictionary<string, JsonElement> args)
    {
        var name = args.TryGetValue("name", out var n) ? n.GetString() : null;
        if (string.IsNullOrEmpty(name)) return ErrorResult("name is required");
        return await templateService.DeleteAsync(name)
            ? new McpToolResult { Content = [new() { Text = $"Template '{name}' deleted successfully" }] }
            : ErrorResult($"Template '{name}' not found");
    }

    private McpToolResult ToolGetValidationTypes(Dictionary<string, JsonElement> args)
    {
        var category = args.TryGetValue("category", out var c) ? c.GetString() : null;
        var types = string.IsNullOrEmpty(category) ? registry.GetAll() : registry.GetByCategory(category);
        return new McpToolResult { Content = [new() { Text = JsonSerializer.Serialize(new { categories = registry.GetCategories(), validators = types }, JsonOpts) }] };
    }

    private McpToolResult ToolCheckField(Dictionary<string, JsonElement> args)
    {
        var value = args.TryGetValue("value", out var v) ? v.GetString() : null;
        var validatorType = args.TryGetValue("validator_type", out var vt) ? vt.GetString() : null;

        if (string.IsNullOrEmpty(validatorType)) return ErrorResult("validator_type is required");

        var validator = registry.Get(validatorType);
        if (validator is null) return ErrorResult($"Unknown validator type: {validatorType}");

        Dictionary<string, object>? parameters = null;
        if (args.TryGetValue("parameters", out var p) && p.ValueKind == JsonValueKind.Object)
            parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(p.GetRawText());

        var (isValid, errorMsg) = validator.Validate(value, parameters);
        return new McpToolResult
        {
            Content = [new() { Text = JsonSerializer.Serialize(new { is_valid = isValid, error = isValid ? null : errorMsg, value, validator_type = validatorType }, JsonOpts) }]
        };
    }

    // --- Helpers ---

    private static McpToolResult ErrorResult(string msg) =>
        new() { IsError = true, Content = [new() { Text = msg }] };

    private static McpResponse Error(JsonElement? id, int code, string msg) =>
        new() { Id = id, Error = new McpError { Code = code, Message = msg } };

    private static List<McpTool> GetAllTools() =>
    [
        new McpTool
        {
            Name = "validate_json",
            Description = "Validate a JSON payload against a saved validation template. Returns pass/fail status and detailed error list.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    template_name = new { type = "string", description = "Name of the validation template to use" },
                    json_payload = new { type = "string", description = "JSON string to validate" },
                    stop_on_first_error = new { type = "boolean", description = "Stop after the first validation error (default: false)", @default = false }
                },
                required = new[] { "template_name", "json_payload" }
            }
        },
        new McpTool
        {
            Name = "list_templates",
            Description = "List all saved validation templates with their names, descriptions, tags, and rule counts.",
            InputSchema = new { type = "object", properties = new { } }
        },
        new McpTool
        {
            Name = "get_template",
            Description = "Get the full details of a specific validation template including all field rules and group rules.",
            InputSchema = new
            {
                type = "object",
                properties = new { name = new { type = "string", description = "Template name" } },
                required = new[] { "name" }
            }
        },
        new McpTool
        {
            Name = "create_template",
            Description = "Create a new validation template with field rules and optional group rules.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string", description = "Unique template name" },
                    description = new { type = "string", description = "Template description" },
                    tags = new { type = "string", description = "Comma-separated tags" },
                    fieldRules = new
                    {
                        type = "array",
                        description = "Array of field validation rules",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                fieldPath = new { type = "string", description = "Dot-notation path (e.g. order.customer.email)" },
                                validatorType = new { type = "string", description = "Validator type (e.g. email, required, sap_plant_code)" },
                                errorMessage = new { type = "string", description = "Custom error message" },
                                isRequired = new { type = "boolean", @default = true },
                                parameters = new { type = "object", description = "Validator-specific parameters" }
                            },
                            required = new[] { "fieldPath", "validatorType" }
                        }
                    }
                },
                required = new[] { "name" }
            }
        },
        new McpTool
        {
            Name = "delete_template",
            Description = "Delete a validation template by name.",
            InputSchema = new
            {
                type = "object",
                properties = new { name = new { type = "string", description = "Template name to delete" } },
                required = new[] { "name" }
            }
        },
        new McpTool
        {
            Name = "get_validation_types",
            Description = "Get all available validation types with descriptions, parameters, and categories (General, String, Numeric, Format, Date/Time, SAP/OData).",
            InputSchema = new
            {
                type = "object",
                properties = new { category = new { type = "string", description = "Filter by category (optional)" } }
            }
        },
        new McpTool
        {
            Name = "check_field",
            Description = "Test a single value against a specific validator type without a full template.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    value = new { type = "string", description = "The value to check" },
                    validator_type = new { type = "string", description = "Validator type to apply" },
                    parameters = new { type = "object", description = "Optional validator parameters" }
                },
                required = new[] { "validator_type" }
            }
        }
    ];
}
