# Project Ledger API

Project Ledger API is a multi-tenant financial backend for projects, built with ASP.NET Core and Entity Framework Core.
It supports expenses, incomes, obligations, budgets, reports, billing, OCR-assisted draft extraction, and MCP endpoints.

## Tech Stack
- .NET 10 (ASP.NET Core Web API)
- Entity Framework Core 10
- PostgreSQL/CockroachDB via Npgsql
- JWT authentication and role/policy authorization
- Stripe billing
- ClosedXML and QuestPDF for exports
- Swagger/OpenAPI

## Core Features
- Multi-project and multi-tenant access model
- Expense and income management with multi-currency support
- Obligation tracking and payment linking
- Project budgets and dashboards
- Reporting and export endpoints
- Auth flows (register, login, refresh, revoke, password reset)
- MCP endpoints protected by service token
- OCR-assisted transaction draft extraction (Azure Document Intelligence)
- Transaction reminder mode:
  - Expenses and incomes can be created as inactive reminders
  - Inactive records exist in the system but do not count in accounting totals
  - They can be activated/deactivated via dedicated active-state endpoints

## Project Structure
- Controllers: HTTP endpoints
- Services: business rules and orchestration
- Repositories: query and persistence logic
- Models + Configurations: domain and DB mapping
- DTOs: request/response contracts
- Middleware, Filters, Authorization: security and request pipeline
- Scripts: SQL migration and maintenance scripts

## Requirements
- .NET SDK 10.x
- PostgreSQL-compatible database
- Access to required environment variables

## Environment Variables
Create a .env file in the project root (or set OS-level variables). Common required values:
- DB_PASSWORD
- JWT_SECRET_KEY
- MCP_SERVICE_TOKEN
- GMAIL_USER
- GMAIL_APP_PASSWORD
- STRIPE_SECRET_KEY
- STRIPE_WEBHOOK_SECRET
- EXCHANGE_RATE_API_KEY
- AZURE_DOC_INTELLIGENCE_ENDPOINT
- AZURE_DOC_INTELLIGENCE_API_KEY

Notes:
- Program.cs loads .env manually at startup.
- appsettings.json uses placeholders like ${JWT_SECRET_KEY} and ${DB_PASSWORD}.

## Local Run
1. Restore packages
   dotnet restore ProjectLedger.API.csproj
2. Build
   dotnet build ProjectLedger.API.csproj
3. Run API
   dotnet run --project ProjectLedger.API.csproj

Swagger is enabled in Development environment.

## Build And Publish
- Build release:
  dotnet build project-ledger-api.sln --configuration Release
- Publish:
  dotnet publish ProjectLedger.API.csproj -c Release -o ./publish

## Database Scripts
SQL scripts are under Scripts.
Apply new scripts in order during deployments.

Recent script for reminder state:
- Scripts/20260313_add_transaction_active_state.pgsql

## CI/CD
A GitHub Actions workflow exists at:
- .github/workflows/main_pledger-api.yml

It builds and publishes the API, then deploys to Azure Web App.

## API Documentation
- Swagger UI (Development)
- ProjectLedger.API.http for local request examples
- Docs/README-MCP.md for MCP-specific API details

### Active-State Endpoints
- PATCH /api/projects/{projectId}/expenses/{expenseId}/active-state
- PATCH /api/projects/{projectId}/incomes/{incomeId}/active-state

Body:
- { "isActive": true } to activate and include in accounting totals
- { "isActive": false } to keep as reminder and exclude from totals

Activation rule:
- If isActive=true, the API validates minimum accounting readiness (amounts, currency, title, date, and related accounting constraints).
- If required accounting data is invalid or missing, activation is rejected.

## Notes
- Soft-delete is used on core entities.
- Role/policy checks and project access validation are enforced per request.
- Audit logs are used for key business actions.

## Suggested Next Improvements
1. Add migration runner automation in CI/CD to enforce script execution order.
2. Add idempotency support for high-risk write operations (billing and financial transaction creation).
3. Add optimistic concurrency tokens to protect against conflicting updates.
