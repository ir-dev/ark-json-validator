# ARK Complaint Processor — Windows PowerShell
# Called by ProcessExecutorService after successful validation.
# Environment variables: COMPLAINT_JSON, ISSUE_CODE, TEMPLATE_NAME, TICKET_ID
# Also receives JSON on stdin.

param(
    [string]$IssueCode   = $env:ISSUE_CODE,
    [string]$TicketId    = $env:TICKET_ID,
    [string]$Template    = $env:TEMPLATE_NAME
)

$timestamp = Get-Date -Format "o"
$json      = $env:COMPLAINT_JSON

# ── Parse the JSON payload ─────────────────────────────────────────────────
try {
    $payload = $json | ConvertFrom-Json
} catch {
    Write-Error "Invalid JSON payload: $_"
    exit 2
}

# ── Route to the correct handler ───────────────────────────────────────────
$queue = switch ($IssueCode) {
    "BILLING_DISPUTE"    { "finance-disputes"     }
    "ACCOUNT_ACCESS"     { "identity-support"     }
    "PRODUCT_DEFECT"     { "quality-assurance"    }
    "SHIPPING_DELAY"     { "logistics-support"    }
    "SERVICE_OUTAGE"     { "incident-management"  }
    "REFUND_REQUEST"     { "returns-processing"   }
    "SAP_PURCHASE_ORDER" { "erp-support"          }
    "USER_REGISTRATION"  { "onboarding"           }
    default              { "general-support"      }
}

$sla = switch ($IssueCode) {
    "SERVICE_OUTAGE"  { "1 hour"            }
    "BILLING_DISPUTE" { "2 business days"   }
    "PRODUCT_DEFECT"  { "3 business days"   }
    default           { "5 business days"   }
}

# ── Simulate writing to a ticketing system / database ─────────────────────
$ticket = [PSCustomObject]@{
    TicketId    = $TicketId
    IssueCode   = $IssueCode
    Template    = $Template
    Queue       = $queue
    SLA         = $sla
    CreatedAt   = $timestamp
    Status      = "OPEN"
    PayloadSize = $json.Length
}

# In production: send to REST API, write to DB, call webhook, etc.
# $ticket | ConvertTo-Json | Invoke-RestMethod -Uri "https://ticketing.internal/api/tickets" -Method POST

# ── Log to a local file ────────────────────────────────────────────────────
$logPath = Join-Path $PSScriptRoot "complaint_log.jsonl"
$ticket | ConvertTo-Json -Compress | Add-Content -Path $logPath -ErrorAction SilentlyContinue

# ── Output result ──────────────────────────────────────────────────────────
Write-Output "=== ARK Complaint Processor (Windows) ==="
Write-Output "Ticket ID   : $TicketId"
Write-Output "Issue Code  : $IssueCode"
Write-Output "Template    : $Template"
Write-Output "Queue       : $queue"
Write-Output "SLA         : $sla"
Write-Output "Timestamp   : $timestamp"
Write-Output "Status      : ACCEPTED"
Write-Output "Payload     : $($json.Length) bytes"
Write-Output "=========================================="
Write-Output "Complaint successfully routed to [$queue] queue."

exit 0
