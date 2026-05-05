using ArkJsonValidator.Models;

namespace ArkJsonValidator.Validators;

public interface IFieldValidator
{
    string Type { get; }
    string DisplayName { get; }
    string Description { get; }
    string Category { get; }
    List<ValidatorParam> Parameters { get; }
    string ExampleValue { get; }

    (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters);
}
