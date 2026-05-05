using ArkJsonValidator.Data;
using ArkJsonValidator.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ArkJsonValidator.Services;

public class TemplateService(AppDbContext db)
{
    public async Task<List<TemplateSummary>> ListAsync() =>
        await db.Templates
            .AsNoTracking()
            .Select(t => new TemplateSummary
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                Tags = t.Tags,
                FieldRuleCount = t.FieldRules.Count,
                GroupRuleCount = t.GroupRules.Count,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .OrderBy(t => t.Name)
            .ToListAsync();

    public async Task<TemplateDetailDto?> GetAsync(string name)
    {
        var t = await db.Templates
            .Include(t => t.FieldRules)
            .Include(t => t.GroupRules)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == name);
        return t is null ? null : MapToDetail(t);
    }

    public async Task<TemplateDetailDto?> GetByIdAsync(int id)
    {
        var t = await db.Templates
            .Include(t => t.FieldRules)
            .Include(t => t.GroupRules)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
        return t is null ? null : MapToDetail(t);
    }

    public async Task<(TemplateDetailDto? Result, string? Error)> CreateAsync(TemplateCreateRequest req)
    {
        if (await db.Templates.AnyAsync(t => t.Name == req.Name))
            return (null, $"Template '{req.Name}' already exists");

        var template = new ValidationTemplate
        {
            Name = req.Name.Trim(),
            Description = req.Description,
            Tags = req.Tags,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var (rule, idx) in req.FieldRules.Select((r, i) => (r, i)))
            template.FieldRules.Add(new FieldRule
            {
                FieldPath = rule.FieldPath,
                ValidatorType = rule.ValidatorType,
                ParametersJson = rule.Parameters is null ? "{}" : JsonSerializer.Serialize(rule.Parameters),
                ErrorMessage = rule.ErrorMessage,
                IsRequired = rule.IsRequired,
                Order = rule.Order == 0 ? idx + 1 : rule.Order
            });

        foreach (var gr in req.GroupRules)
            template.GroupRules.Add(new GroupRule
            {
                GroupName = gr.GroupName,
                GroupValidatorType = gr.GroupValidatorType,
                FieldPaths = string.Join(",", gr.FieldPaths),
                ErrorMessage = gr.ErrorMessage
            });

        db.Templates.Add(template);
        await db.SaveChangesAsync();
        return (MapToDetail(template), null);
    }

    public async Task<(TemplateDetailDto? Result, string? Error)> UpdateAsync(string name, TemplateCreateRequest req)
    {
        var template = await db.Templates
            .Include(t => t.FieldRules)
            .Include(t => t.GroupRules)
            .FirstOrDefaultAsync(t => t.Name == name);

        if (template is null) return (null, $"Template '{name}' not found");

        if (req.Name != name && await db.Templates.AnyAsync(t => t.Name == req.Name))
            return (null, $"Template name '{req.Name}' already taken");

        template.Name = req.Name.Trim();
        template.Description = req.Description;
        template.Tags = req.Tags;
        template.UpdatedAt = DateTime.UtcNow;

        db.FieldRules.RemoveRange(template.FieldRules);
        db.GroupRules.RemoveRange(template.GroupRules);

        foreach (var (rule, idx) in req.FieldRules.Select((r, i) => (r, i)))
            template.FieldRules.Add(new FieldRule
            {
                FieldPath = rule.FieldPath,
                ValidatorType = rule.ValidatorType,
                ParametersJson = rule.Parameters is null ? "{}" : JsonSerializer.Serialize(rule.Parameters),
                ErrorMessage = rule.ErrorMessage,
                IsRequired = rule.IsRequired,
                Order = rule.Order == 0 ? idx + 1 : rule.Order
            });

        foreach (var gr in req.GroupRules)
            template.GroupRules.Add(new GroupRule
            {
                GroupName = gr.GroupName,
                GroupValidatorType = gr.GroupValidatorType,
                FieldPaths = string.Join(",", gr.FieldPaths),
                ErrorMessage = gr.ErrorMessage
            });

        await db.SaveChangesAsync();
        return (MapToDetail(template), null);
    }

    public async Task<bool> DeleteAsync(string name)
    {
        var t = await db.Templates.FirstOrDefaultAsync(t => t.Name == name);
        if (t is null) return false;
        db.Templates.Remove(t);
        await db.SaveChangesAsync();
        return true;
    }

    private static TemplateDetailDto MapToDetail(ValidationTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Description = t.Description,
        Tags = t.Tags,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        FieldRules = t.FieldRules.OrderBy(r => r.Order).Select(r => new FieldRuleDto
        {
            FieldPath = r.FieldPath,
            ValidatorType = r.ValidatorType,
            Parameters = string.IsNullOrWhiteSpace(r.ParametersJson) || r.ParametersJson == "{}"
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(r.ParametersJson),
            ErrorMessage = r.ErrorMessage,
            IsRequired = r.IsRequired,
            Order = r.Order
        }).ToList(),
        GroupRules = t.GroupRules.Select(gr => new GroupRuleDto
        {
            GroupName = gr.GroupName,
            GroupValidatorType = gr.GroupValidatorType,
            FieldPaths = [.. gr.FieldPaths.Split(',', StringSplitOptions.RemoveEmptyEntries)],
            ErrorMessage = gr.ErrorMessage
        }).ToList()
    };
}
