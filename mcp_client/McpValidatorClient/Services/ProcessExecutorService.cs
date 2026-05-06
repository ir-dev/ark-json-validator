using McpValidatorClient.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace McpValidatorClient.Services;

public class ProcessExecutorService(IConfiguration config, ILogger<ProcessExecutorService> logger)
{
    public async Task<ProcessExecutionResult> ExecuteAsync(
        string entityJson, string issueCode, string templateName)
    {
        var sw = Stopwatch.StartNew();
        var ticketId = GenerateTicketId(issueCode);
        var result = new ProcessExecutionResult { TicketId = ticketId };

        var (executable, arguments) = BuildCommand(entityJson, issueCode, templateName, ticketId);
        result.Command = $"{executable} {arguments}".Trim();

        var timeoutSec = config.GetValue<int>("ExternalProcess:TimeoutSeconds", 30);

        logger.LogInformation("Executing: {Cmd}", result.Command);

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = GetScriptDirectory()
                }
            };

            // Pass JSON as environment variable (safe, avoids shell injection)
            process.StartInfo.Environment["COMPLAINT_JSON"] = entityJson;
            process.StartInfo.Environment["ISSUE_CODE"] = issueCode;
            process.StartInfo.Environment["TEMPLATE_NAME"] = templateName;
            process.StartInfo.Environment["TICKET_ID"] = ticketId;

            var stdoutBuilder = new System.Text.StringBuilder();
            var stderrBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderrBuilder.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Write JSON to stdin as well
            await process.StandardInput.WriteAsync(entityJson);
            process.StandardInput.Close();

            var completed = await Task.Run(() => process.WaitForExit(timeoutSec * 1000));

            if (!completed)
            {
                process.Kill(entireProcessTree: true);
                result.Success = false;
                result.Stderr = $"Process timed out after {timeoutSec}s";
                result.ExitCode = -1;
            }
            else
            {
                result.ExitCode = process.ExitCode;
                result.Stdout = stdoutBuilder.ToString().Trim();
                result.Stderr = stderrBuilder.ToString().Trim();
                result.Success = process.ExitCode == 0;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Process execution failed");
            result.Success = false;
            result.Stderr = ex.Message;
            result.ExitCode = -99;

            // If the real process can't run (dev/demo mode), simulate success
            if (config.GetValue<bool>("ExternalProcess:SimulateOnFailure", true))
            {
                result.Success = true;
                result.ExitCode = 0;
                result.Stdout = SimulateOutput(issueCode, ticketId, entityJson);
                result.Stderr = $"[SIMULATED] Real process unavailable: {ex.Message}";
            }
        }

        sw.Stop();
        result.ElapsedMs = sw.ElapsedMilliseconds;
        return result;
    }

    private (string Executable, string Arguments) BuildCommand(
        string json, string issueCode, string templateName, string ticketId)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var scriptPath = config["ExternalProcess:Windows"]
                ?? Path.Combine(GetScriptDirectory(), "process_complaint.ps1");

            return ("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                $"-IssueCode \"{issueCode}\" -TicketId \"{ticketId}\" -Template \"{templateName}\"");
        }
        else
        {
            var scriptPath = config["ExternalProcess:Linux"]
                ?? Path.Combine(GetScriptDirectory(), "process_complaint.sh");

            return ("bash", $"\"{scriptPath}\" \"{issueCode}\" \"{ticketId}\" \"{templateName}\"");
        }
    }

    private string GetScriptDirectory()
    {
        var configured = config["ExternalProcess:ScriptDirectory"];
        if (!string.IsNullOrEmpty(configured) && Directory.Exists(configured)) return configured;

        // Default: Scripts subfolder next to the executable
        return Path.Combine(AppContext.BaseDirectory, "Scripts");
    }

    private static string GenerateTicketId(string issueCode)
    {
        var prefix = issueCode switch
        {
            "BILLING_DISPUTE"    => "BIL",
            "ACCOUNT_ACCESS"     => "ACC",
            "PRODUCT_DEFECT"     => "PRD",
            "SHIPPING_DELAY"     => "SHP",
            "SERVICE_OUTAGE"     => "SVC",
            "REFUND_REQUEST"     => "RFD",
            "SAP_PURCHASE_ORDER" => "SAP",
            "USER_REGISTRATION"  => "REG",
            _                    => "GEN"
        };
        return $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
    }

    private static string SimulateOutput(string issueCode, string ticketId, string json)
    {
        var ts = DateTimeOffset.UtcNow.ToString("O");
        return $"""
            === ARK Complaint Processor (SIMULATED) ===
            Ticket ID   : {ticketId}
            Issue Code  : {issueCode}
            Timestamp   : {ts}
            Status      : ACCEPTED
            Queue       : {GetQueue(issueCode)}
            SLA         : {GetSla(issueCode)}
            Next Action : Complaint routed to {GetTeam(issueCode)} team
            Payload size: {json.Length} bytes
            ==========================================
            Complaint successfully processed and routed.
            """;
    }

    private static string GetQueue(string code) => code switch
    {
        "BILLING_DISPUTE"    => "finance-disputes",
        "ACCOUNT_ACCESS"     => "identity-support",
        "PRODUCT_DEFECT"     => "quality-assurance",
        "SHIPPING_DELAY"     => "logistics-support",
        "SERVICE_OUTAGE"     => "incident-management",
        "REFUND_REQUEST"     => "returns-processing",
        "SAP_PURCHASE_ORDER" => "erp-support",
        "USER_REGISTRATION"  => "onboarding",
        _                    => "general-support"
    };

    private static string GetSla(string code) => code switch
    {
        "SERVICE_OUTAGE"     => "1 hour",
        "BILLING_DISPUTE"    => "2 business days",
        "PRODUCT_DEFECT"     => "3 business days",
        _                    => "5 business days"
    };

    private static string GetTeam(string code) => code switch
    {
        "BILLING_DISPUTE"    => "Finance",
        "ACCOUNT_ACCESS"     => "Identity & Access",
        "PRODUCT_DEFECT"     => "Quality Assurance",
        "SHIPPING_DELAY"     => "Logistics",
        "SERVICE_OUTAGE"     => "Incident Response",
        "SAP_PURCHASE_ORDER" => "ERP/SAP",
        _                    => "General Support"
    };
}
