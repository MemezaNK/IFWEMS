# IFWEMS — Irregular, Fruitless & Wasteful Expenditure Management and Prevention System

Government Department of Health. See [Docs/IFWEMS_Software_Requirements_Specification.md](Docs/IFWEMS_Software_Requirements_Specification.md) for the full SRS.

## Stack
- **Backend**: .NET 8 / ASP.NET Core Web API, Clean Architecture (Domain / Application / Infrastructure / Api)
- **Database**: Microsoft SQL Server, EF Core (code-first migrations)
- **Auth**: JWT bearer tokens issued by the native `/api/auth/login` endpoint
- **Frontend**: Angular (planned — see `src/IFWEMS.Client`, pending Node.js 18+ upgrade)

## Solution structure
```
src/
  IFWEMS.Domain/          Entities, enums (Case, User, Role, OrgUnit, AuditLog, ...)
  IFWEMS.Application/     DTOs, service interfaces
  IFWEMS.Infrastructure/  EF Core DbContext, migrations, auth, notifications, audit logging
  IFWEMS.Api/             ASP.NET Core Web API host, controllers
  IFWEMS.Client/          Angular web app (to be scaffolded)
tests/
  IFWEMS.Domain.Tests/
  IFWEMS.Application.Tests/
  IFWEMS.Api.Tests/
db/
  seed/                   Standalone SQL seed scripts (lookup data + high-volume test data)
```

## Getting started (backend)

1. Update the `ConnectionStrings:DefaultConnection` and `Jwt:Key` values in
   [src/IFWEMS.Api/appsettings.Development.json](src/IFWEMS.Api/appsettings.Development.json)
   (or better, via `dotnet user-secrets`) — never commit real secrets.
2. Apply database migrations:
   ```powershell
   dotnet ef database update --project src/IFWEMS.Infrastructure --startup-project src/IFWEMS.Api
   ```
3. Run the seed scripts against the target database (in order):
   ```
   db/seed/01_lookup_data.sql
   db/seed/02_test_data.sql   (dev/SIT/UAT only — do not run in production)
   ```
4. Run the API:
   ```powershell
   dotnet run --project src/IFWEMS.Api
   ```
5. Browse Swagger UI at `https://localhost:<port>/swagger`.

## Running tests
```powershell
dotnet test IFWEMS.sln
```

## Scope of this initial scaffold
Included: solution/project structure, core domain entities, EF Core schema + migration,
native user/role/org-unit administration (FR-001/002/004), JWT authentication, in-app
notification engine skeleton (FR-005/006), append-only audit logging (FR-024/051), health
checks, Swagger, Serilog logging, and a CI pipeline skeleton.

Not yet implemented (future work): case management workflow (FR-020–026), compliance rule
engine (FR-010–014), contracts/suppliers modules (FR-030–032), reporting/dashboards (FR-042),
departmental system integrations (FR-050), and the Angular client (pending a Node.js 18+
upgrade on the development machine).
