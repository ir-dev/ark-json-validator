using ArkJsonValidator.Models;
using Microsoft.EntityFrameworkCore;

namespace ArkJsonValidator.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ValidationTemplate> Templates { get; set; }
    public DbSet<FieldRule> FieldRules { get; set; }
    public DbSet<GroupRule> GroupRules { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ValidationTemplate>(e =>
        {
            e.HasIndex(t => t.Name).IsUnique();
            e.HasMany(t => t.FieldRules).WithOne(r => r.Template).HasForeignKey(r => r.TemplateId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(t => t.GroupRules).WithOne(r => r.Template).HasForeignKey(r => r.TemplateId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FieldRule>(e =>
        {
            e.Property(r => r.ParametersJson).HasDefaultValue("{}");
        });

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ValidationTemplate>().HasData(new ValidationTemplate
        {
            Id = 1,
            Name = "SAP Purchase Order",
            Description = "Validates SAP OData purchase order payload fields",
            Tags = "sap,odata,purchase-order",
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        modelBuilder.Entity<FieldRule>().HasData(
            new FieldRule { Id = 1, TemplateId = 1, FieldPath = "PurchaseOrder", ValidatorType = "sap_document_number", ErrorMessage = "PurchaseOrder must be a valid SAP document number (up to 10 digits)", Order = 1 },
            new FieldRule { Id = 2, TemplateId = 1, FieldPath = "CompanyCode", ValidatorType = "sap_company_code", ErrorMessage = "CompanyCode must be a 4-character alphanumeric value", Order = 2 },
            new FieldRule { Id = 3, TemplateId = 1, FieldPath = "Vendor", ValidatorType = "required", ErrorMessage = "Vendor is required", Order = 3 },
            new FieldRule { Id = 4, TemplateId = 1, FieldPath = "Currency", ValidatorType = "sap_currency_code", ErrorMessage = "Currency must be a valid ISO 4217 3-letter code", Order = 4 },
            new FieldRule { Id = 5, TemplateId = 1, FieldPath = "PostingDate", ValidatorType = "sap_odata_date", ErrorMessage = "PostingDate must be in SAP OData date format /Date(epoch)/", Order = 5 },
            new FieldRule { Id = 6, TemplateId = 1, FieldPath = "Plant", ValidatorType = "sap_plant_code", ErrorMessage = "Plant must be a 4-character SAP plant code", Order = 6 }
        );

        modelBuilder.Entity<ValidationTemplate>().HasData(new ValidationTemplate
        {
            Id = 2,
            Name = "User Registration",
            Description = "Validates user registration form fields",
            Tags = "user,registration,common",
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        modelBuilder.Entity<FieldRule>().HasData(
            new FieldRule { Id = 7, TemplateId = 2, FieldPath = "email", ValidatorType = "email", ErrorMessage = "Please provide a valid email address", Order = 1 },
            new FieldRule { Id = 8, TemplateId = 2, FieldPath = "phone", ValidatorType = "phone", ErrorMessage = "Please provide a valid phone number", Order = 2, IsRequired = false },
            new FieldRule { Id = 9, TemplateId = 2, FieldPath = "username", ValidatorType = "min_length", ParametersJson = """{"min":3}""", ErrorMessage = "Username must be at least 3 characters", Order = 3 },
            new FieldRule { Id = 10, TemplateId = 2, FieldPath = "username", ValidatorType = "max_length", ParametersJson = """{"max":50}""", ErrorMessage = "Username must be at most 50 characters", Order = 4 },
            new FieldRule { Id = 11, TemplateId = 2, FieldPath = "age", ValidatorType = "min_value", ParametersJson = """{"min":18}""", ErrorMessage = "User must be at least 18 years old", Order = 5, IsRequired = false },
            new FieldRule { Id = 12, TemplateId = 2, FieldPath = "website", ValidatorType = "url", ErrorMessage = "Please provide a valid URL", Order = 6, IsRequired = false }
        );
    }
}
