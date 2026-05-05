using ArkJsonValidator.Models;
using ArkJsonValidator.Services;
using ArkJsonValidator.Validators;
using Microsoft.AspNetCore.Mvc;

namespace ArkJsonValidator.Controllers;

[ApiController]
[Route("api/validate")]
public class ValidationApiController(ValidationService validationService, ValidatorRegistry registry) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Validate([FromBody] ValidationRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.TemplateName))
            return BadRequest(new { error = "templateName is required" });
        if (string.IsNullOrWhiteSpace(req.JsonPayload))
            return BadRequest(new { error = "jsonPayload is required" });

        var result = await validationService.ValidateAsync(req);
        return Ok(result);
    }

    [HttpGet("types")]
    public IActionResult GetValidatorTypes([FromQuery] string? category = null)
    {
        var types = string.IsNullOrEmpty(category)
            ? registry.GetAll()
            : registry.GetByCategory(category);
        return Ok(new { categories = registry.GetCategories(), validators = types });
    }
}
