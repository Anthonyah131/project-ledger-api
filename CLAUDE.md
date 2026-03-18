# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

No test project exists. Swagger UI is available at runtime in the Development environment.

---

## Architecture

**Stack:** .NET 10, ASP.NET Core Web API, EF Core 10, PostgreSQL/CockroachDB via Npgsql, JWT auth, Stripe billing.

**Layering (strict, top-down):**
```
Controllers → Services → Repositories → AppDbContext
```

- **Controllers** — HTTP concerns only: routing, JWT extraction, model state, calling services, returning results. No business logic.
- **Services** — Business rules, plan limits, domain validation, orchestration. All writes go through services.
- **Repositories** — Query shape, `Include`s, filtering, paging, sorting. Override `GetByIdAsync` when navigation properties need to be included (base uses `FindAsync` with no includes).
- **DTOs** — Separate request/response contracts per domain in `DTOs/{Domain}/`. Never expose model entities directly.
- **Mappings** — Extension methods in `Extensions/Mappings/`. No AutoMapper. Pattern: `entity.ToResponse()`, `request.ToEntity(userId)`, `entity.ApplyUpdate(request)`.
- **Configurations** — EF Core fluent config in `Configurations/`. Defines table names, column names, indexes, FK behavior.

**Registrations** — All repos and services are registered in `Extensions/ServiceCollectionExtensions.cs`. Add new ones there when creating a new domain.

---

## Key Conventions

### Entity field prefix
Every model uses a short prefix on all properties. Examples: `PmtId`, `PmtName` for `PaymentMethod`; `PtrId`, `PtrName` for `Partner`; `PrjId` for `Project`. Check the existing model to find the prefix before adding new fields.

### Soft delete
All business entities use soft delete (`IsDeleted`, `DeletedAt`, `DeletedByUserId`). Hard deletes are only used for pure join tables (e.g. `ProjectPaymentMethod`). Always filter `!IsDeleted` in repository queries. In `GetByIdAsync` overrides, filter by ID **and** `!IsDeleted`.

### User identity
`userId` is **always** extracted from the JWT via `User.GetRequiredUserId()` in the controller. Never read it from the request body. Project IDs always come from route parameters.

### Multi-tenancy and access control
- `IProjectAccessService.ValidateAccessAsync(userId, projectId, role, ct)` — throws if no access (catches a `ForbiddenAccessException` in middleware).
- `IPlanAuthorizationService` — gates feature permissions and enforces plan limits (max projects, max payment methods, etc.).
- Role hierarchy: `owner > editor > viewer`. Use the minimum role required.

### Partner → PaymentMethod → Project relationship
- A `Partner` owns zero or more `PaymentMethod`s via `PmtOwnerPartnerId`.
- A `PaymentMethod` can be linked to a `Project` via the `ProjectPaymentMethod` join table.
- If a payment method has a partner, that partner must be assigned to the project first (`ProjectPartner`) before the payment method can be used in the project. Payment methods **without** a partner can be linked to any project freely.

### Workspace → Project relationship
- A `Project` belongs to at most one `Workspace` via `PrjWorkspaceId` (nullable).
- Workspace membership is separate from project membership. Sharing a workspace does not share its projects — projects must be shared individually.

### Inactive/reminder transactions
Expenses and incomes can be `IsActive = false` (reminder mode). Inactive records exist in the DB but **do not count** in accounting totals, balances, or budget calculations. Always filter `&& isActive` when computing financial aggregates.

### Database transactions
Multi-step write operations (entity + splits, entity + currency exchanges, password reset + token revocation) **must** be wrapped in an explicit transaction via `ExecuteInTransactionAsync`. This method uses `CreateExecutionStrategy()` internally, which is required because `NpgsqlRetryingExecutionStrategy` (enabled via `EnableRetryOnFailure`) does **not** allow direct `BeginTransactionAsync`.

**Pattern:**
```csharp
await _repo.ExecuteInTransactionAsync(async (ct) =>
{
    await _repo.AddAsync(entity, ct);
    await _repo.SaveChangesAsync(ct);
    // … additional writes …
}, ct);
```

If an exception occurs before commit, the transaction rolls back automatically. Single `SaveChangesAsync` calls do not need explicit transactions — EF Core handles those atomically.

---

## Database

SQL migration scripts live in `Scripts/`. EF Core migration snapshots are **not** used for production changes — write a `.pgsql` script instead. Naming convention: `YYYYMMDD_description.pgsql`.

When adding a new field: update the **model**, **EF configuration**, **DTO**, **mapping extension**, and **SQL script** together.

Prefer additive, backward-compatible schema changes: new nullable columns or columns with defaults.

The connection string in `appsettings.json` uses `${DB_PASSWORD}` as a placeholder, resolved at startup from the `DB_PASSWORD` environment variable. All other secrets follow the same pattern — see `README.md` for the full list.

---

## Cross-cutting Services Worth Knowing

| Service | Purpose |
|---------|---------|
| `IProjectAccessService` | Validates and reads user role on a project |
| `IPlanAuthorizationService` | Checks plan permissions and enforces entity count limits |
| `ITransactionReferenceGuardService` | Prevents deletion of payment methods that have transaction history |
| `IAuditLogService` | Log create/update/delete events for auditable entities |
| `IReportExportService` | PDF and Excel exports (split into partial classes under `Services/Report/`) |
| `IMcpService` | Read-only model endpoints for AI assistant integration, protected by `MCP_SERVICE_TOKEN` |
