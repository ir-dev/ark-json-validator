using System.ComponentModel.DataAnnotations;

namespace ArkJsonValidator.Models;

public class ValidationTemplate
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Tags { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<FieldRule> FieldRules { get; set; } = [];
    public ICollection<GroupRule> GroupRules { get; set; } = [];
}

public class FieldRule
{
    public int Id { get; set; }
    public int TemplateId { get; set; }

    [Required, MaxLength(300)]
    public string FieldPath { get; set; } = string.Empty;   // dot-notation: "order.customer.email"

    [Required, MaxLength(50)]
    public string ValidatorType { get; set; } = string.Empty;

    public string ParametersJson { get; set; } = "{}";      // JSON serialized params

    [MaxLength(200)]
    public string ErrorMessage { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;
    public int Order { get; set; } = 0;

    public ValidationTemplate Template { get; set; } = null!;
}

public class GroupRule
{
    public int Id { get; set; }
    public int TemplateId { get; set; }

    [Required, MaxLength(100)]
    public string GroupName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string GroupValidatorType { get; set; } = string.Empty; // at_least_one, exactly_one, all_or_none, mutually_exclusive

    public string FieldPaths { get; set; } = string.Empty;    // comma-separated field paths

    [MaxLength(200)]
    public string ErrorMessage { get; set; } = string.Empty;

    public ValidationTemplate Template { get; set; } = null!;
}
