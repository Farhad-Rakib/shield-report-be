# Shield Report Backend (shield-report-be)

## Project Overview
A .NET 8 Web API generated from the local `olympuscore` Clean Architecture template, providing Authentication, RBAC (Roles/Permissions/RolePermission/UserRole), Menu, SiteSettings, Notifications, and a customizable per-user Dashboard. Backed by SQL Server via EF Core.

## Architecture
Clean layered architecture:
- **ShieldReport.Domain** — Entities, enums, value objects (no dependencies)
- **ShieldReport.Application** — Business logic, service interfaces, DTOs, FluentValidation
- **ShieldReport.Infrastructure** — JWT, password hashing, SMTP email, Serilog logging
- **ShieldReport.Scanning** — Scan execution pipeline: Hangfire dispatch (`HangfireScanDispatcher`/`ScanDispatchJob`), per-`ScanTool` work queues (`PerToolScanWorkQueue`), `ScanWorkerBackgroundService`, `DockerScanRunner` (shells out to `docker run` for Naabu/Nuclei/Reconftw), and the tool-specific output parsers. References Application + Domain only — not Infrastructure/Persistence/Api.
- **ShieldReport.Persistence** — EF Core (SQL Server), repositories, migrations, seeders
- **ShieldReport.Api** — Controllers, middleware, authorization handlers, startup

## Build & Run

```bash
# From the src/ directory
dotnet build ShieldReport.sln
dotnet run --project ShieldReport.Api
```

API runs at: `https://localhost:5001` / `http://localhost:5000`
Swagger UI: `https://localhost:5001/swagger` (Development only)

## Database

- Provider: SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`)
- Default DB name: `ShieldReportDb`
- Connection string in `src/ShieldReport.Api/appsettings.json` → `ConnectionStrings:SqlServerConnection`
- `Database:Provider` must stay `sqlserver` — `ShieldReport.Persistence.DependencyInjection` and the design-time `ApplicationDbContextFactory` both only support `sqlserver` in this project (the Postgres branch from the upstream template was intentionally not wired up).

```bash
# Add a new migration (run from src/)
dotnet ef migrations add <MigrationName> --project ShieldReport.Persistence --startup-project ShieldReport.Api

# Apply migrations (touches your real SQL Server — review the connection string first)
dotnet ef database update --project ShieldReport.Persistence --startup-project ShieldReport.Api
```

## Key Configuration (`appsettings.json`)

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:SqlServerConnection` | SQL Server connection string |
| `Jwt:SecretKey` | JWT signing key — change in production |
| `Jwt:Issuer` / `Jwt:Audience` | Token issuer/audience validation |
| `Smtp:*` | SMTP email for password reset emails |
| `Serilog:*` | Structured logging (console + file + optional Seq, disabled by default) |
| `Cors` (`AllowFrontend` policy in `Program.cs`) | Allowed frontend origins |
| `Caching:*TtlMinutes` | TTLs for the permission/menu/role caches (always in-memory — see below) |

## Caching
Caching is in-memory only (`AddDistributedMemoryCache`) — Redis support was removed. Permission/menu/role caching goes through the generic `IAppCache`/`IPermissionCache` abstractions in `ShieldReport.Api/Startup/Distributed*Cache.cs`, so swapping the backend later just means changing the `AddDistributedMemoryCache()` call in `Program.cs`. Cache admin endpoints: `GET /api/v1/system/cache/distributed/keys`, `DELETE /api/v1/system/cache/distributed/flush`.

## Domain Entities
- `User`, `UserRole` — users with role assignments
- `Role`, `RolePermission` — roles with permission assignments
- `Permission` — named permission constants
- `Menu` — dynamic sidebar menu with parent/child hierarchy and required permission
- `SiteSetting` — global key/value app settings (incl. color palettes)
- `Notification` — in-app/SignalR notifications
- `UserDashboardPreference` — per-user dashboard widget order + hidden widgets (JSON)
- `RefreshToken`, `PasswordResetToken` — auth token lifecycle

## Permissions
All permission constants are in `ShieldReport.Application/Security/Permissions.cs`.
Policies are auto-registered from `Permissions.All` in `Program.cs` — adding a new constant there is enough to get a matching `[Authorize(Policy = ...)]` policy; no extra wiring needed.

## RBAC
- **SuperAdmin** — bypasses all permission checks at runtime (handled in `PermissionAuthorizationHandler`)
- **Admin** — granted most permissions except delete operations on core entities
- **User** — granted `users.read` and `dashboard.read` by default
- Seeded automatically on startup via `RbacSeeder`

## Default Seeded Data
On first run, the bootstrapper seeds:
1. Roles: SuperAdmin, Admin, User
2. All permissions from `Permissions.All`
3. Role-permission assignments for Admin and User
4. Default super admin user — `superadmin@localhost` / `SuperAdmin@123!` (see `DefaultUserSeeder.cs`)
5. Default menu structure (`DefaultMenuSeeder.cs`), including a permission-gated "Dashboard" entry

## Dashboard module
Ported from `ims_be`'s dashboard concept but re-themed generically (no inventory data), then extended with PentestOps widgets alongside the original RBAC-admin ones:
- `DashboardController` (`GET /api/v1/dashboard`) returns `DashboardSummaryDto`: original RBAC-admin fields (total/active users, total roles, total permissions, users-by-role, a 6-month registration trend, most recently registered users) **plus** PentestOps fields (`TotalClients`, `TotalEngagements`, `TotalAssets`, `OpenVulnerabilities`, `CriticalVulnerabilities`, `ActiveScans`, `VulnerabilitiesBySeverity`, `EngagementsByStatus`, `RecentActivity`), filterable by `clientOrganizationId`/`from`/`to`.
- `DashboardLayoutController` (`GET`/`PUT /api/v1/dashboardlayout`) persists each user's widget order + hidden widgets (`UserDashboardPreference`) so the frontend's drag-and-drop layout survives across sessions/devices.
- Both are gated behind the single `dashboard.read` permission (no per-widget permissions, unlike ims_be).
- **This is not the PentestOps "Risk Dashboard"** (`TASK-GROUPS-MASTER.md` tasks #69-70/91/97) — that's a separate, still-unbuilt feature (risk score, patch rate, avg time-to-fix, top-5-open, trend-over-time, dedicated `RiskDashboardRead` permission for section-level gating). Don't assume the fields above satisfy it.

## Known gaps
- **Audit logging is not wired up**: `AuditLog` entity/migration (`../TASK-GROUPS-MASTER.md` #9) has existed since the foundation phase, but no controller/service anywhere calls into it — confirmed via a full `Application`/`Api` grep for `AuditLog` returning zero hits. Every mutating endpoint's audit trail is a gap (task #73). Don't assume writes are being logged just because the table exists.
- **`NotificationPreference` has no API**: the entity/table exists (task #11) but there's no service/controller for it yet (task #72) — the frontend's `NotificationSettingsPage` is a local-only stub with no backend to call.

## Task Tracking / Resuming Work
- Master status tracker for the whole PentestOps build-out (all features, Backend/Frontend, Done/Not-Started): `../TASK-GROUPS-MASTER.md` (one level up from this repo, in the `Selise Projects` workspace root). Companion detail docs (`TASK-GROUPS-PentestOps.md`, `TASK-GROUPS-AutomatedScanning.md`, `TASK-GROUPS-EngagementManagement.md`, `TASK-GROUPS-InvitationRegistration.md`) live alongside it but are historical — the master doc is canonical for status.
- Net-new requirements not yet in the master tracker's companion docs get written up in `src/Docs/REQUIREMENTS.md` first, then added to the master tracker as their own Feature section.
- **Next planned work**: Scan Tool Configuration (tasks #117-121 in the master tracker, spec in `src/Docs/REQUIREMENTS.md`) — make scan tool image/host/command configurable instead of hardcoded in `ShieldReport.Scanning/Runners/DockerScanRunner.cs`. Has open architectural questions (remote Docker daemon vs. remote HTTP API per tool) that need answering before starting.

## Adding New Features
1. Add entity to `ShieldReport.Domain/Entities/`
2. Add EF configuration to `ShieldReport.Persistence/Configurations/`
3. Add `DbSet<T>` to `ApplicationDbContext`
4. Add repository interface + implementation (`ShieldReport.Application/<Feature>/` + `ShieldReport.Persistence/Repositories/`)
5. Add service interface + implementation to `ShieldReport.Application/<Feature>/`
6. Register everything in the relevant `DependencyInjection.cs` (`Application` and `Persistence` projects)
7. Add controller to `ShieldReport.Api/Controllers/`
8. Add permissions to `Permissions.cs` (policies are auto-registered — see above)
9. Run `dotnet ef migrations add <Name>`
