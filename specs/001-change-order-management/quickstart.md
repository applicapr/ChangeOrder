# Phase 1 — Quickstart: Change Order Management

**Feature**: `001-change-order-management`
**Date**: 2026-05-12

This quickstart shows how to bring the feature up locally for the first time and validate the primary user journey (US1 — create an order). It assumes the implementation has reached the point where `Host` runs against a real SQL Server. Until `/speckit-implement` produces that code, this document is a **target state contract** for the test harness.

---

## Prerequisites

| Tool | Version | Why |
|---|---|---|
| .NET SDK | 10.0.x | Build + run |
| SQL Server | 2022+ or LocalDB / Docker `mcr.microsoft.com/mssql/server:2022-latest` | Persistence |
| `dotnet ef` | 10.x | Apply migrations |
| `docker` | (optional) | Containerized SQL or app |

Check installations:

```bash
dotnet --version          # → 10.0.x
sqlcmd -? | head -1       # if using local SQL
docker --version          # if using Docker SQL
```

---

## Environment variables (this host only)

If you are working on the same dev host that reproduces the HTTP/2-ALPN bug against `api.nuget.org` (documented in `research.md` R-10), every `dotnet` invocation MUST run with:

```bash
export DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT=false
export DOTNET_SYSTEM_NET_DISABLEIPV6=1
```

Add them to your shell rc file so they are present in every terminal. CI runners (GitHub Actions ubuntu-latest) do NOT need them.

---

## 1. Configure the connection string

Edit `src/ChangeOrder.Host/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ChangeOrderDb;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

For Docker SQL Server:

```json
"DefaultConnection": "Server=localhost,1433;Database=ChangeOrderDb;User Id=sa;Password=YourStrong!Pass;TrustServerCertificate=true;"
```

---

## 2. Apply the initial migration

```bash
dotnet ef database update \
  --project src/ChangeOrder.Data \
  --startup-project src/ChangeOrder.Host
```

Expected output ends with `Done.` and creates two tables: `dbo.ChangeOrders` (with the UNIQUE index on `OrderNumber`) and `dbo.IdempotencyKeys`.

---

## 3. Run the API

```bash
dotnet run --project src/ChangeOrder.Host --launch-profile http
```

You should see:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5151
info: Microsoft.Hosting.Lifetime[0]
      Application started.
```

OpenAPI is served at `http://localhost:5151/openapi/v1.json`. Visit Swagger UI at `http://localhost:5151/swagger` once the dev profile is wired.

---

## 4. Health check

```bash
curl -sS -o /dev/null -w "HTTP %{http_code}\n" http://localhost:5151/health
# → HTTP 200
```

If SQL Server is down, the same call MUST return `HTTP 503` within 5 seconds (SC-007).

---

## 5. End-to-end smoke test — US1 (Create order)

### 5.1 Submit a new change request

```bash
curl -sS -X POST http://localhost:5151/api/v1/change-orders \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: 11111111-2222-3333-4444-555555555555" \
  -d '{
    "programName": "BillingCore",
    "productionVersion": "4.7.2",
    "versionScreenshotPath": "screenshots/pre/BillingCore-4.7.2.png",
    "workDescription": "Fix withholding rounding bug in module X",
    "requestDetails": "Repro: enter $123.45 → rounding produces $123.40 instead of $123.45.",
    "justification": "Customers are being undercharged; tax compliance risk.",
    "requiredAction": "Patch ModuleX.RoundUp() to use MidpointRounding.AwayFromZero.",
    "requester": {
      "name": "Jose Lara",
      "position": "Senior Developer",
      "department": "Programming",
      "email": "jlara@example.com"
    }
  }'
```

Expected response: HTTP 201 with body containing `"orderNumber": "20260512-01"` (or the next available sequence for today's UTC date) and `"status": "Draft"`.

### 5.2 Retry with the same Idempotency-Key (SC-003)

Repeat exactly the same `curl` from 5.1. Expected: HTTP 200 (NOT 201) with **the same `id` and `orderNumber`** — no new row was created.

### 5.3 Retry with the same key but different body

Send the same `Idempotency-Key` but change one field. Expected: HTTP 422 with `code: "idempotency.payload_divergence"`.

### 5.4 List orders

```bash
curl -sS "http://localhost:5151/api/v1/change-orders?page=1&pageSize=10" | jq
```

Expected: `{ "items": [ {...} ], "totalCount": 1, "page": 1, "pageSize": 10 }`.

### 5.5 Soft delete

```bash
curl -sS -X DELETE http://localhost:5151/api/v1/change-orders/<id>
# → HTTP 204

# Verify it disappears from default listing
curl -sS "http://localhost:5151/api/v1/change-orders?page=1&pageSize=10" | jq '.totalCount'
# → 0
```

Confirm the row physically remains in the database:

```sql
SELECT Id, OrderNumber, IsDeleted, DeletedAt FROM dbo.ChangeOrders;
-- → 1 row with IsDeleted=1 and DeletedAt populated
```

---

## 6. Concurrency smoke test — SC-001

```bash
# Submit 50 simultaneous orders; collect the resulting OrderNumbers
seq 1 50 | xargs -P 50 -I{} curl -sS -o /dev/null -w "%{http_code} " \
  -X POST http://localhost:5151/api/v1/change-orders \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: stress-$(date +%s)-{}" \
  -d @smoke-payload.json
```

Expected: 50 × `201`, zero `5xx`, and `SELECT COUNT(DISTINCT OrderNumber) FROM dbo.ChangeOrders WHERE OrderNumber LIKE '$(date -u +%Y%m%d)-%'` returns `50`.

---

## 7. Rate limit smoke — SC-005

```bash
seq 1 105 | xargs -I{} curl -sS -o /dev/null -w "%{http_code}\n" http://localhost:5151/api/v1/change-orders | sort | uniq -c
# Expected:
#  100 200    (or 201 depending on what list returns; adjust)
#    5 429
```

The 101st request through 105th MUST return `HTTP 429` with a `Retry-After` header.

---

## 8. Run the test suite

```bash
dotnet test
```

Unit + integration. The Testcontainers SQL Server tests under `tests/ChangeOrder.Data.Tests/` need Docker running locally; tag them with `[Trait("Category", "Testcontainers")]` so they can be filtered in CI:

```bash
dotnet test --filter "Category!=Testcontainers"     # fast feedback
dotnet test --filter "Category=Testcontainers"      # full validation
```

---

## 9. Stop and clean up

```bash
# Stop the API: Ctrl+C in its terminal
docker stop $(docker ps -q --filter ancestor=mcr.microsoft.com/mssql/server) 2>/dev/null
```

To start fresh: drop the database and re-run section 2.

```sql
DROP DATABASE ChangeOrderDb;
```

---

## 10. Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json` | HTTP/2 ALPN filtered on this host | Export the two env vars from §"Environment variables" above |
| `CS9137: The 'interceptors' feature is not enabled in this namespace` | `Host.csproj` missing the `<InterceptorsNamespaces>` line | See `research.md` R-8 |
| `SqlException: Cannot open database "ChangeOrderDb"` | Migrations not applied | Run §2 |
| API starts but `/health` returns 503 | SQL Server unreachable | Check connection string + SQL Server process |
| `POST /change-orders` returns 500 with `OrderNumber violates UNIQUE constraint` after 3 retries | More than 99 same-day attempts collided despite retries | Verify `OrderNumberGenerator` retry count and review concurrent submission pattern |
