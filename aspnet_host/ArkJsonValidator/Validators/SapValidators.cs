using System.Text.RegularExpressions;
using ArkJsonValidator.Models;

namespace ArkJsonValidator.Validators;

public class SapODataDateValidator : IFieldValidator
{
    public string Type => "sap_odata_date";
    public string DisplayName => "SAP OData Date";
    public string Description => "SAP OData date format: /Date(milliseconds)/ or /Date(milliseconds+offset)/";
    public string Category => "SAP / OData";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "/Date(1706227200000)/";

    private static readonly Regex Pattern = new(@"^\/Date\(\d+([+-]\d{4})?\)\/$");

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? _)
    {
        if (value is null) return (true, string.Empty);
        return Pattern.IsMatch(value.ToString()!)
            ? (true, string.Empty)
            : (false, "Value must be SAP OData date format: /Date(milliseconds)/ or /Date(milliseconds+0000)/");
    }
}

public class SapODataGuidValidator : IFieldValidator
{
    public string Type => "sap_odata_guid";
    public string DisplayName => "SAP OData GUID";
    public string Description => "SAP OData GUID format: guid'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'";
    public string Category => "SAP / OData";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "guid'550e8400-e29b-41d4-a716-446655440000'";

    private static readonly Regex Pattern = new(@"^guid'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}'$", RegexOptions.IgnoreCase);

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? _)
    {
        if (value is null) return (true, string.Empty);
        return Pattern.IsMatch(value.ToString()!)
            ? (true, string.Empty)
            : (false, "Value must be SAP OData GUID format: guid'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'");
    }
}

public class SapMaterialNumberValidator : IFieldValidator
{
    public string Type => "sap_material_number";
    public string DisplayName => "SAP Material Number";
    public string Description => "SAP material number: up to 18 characters (internal format is left-padded with zeros)";
    public string Category => "SAP / OData";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "internal_format", Type = "boolean", Description = "Expect 18-char zero-padded internal format", Required = false, DefaultValue = false }
    ];
    public string ExampleValue => "000000000000010023";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        var s = value.ToString()!;
        var internalFormat = Convert.ToBoolean(parameters?.GetValueOrDefault("internal_format") ?? false);
        if (internalFormat)
        {
            if (s.Length != 18) return (false, "SAP material number internal format must be exactly 18 characters");
            if (!Regex.IsMatch(s, @"^[0-9A-Za-z\-_/. ]{18}$")) return (false, "Invalid SAP material number format");
            return (true, string.Empty);
        }
        if (s.Length > 18) return (false, "SAP material number cannot exceed 18 characters");
        if (!Regex.IsMatch(s, @"^[0-9A-Za-z\-_/. ]+$")) return (false, "SAP material number contains invalid characters");
        return (true, string.Empty);
    }
}

public class SapPlantCodeValidator : IFieldValidator
{
    public string Type => "sap_plant_code";
    public string DisplayName => "SAP Plant Code";
    public string Description => "SAP plant code: 4-character alphanumeric";
    public string Category => "SAP / OData";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "1000";

    private static readonly Regex Pattern = new(@"^[0-9A-Za-z]{4}$");

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? _)
    {
        if (value is null) return (true, string.Empty);
        return Pattern.IsMatch(value.ToString()!)
            ? (true, string.Empty) : (false, "SAP plant code must be exactly 4 alphanumeric characters");
    }
}

public class SapCompanyCodeValidator : IFieldValidator
{
    public string Type => "sap_company_code";
    public string DisplayName => "SAP Company Code";
    public string Description => "SAP company code: exactly 4 alphanumeric characters";
    public string Category => "SAP / OData";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "1000";

    private static readonly Regex Pattern = new(@"^[0-9A-Za-z]{4}$");

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? _)
    {
        if (value is null) return (true, string.Empty);
        return Pattern.IsMatch(value.ToString()!)
            ? (true, string.Empty) : (false, "SAP company code must be exactly 4 alphanumeric characters");
    }
}

public class SapCurrencyCodeValidator : IFieldValidator
{
    public string Type => "sap_currency_code";
    public string DisplayName => "SAP Currency Code";
    public string Description => "ISO 4217 currency code: 3 uppercase letters";
    public string Category => "SAP / OData";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "USD";

    private static readonly HashSet<string> ValidCurrencies =
    [
        "AED","AFN","ALL","AMD","ANG","AOA","ARS","AUD","AWG","AZN","BAM","BBD","BDT","BGN",
        "BHD","BIF","BMD","BND","BOB","BRL","BSD","BTN","BWP","BYN","BZD","CAD","CDF","CHF",
        "CLP","CNY","COP","CRC","CUP","CVE","CZK","DJF","DKK","DOP","DZD","EGP","ERN","ETB",
        "EUR","FJD","FKP","GBP","GEL","GHS","GIP","GMD","GNF","GTQ","GYD","HKD","HNL","HRK",
        "HTG","HUF","IDR","ILS","INR","IQD","IRR","ISK","JMD","JOD","JPY","KES","KGS","KHR",
        "KMF","KPW","KRW","KWD","KYD","KZT","LAK","LBP","LKR","LRD","LSL","LYD","MAD","MDL",
        "MGA","MKD","MMK","MNT","MOP","MRU","MUR","MVR","MWK","MXN","MYR","MZN","NAD","NGN",
        "NIO","NOK","NPR","NZD","OMR","PAB","PEN","PGK","PHP","PKR","PLN","PYG","QAR","RON",
        "RSD","RUB","RWF","SAR","SBD","SCR","SDG","SEK","SGD","SHP","SLL","SOS","SRD","STN",
        "SVC","SYP","SZL","THB","TJS","TMT","TND","TOP","TRY","TTD","TWD","TZS","UAH","UGX",
        "USD","UYU","UZS","VES","VND","VUV","WST","XAF","XCD","XOF","XPF","YER","ZAR","ZMW","ZWL"
    ];

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? _)
    {
        if (value is null) return (true, string.Empty);
        var s = value.ToString()!.ToUpperInvariant();
        return ValidCurrencies.Contains(s) ? (true, string.Empty) : (false, $"'{value}' is not a valid ISO 4217 currency code");
    }
}

public class SapCountryCodeValidator : IFieldValidator
{
    public string Type => "sap_country_code";
    public string DisplayName => "SAP Country Code";
    public string Description => "ISO 3166-1 alpha-2 country code: 2 uppercase letters";
    public string Category => "SAP / OData";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "US";

    private static readonly Regex Pattern = new(@"^[A-Z]{2}$");

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? _)
    {
        if (value is null) return (true, string.Empty);
        var s = value.ToString()!.ToUpperInvariant();
        return Pattern.IsMatch(s) ? (true, string.Empty) : (false, "Country code must be 2 uppercase letters (ISO 3166-1 alpha-2)");
    }
}

public class SapLanguageKeyValidator : IFieldValidator
{
    public string Type => "sap_language_key";
    public string DisplayName => "SAP Language Key";
    public string Description => "SAP language key: 1-2 character code (E=English, D=German, etc.)";
    public string Category => "SAP / OData";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "EN";

    private static readonly HashSet<string> ValidKeys =
    [
        "AF","AR","BG","CA","CS","DA","DE","EL","EN","ES","ET","FI","FR","HE","HR",
        "HU","ID","IT","JA","KO","LT","LV","MS","NL","NO","PL","PT","RO","RU","SK",
        "SL","SR","SV","TH","TR","UK","VI","ZH",
        "A","B","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z"
    ];

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? _)
    {
        if (value is null) return (true, string.Empty);
        var s = value.ToString()!.ToUpperInvariant();
        return ValidKeys.Contains(s) ? (true, string.Empty) : (false, $"'{value}' is not a valid SAP language key");
    }
}

public class SapDocumentNumberValidator : IFieldValidator
{
    public string Type => "sap_document_number";
    public string DisplayName => "SAP Document Number";
    public string Description => "SAP document/object number: up to 10 digits";
    public string Category => "SAP / OData";
    public List<ValidatorParam> Parameters =>
    [
        new() { Name = "padded", Type = "boolean", Description = "Expect 10-char zero-padded format", Required = false, DefaultValue = false }
    ];
    public string ExampleValue => "4500000001";

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? parameters)
    {
        if (value is null) return (true, string.Empty);
        var s = value.ToString()!;
        var padded = Convert.ToBoolean(parameters?.GetValueOrDefault("padded") ?? false);
        if (!Regex.IsMatch(s, @"^\d+$")) return (false, "SAP document number must contain only digits");
        if (padded && s.Length != 10) return (false, "SAP document number (padded) must be exactly 10 digits");
        if (s.Length > 10) return (false, "SAP document number cannot exceed 10 digits");
        return (true, string.Empty);
    }
}

public class ODataEntityKeyValidator : IFieldValidator
{
    public string Type => "odata_entity_key";
    public string DisplayName => "OData Entity Key";
    public string Description => "Valid OData entity key (e.g., 42, 'value', or guid'...')";
    public string Category => "SAP / OData";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "'MyEntity'";

    private static readonly Regex NumericKey = new(@"^\d+[LMDlmd]?$");
    private static readonly Regex StringKey = new(@"^'[^']*'$");
    private static readonly Regex GuidKey = new(@"^guid'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}'$", RegexOptions.IgnoreCase);

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? _)
    {
        if (value is null) return (true, string.Empty);
        var s = value.ToString()!;
        return NumericKey.IsMatch(s) || StringKey.IsMatch(s) || GuidKey.IsMatch(s)
            ? (true, string.Empty) : (false, "Value is not a valid OData entity key format");
    }
}

public class SapCostCenterValidator : IFieldValidator
{
    public string Type => "sap_cost_center";
    public string DisplayName => "SAP Cost Center";
    public string Description => "SAP cost center: up to 10 alphanumeric characters";
    public string Category => "SAP / OData";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "1000000001";

    private static readonly Regex Pattern = new(@"^[0-9A-Za-z]{1,10}$");

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? _)
    {
        if (value is null) return (true, string.Empty);
        return Pattern.IsMatch(value.ToString()!)
            ? (true, string.Empty) : (false, "SAP cost center must be 1-10 alphanumeric characters");
    }
}

public class SapProfitCenterValidator : IFieldValidator
{
    public string Type => "sap_profit_center";
    public string DisplayName => "SAP Profit Center";
    public string Description => "SAP profit center: up to 10 alphanumeric characters";
    public string Category => "SAP / OData";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "PC001";

    private static readonly Regex Pattern = new(@"^[0-9A-Za-z]{1,10}$");

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? _)
    {
        if (value is null) return (true, string.Empty);
        return Pattern.IsMatch(value.ToString()!)
            ? (true, string.Empty) : (false, "SAP profit center must be 1-10 alphanumeric characters");
    }
}

public class SapSalesOrderValidator : IFieldValidator
{
    public string Type => "sap_sales_order";
    public string DisplayName => "SAP Sales Order";
    public string Description => "SAP sales order number: up to 10 digits starting with 1-9";
    public string Category => "SAP / OData";
    public List<ValidatorParam> Parameters => [];
    public string ExampleValue => "0000004321";

    private static readonly Regex Pattern = new(@"^\d{1,10}$");

    public (bool IsValid, string ErrorMessage) Validate(object? value, Dictionary<string, object>? _)
    {
        if (value is null) return (true, string.Empty);
        return Pattern.IsMatch(value.ToString()!)
            ? (true, string.Empty) : (false, "SAP sales order must be numeric with up to 10 digits");
    }
}
