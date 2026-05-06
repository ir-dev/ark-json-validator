#!/usr/bin/env bash
# ARK Complaint Processor — Linux/macOS bash
# Called by ProcessExecutorService after successful validation.
# Environment variables: COMPLAINT_JSON, ISSUE_CODE, TEMPLATE_NAME, TICKET_ID
# Args: $1=ISSUE_CODE  $2=TICKET_ID  $3=TEMPLATE_NAME

set -euo pipefail

ISSUE_CODE="${1:-${ISSUE_CODE:-GENERAL_INQUIRY}}"
TICKET_ID="${2:-${TICKET_ID:-GEN-00000000-0000}}"
TEMPLATE="${3:-${TEMPLATE_NAME:-unknown}}"
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
COMPLAINT_JSON="${COMPLAINT_JSON:-{}}"
JSON_SIZE=${#COMPLAINT_JSON}

# ── Route to the correct queue ─────────────────────────────────────────────
case "$ISSUE_CODE" in
    BILLING_DISPUTE)    QUEUE="finance-disputes"    SLA="2 business days" ;;
    ACCOUNT_ACCESS)     QUEUE="identity-support"    SLA="5 business days" ;;
    PRODUCT_DEFECT)     QUEUE="quality-assurance"   SLA="3 business days" ;;
    SHIPPING_DELAY)     QUEUE="logistics-support"   SLA="5 business days" ;;
    SERVICE_OUTAGE)     QUEUE="incident-management" SLA="1 hour"          ;;
    REFUND_REQUEST)     QUEUE="returns-processing"  SLA="5 business days" ;;
    SAP_PURCHASE_ORDER) QUEUE="erp-support"         SLA="2 business days" ;;
    USER_REGISTRATION)  QUEUE="onboarding"          SLA="5 business days" ;;
    *)                  QUEUE="general-support"     SLA="5 business days" ;;
esac

# ── Validate JSON (requires jq if available) ───────────────────────────────
if command -v jq &>/dev/null; then
    echo "$COMPLAINT_JSON" | jq . >/dev/null 2>&1 || { echo "ERROR: Invalid JSON payload" >&2; exit 2; }
fi

# ── Write to log ───────────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG_FILE="$SCRIPT_DIR/complaint_log.jsonl"
echo "{\"ticketId\":\"$TICKET_ID\",\"issueCode\":\"$ISSUE_CODE\",\"queue\":\"$QUEUE\",\"sla\":\"$SLA\",\"createdAt\":\"$TIMESTAMP\",\"status\":\"OPEN\"}" \
    >> "$LOG_FILE" 2>/dev/null || true

# ── Output result ──────────────────────────────────────────────────────────
echo "=== ARK Complaint Processor (Linux) ==="
echo "Ticket ID   : $TICKET_ID"
echo "Issue Code  : $ISSUE_CODE"
echo "Template    : $TEMPLATE"
echo "Queue       : $QUEUE"
echo "SLA         : $SLA"
echo "Timestamp   : $TIMESTAMP"
echo "Status      : ACCEPTED"
echo "Payload     : ${JSON_SIZE} bytes"
echo "=========================================="
echo "Complaint successfully routed to [$QUEUE] queue."

exit 0
