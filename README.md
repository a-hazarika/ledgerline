# Ledgerline

Multi-tenant invoicing for small agencies and studios. Each tenant keeps its own
customers, invoices, payments and branding; invoices go out as HTML email under the
tenant's own name, logo and remittance details.

| Piece    | Stack                                        |
| -------- | -------------------------------------------- |
| `web/`   | Vite + React 19 + TypeScript                 |
| `src/`   | ASP.NET Core (.NET 10) minimal APIs, EF Core  |
| `db/`    | PostgreSQL 17, SQL-first schema and seed data |
| mail     | Mailpit (local SMTP sink)                     |

## Quick start

```bash
docker compose up --build
```

| Service      | URL                                              |
| ------------ | ------------------------------------------------ |
| Web app      | <http://localhost:5173>                          |
| API          | <http://localhost:8080>                          |
| OpenAPI      | <http://localhost:8080/openapi/v1.json>          |
| Mailpit      | <http://localhost:8025>                          |
| Postgres     | `localhost:5432` — `ledgerline` / `ledgerline`   |

The database is created and seeded from `db/init/` the first time the volume is
created, with four tenants (Northwind Studio, Atlas Freight, Verity Health,
BrightPath Labs), customers and a spread of draft, sent and paid invoices.

Pick a tenant from the switcher in the top right, open an invoice, hit **Send
invoice**, and the message shows up in Mailpit.

To start over: `scripts/reset-data.sh`.

## Running the pieces on the host

Compose is the easy path, but the API and web app both run fine outside it as long
as Postgres and Mailpit are up:

```bash
docker compose up -d postgres mailpit

dotnet run --project src/Ledgerline.Api      # http://localhost:8080
cd web && npm install && npm run dev         # http://localhost:5173
```

`appsettings.json` already points at `localhost` for both Postgres and SMTP. Inside
Compose those are overridden with the service names.

## Tests

```bash
dotnet test          # API unit and component tests, no Docker required
cd web && npm run build   # type-checks and bundles the front end
```

## Demo traffic

`scripts/demo-traffic.sh [count]` tops up draft invoices for every seeded tenant
and issues them, which is a faster way to fill Mailpit than clicking through the
UI. It only needs `curl` and `python3`.

```bash
scripts/demo-traffic.sh 20
```

## How tenancy works

Every API call except the operator endpoints carries an `X-Tenant` header holding
the tenant slug:

```bash
curl -H 'X-Tenant: atlas-freight' http://localhost:8080/api/invoices
```

`TenantResolutionMiddleware` maps the slug to a tenant and binds it to the scoped
`ITenantContext`. `LedgerlineDbContext` uses that to scope queries, so feature code
does not repeat a tenant predicate on every query.

`/api/admin/*` is the operator console. It deliberately runs without a tenant and
reports across the whole platform.

## Layout

```
src/Ledgerline.Api/
  Program.cs             composition root
  Tenancy/               slug -> tenant resolution, scoped tenant context
  Data/                  entities and the EF Core model
  Domain/                invoice totals and money formatting
  Features/              minimal API endpoints grouped by feature
  Email/                 outbound queue, worker, renderer, SMTP client
  Templates/             HTML email templates
  wwwroot/branding/      per-tenant logos
tests/Ledgerline.Api.Tests/
web/src/                 React app: invoices, customers, email activity, branding
db/init/                 schema + seed, applied on first boot of the volume
scripts/                 demo traffic, data reset
```

## Sending an invoice

`POST /api/invoices/{id}/send` writes an `email_log` row and pushes a job onto an
in-process channel. `EmailSendingWorker` drains that channel with a few senders in
parallel: it loads the invoice and the tenant's branding, renders the template,
hands the message to Mailpit over SMTP, and marks the invoice sent.

## Changing the schema

The schema is SQL-first — EF Core maps onto the tables in `db/init/01_schema.sql`
rather than owning migrations. Adding a column means editing the SQL and recreating
the volume (`scripts/reset-data.sh`), or applying the change by hand to a running
database.
