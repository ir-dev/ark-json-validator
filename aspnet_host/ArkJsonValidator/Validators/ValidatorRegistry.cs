using ArkJsonValidator.Models;

namespace ArkJsonValidator.Validators;

public class ValidatorRegistry
{
    private readonly Dictionary<string, IFieldValidator> _validators;

    public ValidatorRegistry()
    {
        var all = new List<IFieldValidator>
        {
            new RequiredValidator(),
            new NotNullValidator(),
            new RegexValidator(),
            new EmailValidator(),
            new PhoneValidator(),
            new MinLengthValidator(),
            new MaxLengthValidator(),
            new ExactLengthValidator(),
            new NumericValidator(),
            new IntegerValidator(),
            new MinValueValidator(),
            new MaxValueValidator(),
            new BooleanValidator(),
            new DateValidator(),
            new Iso8601Validator(),
            new GuidValidator(),
            new UrlValidator(),
            new EnumValuesValidator(),
            new ContainsValidator(),
            new StartsWithValidator(),
            new EndsWithValidator(),
            new IpAddressValidator(),
            new JsonValidator(),
            new AlphanumericValidator(),
            // SAP / OData
            new SapODataDateValidator(),
            new SapODataGuidValidator(),
            new SapMaterialNumberValidator(),
            new SapPlantCodeValidator(),
            new SapCompanyCodeValidator(),
            new SapCurrencyCodeValidator(),
            new SapCountryCodeValidator(),
            new SapLanguageKeyValidator(),
            new SapDocumentNumberValidator(),
            new ODataEntityKeyValidator(),
            new SapCostCenterValidator(),
            new SapProfitCenterValidator(),
            new SapSalesOrderValidator(),
        };
        _validators = all.ToDictionary(v => v.Type, StringComparer.OrdinalIgnoreCase);
    }

    public IFieldValidator? Get(string type) => _validators.GetValueOrDefault(type);

    public IEnumerable<ValidatorTypeInfo> GetAll() =>
        _validators.Values.Select(v => new ValidatorTypeInfo
        {
            Type = v.Type,
            DisplayName = v.DisplayName,
            Description = v.Description,
            Category = v.Category,
            Parameters = v.Parameters,
            ExampleValue = v.ExampleValue
        }).OrderBy(v => v.Category).ThenBy(v => v.DisplayName);

    public IEnumerable<ValidatorTypeInfo> GetByCategory(string category) =>
        GetAll().Where(v => v.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<string> GetCategories() =>
        _validators.Values.Select(v => v.Category).Distinct().OrderBy(c => c);
}
