using McpValidatorClient.Models;
using McpValidatorClient.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;

namespace McpValidatorClient.Controllers;

[ApiController]
[Route("api/complaint")]
public class ComplaintController(
    ComplaintPipelineService pipeline,
    McpClientService mcp,
    ILogger<ComplaintController> logger) : ControllerBase
{
    // ── Full pipeline (batch) ───────────────────────────────────────────────
    [HttpPost("process")]
    public async Task<IActionResult> Process([FromBody] ComplaintRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ComplaintText))
            return BadRequest(new { error = "complaintText is required" });

        try
        {
            var result = await pipeline.RunAsync(req);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pipeline failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── Streaming pipeline via Server-Sent Events ──────────────────────────
    [HttpPost("process/stream")]
    public async Task ProcessStream([FromBody] ComplaintRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ComplaintText))
        {
            Response.StatusCode = 400;
            return;
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        await Response.Body.FlushAsync();

        async Task Send(string eventName, object data)
        {
            var json = JsonConvert.SerializeObject(data, Formatting.None);
            var message = $"event: {eventName}\ndata: {json}\n\n";
            var bytes = Encoding.UTF8.GetBytes(message);
            await Response.Body.WriteAsync(bytes);
            await Response.Body.FlushAsync();
        }

        await Send("start", new { message = "Pipeline started", timestamp = DateTimeOffset.UtcNow });

        try
        {
            await foreach (var evt in pipeline.RunStreamingAsync(req))
            {
                await Send(evt.StepKey, new
                {
                    step = new
                    {
                        name = evt.Step.Name,
                        status = evt.Step.Status.ToString().ToLower(),
                        output = evt.Step.Output,
                        error = evt.Step.Error,
                        rawJson = evt.Step.RawJson,
                        elapsedMs = evt.Step.ElapsedMs,
                        metadata = evt.Step.Metadata
                    }
                });
            }

            await Send("done", new { message = "Pipeline complete", timestamp = DateTimeOffset.UtcNow });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Streaming pipeline error");
            await Send("error", new { message = ex.Message });
        }
    }

    // ── List available templates from MCP server ───────────────────────────
    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates()
    {
        try
        {
            var templates = await mcp.ListTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch templates from MCP server");
            return Ok(new List<McpTemplateSummary>());
        }
    }

    // ── Validate a single field inline ─────────────────────────────────────
    [HttpPost("check-field")]
    public async Task<IActionResult> CheckField([FromBody] CheckFieldRequest req)
    {
        try
        {
            var (isValid, error) = await mcp.CheckFieldAsync(req.Value, req.ValidatorType);
            return Ok(new { isValid, error });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // ── Demo mode — return pre-canned result without real AI calls ─────────
    [HttpPost("process/demo")]
    public IActionResult Demo([FromBody] ComplaintRequest req)
    {
        var issueCode = DetectDemoIssue(req.ComplaintText);
        return Ok(BuildDemoResult(req.ComplaintText, issueCode));
    }

    private static string DetectDemoIssue(string text)
    {
        text = text.ToLowerInvariant();
        if (text.Contains("purchase order") || text.Contains("sap") || text.Contains("po ") || text.Contains("company code")) return "SAP_PURCHASE_ORDER";
        if (text.Contains("register") || text.Contains("sign up") || text.Contains("account creat")) return "USER_REGISTRATION";
        if (text.Contains("invoice") || text.Contains("billed") || text.Contains("charge") || text.Contains("overcharg")) return "BILLING_DISPUTE";
        if (text.Contains("login") || text.Contains("password") || text.Contains("locked") || text.Contains("access")) return "ACCOUNT_ACCESS";
        if (text.Contains("defect") || text.Contains("broken") || text.Contains("damage") || text.Contains("not working")) return "PRODUCT_DEFECT";
        if (text.Contains("shipping") || text.Contains("delivery") || text.Contains("package") || text.Contains("late")) return "SHIPPING_DELAY";
        if (text.Contains("refund") || text.Contains("return") || text.Contains("exchange")) return "REFUND_REQUEST";
        if (text.Contains("down") || text.Contains("outage") || text.Contains("unavailable")) return "SERVICE_OUTAGE";
        return "GENERAL_INQUIRY";
    }

    private static PipelineResult BuildDemoResult(string complaint, string issueCode)
    {
        var (templateName, entityJson) = issueCode switch
        {
            "SAP_PURCHASE_ORDER" => ("SAP Purchase Order", """{"PurchaseOrder":"4500000001","CompanyCode":"1000","Vendor":"0000001234","Currency":"USD","PostingDate":"/Date(1706227200000)/","Plant":"1000"}"""),
            "USER_REGISTRATION"  => ("User Registration",  """{"email":"john.doe@example.com","username":"johndoe","phone":"+1-555-123-4567","age":28,"website":"https://example.com"}"""),
            _                    => ("User Registration",  """{"email":"customer@example.com","username":"user123","age":25}""")
        };

        return new PipelineResult
        {
            ComplaintText = complaint,
            Classification = new PipelineStep { Name = "Complaint Classification", Status = StepStatus.Success, Output = $"{issueCode} (confidence: 92%)", ElapsedMs = 412, RawJson = $"{{\"issueCode\":\"{issueCode}\",\"rationale\":\"The complaint describes a {issueCode.Replace('_', ' ').ToLower()} scenario\",\"confidence\":0.92}}" },
            EntityExtraction = new PipelineStep { Name = "Entity Extraction", Status = StepStatus.Success, Output = "Extracted 6 field(s)", ElapsedMs = 684, RawJson = entityJson },
            TemplateSelection = new PipelineStep { Name = "MCP Template Selection", Status = StepStatus.Success, Output = $"Selected: \"{templateName}\"", ElapsedMs = 28 },
            Validation = new PipelineStep { Name = "MCP JSON Validation", Status = StepStatus.Success, Output = "PASSED — 6 rules checked", ElapsedMs = 15, RawJson = "{\"isValid\":true,\"errors\":[],\"totalRulesChecked\":6}" },
            ProcessExecution = new PipelineStep { Name = "External Process Execution", Status = StepStatus.Success, Output = "Ticket SAP-20240115-4782 created (exit 0)", ElapsedMs = 320, Metadata = { ["ticketId"] = "SAP-20240115-4782" } }
        };
    }
}

public class CheckFieldRequest
{
    public string Value { get; set; } = string.Empty;
    public string ValidatorType { get; set; } = string.Empty;
}
