using McpValidatorClient.Models;
using Newtonsoft.Json;
using System.Diagnostics;

namespace McpValidatorClient.Services;

public class ComplaintPipelineService(
    AzureOpenAiService openAi,
    McpClientService mcp,
    ProcessExecutorService executor)
{
    // ── Full pipeline — runs all 5 steps sequentially ──────────────────────
    public async Task<PipelineResult> RunAsync(ComplaintRequest request)
    {
        var result = new PipelineResult { ComplaintText = request.ComplaintText };

        // Step 1 — Classify
        await RunStepAsync(result.Classification, "Complaint Classification", async step =>
        {
            var classification = await openAi.ClassifyComplaintAsync(request.ComplaintText);

            step.Output = $"{classification.IssueCode} (confidence: {classification.Confidence:P0})";
            step.RawJson = JsonConvert.SerializeObject(classification, Formatting.Indented);
            step.Metadata["issueCode"] = classification.IssueCode;
            step.Metadata["rationale"] = classification.Rationale;
            step.Metadata["confidence"] = classification.Confidence.ToString("P0");
        });

        if (result.Classification.Status != StepStatus.Success)
            return MarkRemaining(result, "Classification failed");

        var issueCode = result.Classification.Metadata["issueCode"];

        // Step 2 — Extract entities
        await RunStepAsync(result.EntityExtraction, "Entity Extraction", async step =>
        {
            var extraction = await openAi.ExtractEntitiesAsync(request.ComplaintText, issueCode);

            step.Output = $"Extracted {extraction.FieldCount} field(s)";
            step.RawJson = extraction.EntityJson;
            step.Metadata["fieldCount"] = extraction.FieldCount.ToString();
            step.Metadata["entityJson"] = extraction.EntityJson;
        });

        if (result.EntityExtraction.Status != StepStatus.Success)
            return MarkRemaining(result, "Entity extraction failed");

        var entityJson = result.EntityExtraction.Metadata["entityJson"];

        // Step 3 — Select template via MCP
        await RunStepAsync(result.TemplateSelection, "MCP Template Selection", async step =>
        {
            // Use override if provided
            if (!string.IsNullOrEmpty(request.OverrideTemplate))
            {
                step.Output = $"Using override template: {request.OverrideTemplate}";
                step.Metadata["templateName"] = request.OverrideTemplate;
                step.Metadata["source"] = "override";
                return;
            }

            var templates = await mcp.ListTemplatesAsync();
            if (!templates.Any())
                throw new InvalidOperationException("No templates available on MCP server");

            var selected = await openAi.SelectTemplateAsync(issueCode, entityJson, templates);
            if (string.IsNullOrEmpty(selected))
                selected = templates[0].Name;

            step.Output = $"Selected: \"{selected}\"";
            step.RawJson = JsonConvert.SerializeObject(new
            {
                selectedTemplate = selected,
                availableTemplates = templates.Select(t => new { t.Name, t.Description, t.Tags })
            }, Formatting.Indented);

            step.Metadata["templateName"] = selected;
            step.Metadata["totalTemplates"] = templates.Count.ToString();
        });

        if (result.TemplateSelection.Status != StepStatus.Success)
            return MarkRemaining(result, "Template selection failed");

        var templateName = result.TemplateSelection.Metadata["templateName"];

        // Step 4 — Validate JSON via MCP
        await RunStepAsync(result.Validation, "MCP JSON Validation", async step =>
        {
            var validation = await mcp.ValidateJsonAsync(templateName, entityJson);

            step.Output = validation.IsValid
                ? $"PASSED — {validation.TotalRulesChecked} rules checked"
                : $"FAILED — {validation.Errors.Count} error(s) on {validation.TotalRulesChecked} rules";

            step.RawJson = JsonConvert.SerializeObject(validation, Formatting.Indented);
            step.Metadata["isValid"] = validation.IsValid.ToString();
            step.Metadata["errorCount"] = validation.Errors.Count.ToString();
            step.Metadata["rulesChecked"] = validation.TotalRulesChecked.ToString();

            if (!validation.IsValid)
            {
                // Surface errors in the step but don't throw — let caller decide
                step.Status = StepStatus.Failed;
                step.Error = string.Join("; ", validation.Errors.Take(3).Select(e => $"{e.FieldPath}: {e.ErrorMessage}"));
            }
        });

        // Step 5 — Execute external process (only if validation passed)
        if (result.Validation.Status == StepStatus.Success)
        {
            await RunStepAsync(result.ProcessExecution, "External Process Execution", async step =>
            {
                var exec = await executor.ExecuteAsync(entityJson, issueCode, templateName);

                step.Output = exec.Success
                    ? $"Process completed (exit {exec.ExitCode}) — Ticket: {exec.TicketId}"
                    : $"Process failed (exit {exec.ExitCode})";

                step.RawJson = JsonConvert.SerializeObject(exec, Formatting.Indented);
                step.Metadata["ticketId"] = exec.TicketId;
                step.Metadata["exitCode"] = exec.ExitCode.ToString();
                step.Metadata["command"] = exec.Command;

                if (!exec.Success)
                    throw new InvalidOperationException($"Process exited with code {exec.ExitCode}: {exec.Stderr}");
            });
        }
        else
        {
            result.ProcessExecution.Name = "External Process Execution";
            result.ProcessExecution.Status = StepStatus.Skipped;
            result.ProcessExecution.Output = "Skipped — validation did not pass";
        }

        return result;
    }

    // ── Streaming variant — yields steps one by one for SSE ───────────────
    public async IAsyncEnumerable<PipelineStepEvent> RunStreamingAsync(ComplaintRequest request)
    {
        var issueCode = string.Empty;
        var entityJson = string.Empty;
        var templateName = string.Empty;

        // Step 1
        var (s1, s1Val) = await RunAndYield<ClassificationResult>("Complaint Classification", async () =>
        {
            var r = await openAi.ClassifyComplaintAsync(request.ComplaintText);
            return (new PipelineStep
            {
                Status = StepStatus.Success,
                Output = $"{r.IssueCode} (confidence: {r.Confidence:P0})",
                RawJson = JsonConvert.SerializeObject(r, Formatting.Indented),
                Metadata = { ["issueCode"] = r.IssueCode, ["rationale"] = r.Rationale }
            }, r);
        });
        yield return new PipelineStepEvent("classification", s1);
        if (s1.Status != StepStatus.Success) yield break;
        issueCode = s1.Metadata["issueCode"];

        // Step 2
        var (s2, s2Val) = await RunAndYield<EntityExtractionResult>("Entity Extraction", async () =>
        {
            var r = await openAi.ExtractEntitiesAsync(request.ComplaintText, issueCode);
            return (new PipelineStep
            {
                Status = StepStatus.Success,
                Output = $"Extracted {r.FieldCount} field(s)",
                RawJson = r.EntityJson,
                Metadata = { ["fieldCount"] = r.FieldCount.ToString(), ["entityJson"] = r.EntityJson }
            }, r);
        });
        yield return new PipelineStepEvent("entityExtraction", s2);
        if (s2.Status != StepStatus.Success) yield break;
        entityJson = s2.Metadata["entityJson"];

        // Step 3
        var (s3, _) = await RunAndYield<string>("MCP Template Selection", async () =>
        {
            string selected;
            List<McpTemplateSummary> templates = [];
            if (!string.IsNullOrEmpty(request.OverrideTemplate))
            {
                selected = request.OverrideTemplate;
            }
            else
            {
                templates = await mcp.ListTemplatesAsync();
                selected = await openAi.SelectTemplateAsync(issueCode, entityJson, templates);
                if (string.IsNullOrEmpty(selected) && templates.Any()) selected = templates[0].Name;
            }
            return (new PipelineStep
            {
                Status = StepStatus.Success,
                Output = $"Selected: \"{selected}\"",
                RawJson = JsonConvert.SerializeObject(new { selectedTemplate = selected, availableTemplates = templates.Select(t => t.Name) }, Formatting.Indented),
                Metadata = { ["templateName"] = selected }
            }, selected);
        });
        yield return new PipelineStepEvent("templateSelection", s3);
        if (s3.Status != StepStatus.Success) yield break;
        templateName = s3.Metadata["templateName"];

        // Step 4
        var (s4, _) = await RunAndYield<ValidationStepResult>("MCP JSON Validation", async () =>
        {
            var v = await mcp.ValidateJsonAsync(templateName, entityJson);
            var step = new PipelineStep
            {
                Status = v.IsValid ? StepStatus.Success : StepStatus.Failed,
                Output = v.IsValid ? $"PASSED — {v.TotalRulesChecked} rules" : $"FAILED — {v.Errors.Count} error(s)",
                RawJson = JsonConvert.SerializeObject(v, Formatting.Indented),
                Metadata = { ["isValid"] = v.IsValid.ToString(), ["errorCount"] = v.Errors.Count.ToString() }
            };
            if (!v.IsValid) step.Error = string.Join("; ", v.Errors.Take(3).Select(e => $"{e.FieldPath}: {e.ErrorMessage}"));
            return (step, v);
        });
        yield return new PipelineStepEvent("validation", s4);

        // Step 5
        if (s4.Status == StepStatus.Success)
        {
            var (s5, _) = await RunAndYield<ProcessExecutionResult>("External Process Execution", async () =>
            {
                var exec = await executor.ExecuteAsync(entityJson, issueCode, templateName);
                return (new PipelineStep
                {
                    Status = exec.Success ? StepStatus.Success : StepStatus.Failed,
                    Output = exec.Success ? $"Ticket {exec.TicketId} created (exit {exec.ExitCode})" : $"Failed (exit {exec.ExitCode})",
                    RawJson = JsonConvert.SerializeObject(exec, Formatting.Indented),
                    Error = exec.Success ? null : exec.Stderr,
                    Metadata = { ["ticketId"] = exec.TicketId, ["exitCode"] = exec.ExitCode.ToString() }
                }, exec);
            });
            yield return new PipelineStepEvent("processExecution", s5);
        }
        else
        {
            yield return new PipelineStepEvent("processExecution", new PipelineStep
            {
                Name = "External Process Execution",
                Status = StepStatus.Skipped,
                Output = "Skipped — validation did not pass"
            });
        }
    }

    // ── Internal helpers ───────────────────────────────────────────────────
    private static async Task RunStepAsync(PipelineStep step, string name, Func<PipelineStep, Task> action)
    {
        step.Name = name;
        step.Status = StepStatus.Running;
        var sw = Stopwatch.StartNew();
        try
        {
            await action(step);
            if (step.Status == StepStatus.Running) step.Status = StepStatus.Success;
        }
        catch (Exception ex)
        {
            step.Status = StepStatus.Failed;
            step.Error = ex.Message;
        }
        finally { sw.Stop(); step.ElapsedMs = sw.ElapsedMilliseconds; }
    }

    private static async Task<(PipelineStep Step, T Value)> RunAndYield<T>(string name, Func<Task<(PipelineStep, T)>> action)
        where T : class?
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var (step, val) = await action();
            step.Name = name;
            sw.Stop();
            step.ElapsedMs = sw.ElapsedMilliseconds;
            return (step, val);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (new PipelineStep { Name = name, Status = StepStatus.Failed, Error = ex.Message, ElapsedMs = sw.ElapsedMilliseconds }, null!);
        }
    }

    private static PipelineResult MarkRemaining(PipelineResult r, string reason)
    {
        foreach (var step in new[] { r.EntityExtraction, r.TemplateSelection, r.Validation, r.ProcessExecution })
            if (step.Status == StepStatus.Pending)
            { step.Status = StepStatus.Skipped; step.Output = reason; }
        return r;
    }
}

public record PipelineStepEvent(string StepKey, PipelineStep Step);
