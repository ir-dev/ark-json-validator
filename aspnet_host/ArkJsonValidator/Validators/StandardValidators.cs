using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using ArkJsonValidator.Models;

namespace ArkJsonValidator.Validators;

public class RequiredValidator : IFieldValidator
{
    public string Type => "required";
    public string DisplayName => "Required";
    public string Description => "Field must be present and not null or empty";
    public string Category => "General";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "any non-empty value";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (false, "Field is required and cannot be null");
        var s = value.ToString();
        if (string.IsNullOrWhiteSpace(s)) return (false, "Field is required and cannot be empty");
        return (true, string.Empty);
    }
}

public class NotNullValidator : IFieldValidator
{
    public string Type => "not_null";
    public string DisplayName => "Not Null";
    public string Description => "Field must not be null (empty string is allowed)";
    public string Category => "General";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "\"\" or any value";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
        => value is null ? (false, "Field cannot be null") : (true, string.Empty);
}

public class RegexValidator : IFieldValidator
{
    public string Type => "regex";
    public string DisplayName => "Regex Pattern";
    public string Description => "Field value must match a regular expression pattern";
    public string Category => "General";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "pattern", Type = "string", Description = "Regular expression pattern", Required = true },
        new() { Name = "flags", Type = "string", Description = "Regex flags (i=ignore case, m=multiline)", Required = false, DefaultValue = "" }
    ];
    public string ExampleValue => "^[A-Z]{2}\\d{4}$";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        var pattern = parameters?.GetValueOrDefault("pattern")?.ToString() ?? "";
        if (string.IsNullOrEmpty(pattern)) return (false, "Regex pattern not configured");
        var flags = parameters?.GetValueOrDefault("flags")?.ToString() ?? "";
        var options = RegexOptions.None;
        if (flags.Contains('i')) options |= RegexOptions.IgnoreCase;
        if (flags.Contains('m')) options |= RegexOptions.Multiline;
        return Regex.IsMatch(value.ToString()!, pattern, options)
            ? (true, string.Empty)
            : (false, $"Value does not match pattern: {pattern}");
    }
}

public class EmailValidator : IFieldValidator
{
    public string Type => "email";
    public string DisplayName => "Email Address";
    public string Description => "Field must be a valid email address";
    public string Category => "Format";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "user@example.com";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        try
        {
            var addr = new MailAddress(value.ToString()!);
            return addr.Address == value.ToString()!.Trim()
                ? (true, string.Empty)
                : (false, "Invalid email address format");
        }
        catch { return (false, "Invalid email address format"); }
    }
}

public class PhoneValidator : IFieldValidator
{
    public string Type => "phone";
    public string DisplayName => "Phone Number";
    public string Description => "Field must be a valid phone number (E.164 or common formats)";
    public string Category => "Format";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "format", Type = "string", Description = "Format: any, e164, us", Required = false, DefaultValue = "any" }
    ];
    public string ExampleValue => "+1-555-123-4567";

    private static readonly Regex AnyPhone = new(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{3}[-\s\.]?[0-9]{4,6}$");
    private static readonly Regex E164 = new(@"^\+[1-9]\d{1,14}$");
    private static readonly Regex UsPhone = new(@"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$");

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        var s = value.ToString()!;
        var format = parameters?.GetValueOrDefault("format")?.ToString() ?? "any";
        var regex = format switch { "e164" => E164, "us" => UsPhone, _ => AnyPhone };
        return regex.IsMatch(s) ? (true, string.Empty) : (false, $"Invalid phone number format ({format})");
    }
}

public class MinLengthValidator : IFieldValidator
{
    public string Type => "min_length";
    public string DisplayName => "Minimum Length";
    public string Description => "String must be at least N characters long";
    public string Category => "String";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "min", Type = "number", Description = "Minimum character count", Required = true }
    ];
    public string ExampleValue => "hello";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        var min = Convert.ToInt32(parameters?.GetValueOrDefault("min") ?? 0);
        var len = value.ToString()!.Length;
        return len >= min ? (true, string.Empty) : (false, $"Value length {len} is less than minimum {min}");
    }
}

public class MaxLengthValidator : IFieldValidator
{
    public string Type => "max_length";
    public string DisplayName => "Maximum Length";
    public string Description => "String must not exceed N characters";
    public string Category => "String";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "max", Type = "number", Description = "Maximum character count", Required = true }
    ];
    public string ExampleValue => "hello";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        var max = Convert.ToInt32(parameters?.GetValueOrDefault("max") ?? int.MaxValue);
        var len = value.ToString()!.Length;
        return len <= max ? (true, string.Empty) : (false, $"Value length {len} exceeds maximum {max}");
    }
}

public class ExactLengthValidator : IFieldValidator
{
    public string Type => "exact_length";
    public string DisplayName => "Exact Length";
    public string Description => "String must be exactly N characters";
    public string Category => "String";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "length", Type = "number", Description = "Required character count", Required = true }
    ];
    public string ExampleValue => "AB12";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        var length = Convert.ToInt32(parameters?.GetValueOrDefault("length") ?? 0);
        var len = value.ToString()!.Length;
        return len == length ? (true, string.Empty) : (false, $"Value length {len} must be exactly {length}");
    }
}

public class NumericValidator : IFieldValidator
{
    public string Type => "numeric";
    public string DisplayName => "Numeric";
    public string Description => "Field must be a numeric value";
    public string Category => "Numeric";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "42.5";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        return double.TryParse(value.ToString(), out double _d) ? (true, string.Empty) : (false, "Value must be numeric");
    }
}

public class IntegerValidator : IFieldValidator
{
    public string Type => "integer";
    public string DisplayName => "Integer";
    public string Description => "Field must be a whole number (no decimals)";
    public string Category => "Numeric";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "42";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        return long.TryParse(value.ToString(), out long _l) ? (true, string.Empty) : (false, "Value must be an integer");
    }
}

public class MinValueValidator : IFieldValidator
{
    public string Type => "min_value";
    public string DisplayName => "Minimum Value";
    public string Description => "Numeric value must be greater than or equal to minimum";
    public string Category => "Numeric";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "min", Type = "number", Description = "Minimum allowed value", Required = true }
    ];
    public string ExampleValue => "18";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        if (!double.TryParse(value.ToString(), out var num)) return (false, "Value must be numeric");
        var min = Convert.ToDouble(parameters?.GetValueOrDefault("min") ?? double.MinValue);
        return num >= min ? (true, string.Empty) : (false, $"Value {num} is less than minimum {min}");
    }
}

public class MaxValueValidator : IFieldValidator
{
    public string Type => "max_value";
    public string DisplayName => "Maximum Value";
    public string Description => "Numeric value must be less than or equal to maximum";
    public string Category => "Numeric";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "max", Type = "number", Description = "Maximum allowed value", Required = true }
    ];
    public string ExampleValue => "100";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        if (!double.TryParse(value.ToString(), out var num)) return (false, "Value must be numeric");
        var max = Convert.ToDouble(parameters?.GetValueOrDefault("max") ?? double.MaxValue);
        return num <= max ? (true, string.Empty) : (false, $"Value {num} exceeds maximum {max}");
    }
}

public class BooleanValidator : IFieldValidator
{
    public string Type => "boolean";
    public string DisplayName => "Boolean";
    public string Description => "Field must be true or false";
    public string Category => "General";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "true";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        var s = value.ToString()!.ToLowerInvariant();
        return s is "true" or "false" or "1" or "0" or "yes" or "no"
            ? (true, string.Empty) : (false, "Value must be boolean (true/false)");
    }
}

public class DateValidator : IFieldValidator
{
    public string Type => "date";
    public string DisplayName => "Date";
    public string Description => "Field must be a parseable date value";
    public string Category => "Date/Time";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "format", Type = "string", Description = "Expected date format (e.g. yyyy-MM-dd)", Required = false }
    ];
    public string ExampleValue => "2024-01-15";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        var format = parameters?.GetValueOrDefault("format")?.ToString();
        if (!string.IsNullOrEmpty(format))
            return DateTime.TryParseExact(value.ToString(), format, null, System.Globalization.DateTimeStyles.None, out _)
                ? (true, string.Empty) : (false, $"Date does not match format {format}");
        return DateTime.TryParse(value.ToString(), out _) ? (true, string.Empty) : (false, "Invalid date value");
    }
}

public class Iso8601Validator : IFieldValidator
{
    public string Type => "iso8601";
    public string DisplayName => "ISO 8601 DateTime";
    public string Description => "Field must be an ISO 8601 date/datetime string";
    public string Category => "Date/Time";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "2024-01-15T10:30:00Z";

    private static readonly string[] Formats =
        ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ", "yyyy-MM-ddTHH:mm:ss.fffZ",
         "yyyy-MM-ddTHH:mm:sszzz", "yyyy-MM-ddTHH:mm:ss.fff", "yyyyMMdd"];

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        return DateTime.TryParseExact(value.ToString(), Formats, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out DateTime _dt)
            ? (true, string.Empty) : (false, "Value is not a valid ISO 8601 date/datetime");
    }
}

public class GuidValidator : IFieldValidator
{
    public string Type => "guid";
    public string DisplayName => "GUID / UUID";
    public string Description => "Field must be a valid GUID/UUID";
    public string Category => "Format";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "550e8400-e29b-41d4-a716-446655440000";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        return Guid.TryParse(value.ToString(), out Guid _g) ? (true, string.Empty) : (false, "Value is not a valid GUID");
    }
}

public class UrlValidator : IFieldValidator
{
    public string Type => "url";
    public string DisplayName => "URL";
    public string Description => "Field must be a valid URL";
    public string Category => "Format";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "schemes", Type = "array", Description = "Allowed URL schemes (default: http, https)", Required = false }
    ];
    public string ExampleValue => "https://example.com";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        if (!Uri.TryCreate(value.ToString(), UriKind.Absolute, out var uri))
            return (false, "Value is not a valid URL");
        var schemes = new[] { "http", "https" };
        return schemes.Contains(uri.Scheme.ToLower()) ? (true, string.Empty) : (false, $"URL scheme '{uri.Scheme}' is not allowed");
    }
}

public class EnumValuesValidator : IFieldValidator
{
    public string Type => "enum";
    public string DisplayName => "Enum Values";
    public string Description => "Field must be one of the specified allowed values";
    public string Category => "General";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "values", Type = "array", Description = "List of allowed values", Required = true },
        new() { Name = "case_sensitive", Type = "boolean", Description = "Case-sensitive comparison", Required = false, DefaultValue = false }
    ];
    public string ExampleValue => "ACTIVE";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        var caseSensitive = Convert.ToBoolean(parameters?.GetValueOrDefault("case_sensitive") ?? false);
        var allowed = parameters?.GetValueOrDefault("values");
        List<string> allowedValues = [];
        if (allowed is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var item in je.EnumerateArray()) allowedValues.Add(item.ToString());
        if (!allowedValues.Any()) return (false, "Enum validator: no allowed values configured");
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return allowedValues.Any(v => v.Equals(value.ToString(), comparison))
            ? (true, string.Empty)
            : (false, $"Value must be one of: {string.Join(", ", allowedValues)}");
    }
}

public class ContainsValidator : IFieldValidator
{
    public string Type => "contains";
    public string DisplayName => "Contains";
    public string Description => "String must contain the specified substring";
    public string Category => "String";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "value", Type = "string", Description = "Substring to search for", Required = true },
        new() { Name = "case_sensitive", Type = "boolean", Description = "Case-sensitive match", Required = false, DefaultValue = false }
    ];
    public string ExampleValue => "Hello World";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        var search = parameters?.GetValueOrDefault("value")?.ToString() ?? "";
        var caseSensitive = Convert.ToBoolean(parameters?.GetValueOrDefault("case_sensitive") ?? false);
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return value.ToString()!.Contains(search, comparison)
            ? (true, string.Empty) : (false, $"Value must contain '{search}'");
    }
}

public class StartsWithValidator : IFieldValidator
{
    public string Type => "starts_with";
    public string DisplayName => "Starts With";
    public string Description => "String must start with the specified prefix";
    public string Category => "String";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "prefix", Type = "string", Description = "Required prefix", Required = true }
    ];
    public string ExampleValue => "PO-12345";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        var prefix = parameters?.GetValueOrDefault("prefix")?.ToString() ?? "";
        return value.ToString()!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? (true, string.Empty) : (false, $"Value must start with '{prefix}'");
    }
}

public class EndsWithValidator : IFieldValidator
{
    public string Type => "ends_with";
    public string DisplayName => "Ends With";
    public string Description => "String must end with the specified suffix";
    public string Category => "String";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "suffix", Type = "string", Description = "Required suffix", Required = true }
    ];
    public string ExampleValue => "document.pdf";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        var suffix = parameters?.GetValueOrDefault("suffix")?.ToString() ?? "";
        return value.ToString()!.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? (true, string.Empty) : (false, $"Value must end with '{suffix}'");
    }
}

public class IpAddressValidator : IFieldValidator
{
    public string Type => "ip_address";
    public string DisplayName => "IP Address";
    public string Description => "Field must be a valid IPv4 or IPv6 address";
    public string Category => "Format";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "version", Type = "string", Description = "IP version: any, ipv4, ipv6", Required = false, DefaultValue = "any" }
    ];
    public string ExampleValue => "192.168.1.1";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        if (!IPAddress.TryParse(value.ToString(), out var ip)) return (false, "Invalid IP address");
        var version = parameters?.GetValueOrDefault("version")?.ToString() ?? "any";
        if (version == "ipv4" && ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return (false, "Value must be an IPv4 address");
        if (version == "ipv6" && ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
            return (false, "Value must be an IPv6 address");
        return (true, string.Empty);
    }
}

public class JsonValidator : IFieldValidator
{
    public string Type => "valid_json";
    public string DisplayName => "Valid JSON";
    public string Description => "Field value must be a valid JSON string";
    public string Category => "Format";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "{\"key\":\"value\"}";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        try
        {
            System.Text.Json.JsonDocument.Parse(value.ToString()!);
            return (true, string.Empty);
        }
        catch { return (false, "Value is not valid JSON"); }
    }
}

public class AlphanumericValidator : IFieldValidator
{
    public string Type => "alphanumeric";
    public string DisplayName => "Alphanumeric";
    public string Description => "Field must contain only letters and digits";
    public string Category => "String";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "allow_spaces", Type = "boolean", Description = "Allow space characters", Required = false, DefaultValue = false }
    ];
    public string ExampleValue => "ABC123";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        var allowSpaces = Convert.ToBoolean(parameters?.GetValueOrDefault("allow_spaces") ?? false);
        var pattern = allowSpaces ? @"^[a-zA-Z0-9 ]+$" : @"^[a-zA-Z0-9]+$";
        return Regex.IsMatch(value.ToString()!, pattern)
            ? (true, string.Empty) : (false, "Value must contain only alphanumeric characters");
    }
}
