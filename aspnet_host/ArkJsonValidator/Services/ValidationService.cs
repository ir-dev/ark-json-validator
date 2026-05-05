using ArkJsonValidator.Models;
using ArkJsonValidator.Validators;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace ArkJsonValidator.Services;

public class ValidationService(TemplateService templateService, ValidatorRegistry registry)
{
    public async Task<ValidationResponse> ValidateAsync(ValidationRequest request)
    {
        var response = new ValidationResponse { TemplateName = request.TemplateName };

        var template = await templateService.GetAsync(request.TemplateName);
        if (template is null)
        {
            response.IsValid = false;
            response.Errors.Add(new FieldValidationError
            {
                FieldPath = "_template",
                ErrorMessage = $"Template '{request.TemplateName}' not found"
            });
            return response;
        }

        JObject? json = null;
        try { json = JObject.Parse(request.JsonPayload); }
        catch (Exception ex)
        {
            response.IsValid = false;
            response.Errors.Add(new FieldValidationError
            {
                FieldPath = "_json",
                ErrorMessage = $"Invalid JSON: {ex.Message}"
            });
            return response;
        }

        // Validate field rules
        foreach (var rule in template.FieldRules.OrderBy(r => r.Order))
        {
            response.TotalRulesChecked++;
            var fieldValue = GetFieldValue(json, rule.FieldPath);

            if (fieldValue is null && !rule.IsRequired) continue;
            if (fieldValue is null && rule.IsRequired && rule.ValidatorType != "required")
            {
                response.Errors.Add(new FieldValidationError
                {
                    FieldPath = rule.FieldPath,
                    ValidatorType = rule.ValidatorType,
                    ErrorMessage = string.IsNullOrEmpty(rule.ErrorMessage)
                        ? $"Field '{rule.FieldPath}' is required but missing"
                        : rule.ErrorMessage
                });
                if (request.StopOnFirstError) break;
                continue;
            }

            var validator = registry.Get(rule.ValidatorType);
            if (validator is null)
            {
                response.Warnings.Add($"Unknown validator type '{rule.ValidatorType}' on field '{rule.FieldPath}'");
                continue;
            }

            var parameters = DeserializeParameters(rule.Parameters);
            var (isValid, errorMessage) = validator.Validate(fieldValue, parameters);

            if (!isValid)
            {
                response.Errors.Add(new FieldValidationError
                {
                    FieldPath = rule.FieldPath,
                    ValidatorType = rule.ValidatorType,
                    ErrorMessage = !string.IsNullOrEmpty(rule.ErrorMessage) ? rule.ErrorMessage : errorMessage,
                    ActualValue = fieldValue
                });
                if (request.StopOnFirstError) break;
            }
        }

        if (!request.StopOnFirstError || !response.Errors.Any())
        {
            // Validate group rules
            foreach (var gr in template.GroupRules)
            {
                response.TotalRulesChecked++;
                var fieldValues = gr.FieldPaths.ToDictionary(
                    fp => fp, fp => GetFieldValue(json, fp));

                if (!Enum.TryParse<GroupValidatorType>(
                    string.Concat(gr.GroupValidatorType.Split('_').Select(w => char.ToUpper(w[0]) + w[1..])),
                    out var groupType)) continue;

                var (isValid, errorMessage) = groupType.Validate(
                    gr.GroupName, gr.FieldPaths, fieldValues, gr.ErrorMessage);

                if (!isValid)
                    response.Errors.Add(new FieldValidationError
                    {
                        FieldPath = string.Join(", ", gr.FieldPaths),
                        ErrorMessage = errorMessage,
                        GroupName = gr.GroupName
                    });
            }
        }

        response.IsValid = !response.Errors.Any();
        return response;
    }

    private static object? GetFieldValue(JObject json, string fieldPath)
    {
        var parts = fieldPath.Split('.');
        JToken? current = json;
        foreach (var part in parts)
        {
            if (current is null) return null;
            if (part.EndsWith(']') && part.Contains('['))
            {
                var propName = part[..part.IndexOf('[')];
                var indexStr = part[(part.IndexOf('[') + 1)..^1];
                current = current[propName];
                if (int.TryParse(indexStr, out var idx) && current is JArray arr)
                    current = idx < arr.Count ? arr[idx] : null;
            }
            else current = current[part];
        }
        return current switch
        {
            null => null,
            JValue jv => jv.Value,
            JObject jo => jo.ToString(),
            JArray ja => ja.ToString(),
            _ => current.ToString()
        };
    }

    private static Dictionary<string, object>? DeserializeParameters(Dictionary<string, object>? parameters)
    {
        if (parameters is null) return null;
        var result = new Dictionary<string, object>();
        foreach (var (k, v) in parameters)
        {
            if (v is JsonElement je) result[k] = (object)je;
            else result[k] = v;
        }
        return result;
    }
}
