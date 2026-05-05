using ArkJsonValidator.Models;
using ArkJsonValidator.Services;
using Microsoft.AspNetCore.Mvc;

namespace ArkJsonValidator.Controllers;

[ApiController]
[Route("api/templates")]
public class TemplateApiController(TemplateService templateService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await templateService.ListAsync());

    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name)
    {
        var t = await templateService.GetAsync(name);
        return t is null ? NotFound(new { error = $"Template '{name}' not found" }) : Ok(t);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TemplateCreateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Template name is required" });

        var (result, error) = await templateService.CreateAsync(req);
        return error is not null
            ? Conflict(new { error })
            : CreatedAtAction(nameof(Get), new { name = result!.Name }, result);
    }

    [HttpPut("{name}")]
    public async Task<IActionResult> Update(string name, [FromBody] TemplateCreateRequest req)
    {
        var (result, error) = await templateService.UpdateAsync(name, req);
        if (error is not null)
            return error.Contains("not found") ? NotFound(new { error }) : Conflict(new { error });
        return Ok(result);
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> Delete(string name)
    {
        return await templateService.DeleteAsync(name)
            ? NoContent()
            : NotFound(new { error = $"Template '{name}' not found" });
    }
}
