namespace ArkJsonValidator.Models;

public class ValidationRequest
{
    public string TemplateName { get; set; } = string.Empty;
    public string JsonPayload { get; set; } = string.Empty;
    public bool StopOnFirstError { get; set; } = false;
}

public class ValidationResponse
{
    public bool IsValid { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public List<FieldValidationError> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public int TotalRulesChecked { get; set; }
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

public class FieldValidationError
{
    public string FieldPath { get; set; } = string.Empty;
    public string ValidatorType { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public object? ActualValue { get; set; }
    public string? GroupName { get; set; }
}

public class ValidatorTypeInfo
{
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<ValidatorParam> Parameters { get; set; } = [];
    public string ExampleValue { get; set; } = string.Empty;
}

public class ValidatorParam
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;   // string, number, boolean, array
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; } = false;
    public object? DefaultValue { get; set; }
}

public class TemplateCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public List<FieldRuleDto> FieldRules { get; set; } = [];
    public List<GroupRuleDto> GroupRules { get; set; } = [];
}

public class FieldRuleDto
{
    public string FieldPath { get; set; } = string.Empty;
    public string ValidatorType { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public int Order { get; set; } = 0;
}

public class GroupRuleDto
{
    public string GroupName { get; set; } = string.Empty;
    public string GroupValidatorType { get; set; } = string.Empty;
    public List<string> FieldPaths { get; set; } = [];
    public string ErrorMessage { get; set; } = string.Empty;
}

public class TemplateSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public int FieldRuleCount { get; set; }
    public int GroupRuleCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TemplateDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public List<FieldRuleDto> FieldRules { get; set; } = [];
    public List<GroupRuleDto> GroupRules { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
