# Security Review — shield-report-be

Findings from a manual code review + `dotnet list package --vulnerable` scan. None of these have been fixed yet — this is a record of what was found. Severity follows OWASP-style impact x likelihood, not a formal CVSS score.

## Summary

| # | Severity | Finding | Status |
|---|----------|---------|--------|
| 1 | **Critical** | Open self-registration lets anyone become SuperAdmin/Admin | Not fixed |
| 2 | **Critical** | Unauthenticated `change-password` endpoint trusts a client-supplied user ID | Not fixed |
| 3 | High | No rate limiting on any auth endpoint | Not fixed |
| 4 | High | No account lockout after repeated failed logins | Not fixed |
| 5 | High | Vulnerable transitive packages: `Microsoft.Extensions.Caching.Memory` 8.0.0, `System.Formats.Asn1` 5.0.0 | Not fixed |
| 6 | Moderate | Vulnerable transitive packages: `Azure.Identity` 1.10.3, `Microsoft.Identity.Client` 4.56.0 | Not fixed |
| 7 | Medium | Default seeded SuperAdmin credential never forced to change | Not fixed |
| 8 | Low | `TrustServerCertificate=True` in the SQL connection string | Not fixed (dev-only as configured) |
| 9 | Low | Permission/role revocations don't take effect until the access token expires (≤60 min) | Not fixed (by design, no token blacklist) |
| 10 | Info | No HSTS configured | Not fixed |

---

## 1. Critical — Privilege escalation via open registration
**Where:** `ShieldReport.Api/Controllers/AuthController.cs` (`POST /api/v1/auth/register`, `[AllowAnonymous]`) → `ShieldReport.Application/Auth/AuthService.cs` `RegisterAsync` → `ShieldReport.Application/Auth/Dtos/RegisterUserRequestDto.cs`.

`RegisterUserRequestDto` includes a client-supplied `Roles: IReadOnlyList<string>` field. `RegisterAsync` resolves those role names via `_roleRepository.GetByNamesAsync(request.Roles, ...)` and assigns them directly to the new user — **with no check that the caller is authorized to grant those roles**, because the endpoint itself requires no authentication at all.

**Impact:** Any unauthenticated attacker can run:
```
POST /api/v1/auth/register
{ "fullName": "x", "email": "x@x.com", "password": "...", "roles": ["SuperAdmin"] }
```
and get a fully privileged SuperAdmin account.

**Fix:** Either (a) remove `Roles` from the public registration DTO entirely and always assign the default `User` role server-side, or (b) keep self-service registration role-less and move privileged user creation behind an authenticated, permission-gated endpoint (e.g. reuse `UsersController`'s role-assignment endpoints, which are already correctly gated by `Permissions.UserRolesCreate`).

## 2. Critical — Unauthenticated password change with attacker-supplied target user ID
**Where:** `ShieldReport.Api/Controllers/AuthController.cs` lines 111–119 (`POST /api/v1/auth/change-password`).

Unlike every other action in this controller, `ChangePassword` has neither `[Authorize]` nor `[AllowAnonymous]` — and there is no global fallback authorization policy configured in `Program.cs` (`AddAuthorization` only registers per-permission policies, no `FallbackPolicy`). In ASP.NET Core, an action with no `[Authorize]` attribute and no fallback policy is **public by default**. `ChangePasswordRequestDto` carries `UserId` directly from the request body (see `AuthService.ChangePasswordAsync`, `ShieldReport.Application/Auth/AuthService.cs:226`), instead of being derived from the caller's JWT (`sub` claim), the way `UsersController`/`DashboardLayoutController` do it.

**Impact:** Combined with finding #3 (no rate limiting) and #4 (no lockout), this endpoint is a fully unauthenticated online password-guessing oracle against **any** user ID — an attacker doesn't even need a valid session.

**Fix:** Add `[Authorize]` to the action (or the controller, with `[AllowAnonymous]` on the public actions as already done for login/register/refresh/forgot/reset). Derive the target user ID from `User.FindFirst("sub")` like the other controllers do, and drop `UserId` from `ChangePasswordRequestDto`.

## 3. High — No rate limiting on auth endpoints
**Where:** `ShieldReport.Api/Program.cs` — no `AddRateLimiter`/`UseRateLimiter` anywhere.

`login`, `change-password`, `forgot-password`, `reset-password`, and `refresh` all accept unlimited requests per IP/account. Combined with #1/#2 this makes credential stuffing, password spraying, and the change-password oracle practical at scale.

**Fix:** Add ASP.NET Core's built-in rate limiter (`Microsoft.AspNetCore.RateLimiting`, available in .NET 8) with a fixed-window or token-bucket policy scoped to the auth endpoints at minimum.

## 4. High — No account lockout
**Where:** `ShieldReport.Application/Auth/AuthService.cs:53` (`LoginAsync`).

Failed login attempts are not counted or throttled per-account; only the generic "Invalid credentials." error is returned (good — no user enumeration there), but there's nothing stopping unlimited retries.

**Fix:** Track failed attempts (e.g. a counter + timestamp on `User`, or a cache entry) and lock the account or add exponential backoff after N consecutive failures.

## 5. High — Vulnerable transitive NuGet packages (DoS)
**Where:** Pulled in transitively via `Microsoft.EntityFrameworkCore` 8.0.8 and `Microsoft.Data.SqlClient` 5.1.5 (used by `ShieldReport.Persistence`/`ShieldReport.Api`).

| Package | Resolved | Fixed in | Advisory |
|---|---|---|---|
| `Microsoft.Extensions.Caching.Memory` | 8.0.0 | 8.0.1 | [GHSA-qj66-m88j-hmgj](https://github.com/advisories/GHSA-qj66-m88j-hmgj) |
| `System.Formats.Asn1` | 5.0.0 | 8.0.1+ (use 8.0.2) | [GHSA-447r-wph3-92pm](https://github.com/advisories/GHSA-447r-wph3-92pm) |

**Fix:** Add explicit `<PackageReference>` pins for these two packages at the fixed versions in `ShieldReport.Persistence.csproj` (NuGet's nearest-wins resolution will then use the patched version instead of the transitive one). No code changes needed — neither package is used directly.

## 6. Moderate — Vulnerable transitive NuGet packages (auth library)
**Where:** Pulled in transitively via `Microsoft.Data.SqlClient` 5.1.5 → `Azure.Identity` 1.10.3 → `Microsoft.Identity.Client` 4.56.0.

| Package | Resolved | Recommended | Advisory |
|---|---|---|---|
| `Azure.Identity` | 1.10.3 | 1.14.2+ | [GHSA-wvxc-855f-jvrv](https://github.com/advisories/GHSA-wvxc-855f-jvrv), [GHSA-m5vv-6r4h-3vj9](https://github.com/advisories/GHSA-m5vv-6r4h-3vj9) |
| `Microsoft.Identity.Client` | 4.56.0 | 4.66.2+ | [GHSA-x674-v45j-fwxw](https://github.com/advisories/GHSA-x674-v45j-fwxw), [GHSA-m5vv-6r4h-3vj9](https://github.com/advisories/GHSA-m5vv-6r4h-3vj9) |

These libraries are only relevant if/when Azure AD-based SQL authentication is ever used; the app currently uses SQL `Trusted_Connection`/SQL auth, not Azure AD, so practical exposure today is low — but they ship in the deployed binaries regardless. Same fix approach as #5: explicit pinned `<PackageReference>` entries.

## 7. Medium — Default credential never forced to rotate
**Where:** `ShieldReport.Persistence/Seeding/DefaultUserSeeder.cs` — seeds `superadmin@localhost` / `SuperAdmin@123!` on first run, documented in this repo's `CLAUDE.md`.

There is no `MustChangePassword` flag or first-login forced reset. If a deployment is stood up and this credential isn't rotated immediately, it's a published, guessable SuperAdmin login.

**Fix:** Add a `MustChangePassword` flag on `User`, set it `true` for the seeded admin, and enforce a password change before issuing a usable access token while it's set.

## 8. Low — `TrustServerCertificate=True`
**Where:** `appsettings.json` → `ConnectionStrings:SqlServerConnection`, and `ApplicationDbContextFactory.cs`.

This disables TLS certificate validation for the SQL Server connection — fine for a local/dev instance without a trusted cert, but if this connection string is reused as-is against a production SQL Server over an untrusted network, it permits a MITM on the DB connection.

**Fix:** For non-local environments, use a connection string with a properly trusted certificate and `TrustServerCertificate=False` (or `Encrypt=True;TrustServerCertificate=False` with a real cert chain).

## 9. Low — Stale permissions in already-issued tokens
**Where:** `ShieldReport.Infrastructure/Authentication/JwtTokenGenerator.cs` — permissions are baked into the JWT at login/refresh time; there's no server-side revocation list for access tokens (only refresh tokens can be revoked, via `revoke-refresh`).

If an admin revokes a user's role/permission, that user's already-issued access token remains valid with the old permissions for up to `Jwt:ExpiryMinutes` (currently 60 minutes).

**Fix (optional, tradeoff-dependent):** Shorten `ExpiryMinutes`, or check permissions against the DB/cache instead of (or in addition to) the JWT claim for highly sensitive actions.

## 10. Info — No HSTS
**Where:** `Program.cs` — `UseHttpsRedirection()` is present, `UseHsts()` is not.

Low-impact hardening gap; add `app.UseHsts()` in non-development environments.

---

## Dependency scan command used
```bash
dotnet list package --vulnerable --include-transitive --source https://api.nuget.org/v3/index.json
```
(The default configured NuGet sources include private GitHub Packages feeds that require auth not available in this environment — re-run without `--source` once those credentials are available, to also confirm no first-party package advisories were missed.)
