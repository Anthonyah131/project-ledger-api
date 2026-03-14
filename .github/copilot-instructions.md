# Project Guidelines

## Code Style
- Use C# with nullable reference types enabled and implicit usings enabled.
- Keep Spanish domain comments when they add business context, but keep API surface naming in English for consistency.
- Preserve the existing entity prefix convention in models and DB fields.
- Prefer extension-based mappings in Extensions/Mappings for DTO <-> Entity conversion.

## Architecture
- Layering is Controllers -> Services -> Repositories -> EF Core DbContext.
- Controllers handle HTTP concerns, auth policies, and route/body ownership boundaries.
- Services contain business rules, limits, and domain validation.
- Repositories handle query shape, includes, filtering, paging, and sorting.
- Entity configurations in Configurations define snake_case table/column mapping.

## Security And Multi-Tenancy
- Never trust project/user identifiers from request body.
- Project IDs come from route parameters.
- User identity comes from JWT claims via ClaimsPrincipalExtensions.
- Enforce project membership and role checks through Project policies and IProjectAccessService.
- Keep Admin isolation and Active-user write filters enabled.

## Build And Validation
- Restore: dotnet restore ProjectLedger.API.csproj
- Build: dotnet build ProjectLedger.API.csproj
- Run: dotnet run --project ProjectLedger.API.csproj
- Publish: dotnet publish ProjectLedger.API.csproj -c Release -o ./publish
- If build artifacts are locked by a running process, use: dotnet build ProjectLedger.API.csproj -o .\\artifacts\\tmpbuild

## Database And Scripts
- Use SQL migration scripts under Scripts instead of relying on EF migration snapshots.
- Keep model, configuration, DTO mapping, and script changes in sync when adding fields.
- Prefer additive, backward-compatible schema changes (new nullable columns or columns with defaults).

## Conventions That Matter
- Soft delete is the default deletion strategy for business entities.
- includeDeleted access should remain restricted to editor-level access.
- Keep audit logging for create/update/delete and relevant association events.
- For financial totals, ensure reminder/inactive transactions do not affect accounting totals.

## Key Files
- Program.cs
- Data/AppDbContext.cs
- Extensions/SecurityExtensions.cs
- Extensions/ServiceCollectionExtensions.cs
- Configurations/ExpenseConfiguration.cs
- Configurations/IncomeConfiguration.cs
- Services/ExpenseService.cs
- Services/IncomeService.cs
- Repositories/ExpenseRepository.cs
- Repositories/IncomeRepository.cs
