namespace ArkJsonValidator.Validators;

public enum GroupValidatorType
{
    AtLeastOne,       // at least one field in group must have a value
    ExactlyOne,       // exactly one field must have a value
    AllOrNone,        // either all fields have values or none do
    MutuallyExclusive // only one can have a value (same as ExactlyOne but error message differs)
}

public static class GroupValidatorTypeExtensions
{
    public static (bool IsValid, string ErrorMessage) Validate(
        this GroupValidatorType type, string groupName, List<string> fieldPaths,
        Dictionary<string, object?> fieldValues, string customError)
    {
        var presentFields = fieldPaths.Where(f => fieldValues.TryGetValue(f, out var v) && v is not null && v.ToString() != "").ToList();

        return type switch
        {
            GroupValidatorType.AtLeastOne =>
                presentFields.Count >= 1 ? (true, string.Empty)
                    : (false, string.IsNullOrEmpty(customError)
                        ? $"Group '{groupName}': At least one of [{string.Join(", ", fieldPaths)}] must have a value"
                        : customError),

            GroupValidatorType.ExactlyOne =>
                presentFields.Count == 1 ? (true, string.Empty)
                    : (false, string.IsNullOrEmpty(customError)
                        ? $"Group '{groupName}': Exactly one of [{string.Join(", ", fieldPaths)}] must have a value (found {presentFields.Count})"
                        : customError),

            GroupValidatorType.AllOrNone =>
                presentFields.Count == 0 || presentFields.Count == fieldPaths.Count ? (true, string.Empty)
                    : (false, string.IsNullOrEmpty(customError)
                        ? $"Group '{groupName}': Either all or none of [{string.Join(", ", fieldPaths)}] must have values"
                        : customError),

            GroupValidatorType.MutuallyExclusive =>
                presentFields.Count <= 1 ? (true, string.Empty)
                    : (false, string.IsNullOrEmpty(customError)
                        ? $"Group '{groupName}': Only one of [{string.Join(", ", fieldPaths)}] can have a value at a time"
                        : customError),

            _ => (true, string.Empty)
        };
    }
}
