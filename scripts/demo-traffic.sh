#!/usr/bin/env bash
#
# Generates demo activity against a running stack: tops up draft invoices for each
# seeded tenant, then issues them round-robin so the tenants are all busy at once.
# Handy for filling Mailpit with realistic mail instead of clicking Send in the UI
# one invoice at a time.
#
#   scripts/demo-traffic.sh            # 5 invoices per tenant
#   scripts/demo-traffic.sh 20         # 20 invoices per tenant
#   API=http://localhost:8080 PARALLEL=16 scripts/demo-traffic.sh
#
set -euo pipefail

API="${API:-http://localhost:8080}"
PER_TENANT="${1:-5}"
PARALLEL="${PARALLEL:-16}"
TENANTS=(northwind-studio atlas-freight verity-health brightpath-labs)

command -v curl >/dev/null || { echo "missing dependency: curl" >&2; exit 1; }
PY="${PY:-$(command -v python3 || command -v python || true)}"
[ -n "$PY" ] || { echo "missing dependency: python3" >&2; exit 1; }

api_get() { curl -sf -H "X-Tenant: $1" "$API/api$2"; }

draft_ids() {
  api_get "$1" "/invoices?status=draft" | "$PY" -c "
import sys, json
for invoice in json.load(sys.stdin):
    print(invoice['id'])
" | tr -d '\r'
}

first_customer() {
  api_get "$1" "/customers" \
    | "$PY" -c "import sys, json; print(json.load(sys.stdin)[0]['id'])" \
    | tr -d '\r'
}

create_draft() {
  curl -sf -o /dev/null -X POST "$API/api/invoices" \
    -H "X-Tenant: $1" \
    -H 'Content-Type: application/json' \
    -d "{\"customerId\":\"$2\",\"termDays\":30,\"currency\":\"USD\",\"lines\":[
          {\"description\":\"Platform usage\",\"quantity\":1,\"unitPriceCents\":$((RANDOM % 40000 + 10000)),\"taxRateBp\":875}]}"
}

echo "==> topping up drafts (${PER_TENANT} per tenant)"
for tenant in "${TENANTS[@]}"; do
  customer=$(first_customer "$tenant")
  have=$(draft_ids "$tenant" | grep -c . || true)
  while [ "$have" -lt "$PER_TENANT" ]; do
    create_draft "$tenant" "$customer"
    have=$((have + 1))
  done
  echo "    $tenant: $have draft(s) ready"
done

# Collect PER_TENANT drafts per tenant into one flat, tenant-major list.
ids=()
for tenant in "${TENANTS[@]}"; do
  taken=0
  while read -r id; do
    if [ "$taken" -ge "$PER_TENANT" ]; then break; fi
    ids+=("$id")
    taken=$((taken + 1))
  done < <(draft_ids "$tenant")
  while [ "$taken" -lt "$PER_TENANT" ]; do
    ids+=("")
    taken=$((taken + 1))
  done
done

# Re-order round-robin so consecutive requests come from different tenants, the way
# traffic actually arrives, then fire them together from a single curl process.
echo "==> issuing invoices"
requests=()
sent=0
slot=0
while [ "$slot" -lt "$PER_TENANT" ]; do
  index=0
  while [ "$index" -lt "${#TENANTS[@]}" ]; do
    id=${ids[$((index * PER_TENANT + slot))]}
    if [ -n "$id" ]; then
      if [ "$sent" -gt 0 ]; then requests+=(--next); fi
      requests+=(-X POST -H "X-Tenant: ${TENANTS[$index]}" -o /dev/null "$API/api/invoices/$id/send")
      sent=$((sent + 1))
    fi
    index=$((index + 1))
  done
  slot=$((slot + 1))
done

if [ "$sent" -gt 0 ]; then
  curl -sS --parallel --parallel-immediate --parallel-max "$PARALLEL" "${requests[@]}"
fi

echo "==> queued $sent invoice(s); inbox: http://localhost:8025"
