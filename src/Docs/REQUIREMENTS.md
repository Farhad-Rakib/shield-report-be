# ShieldReport — Requirements Status & Open Items

Tracks confirmed-done items vs. genuinely remaining work, verified directly against the codebase (not assumed). The original planning docs referenced in code comments (`TASK-GROUPS-*.md`, `BUSINESS-FLOW-*.md`, `API-DESIGN-*.md`) were never committed to this repo — this file is the in-repo substitute going forward.

## Confirmed already implemented (do not re-build)

- **Pentester assignment to an engagement** — `Engagement.LeadPentesterId` + `EngagementAssignee` collection; `EngagementService.AssignUserAsync` / `RemoveAssigneeAsync`; `POST/DELETE /api/v1/engagements/{id}/assignees`.
- **Pentester recommendation on a finding** — `Vulnerability.Recommendation` field, settable via `CreateVulnerabilityRequestDto`/`UpdateVulnerabilityRequestDto`.

## Confirmed remaining gaps

| Gap | Evidence |
|---|---|
| Audit trail not exposed | `AuditLog` entity + `AuditLogConfiguration` + `DbSet` exist in `ApplicationDbContext`, but no Application service writes entries and no controller reads them — currently dead code. |
| No report/export generation | `Vulnerability.ReportIncluded` flag exists to mark findings for a report, but no generation/export endpoint exists anywhere. |
| No SLA tracking | No fields on `Engagement`/`Vulnerability` for response or remediation SLAs. |
| No scheduling beyond dates | `Engagement` has `StartDate`/`EndDate` only — no calendar or conflict logic. |
| Tool Configuration | See requirement below — net-new. |

Billing/invoicing was checked and found absent — likely out of scope for a pentest-ops tool rather than a gap; confirm before treating it as a requirement.

## New Requirement: Tool Configuration

> Tracked as tasks **#117-121** in `../../TASK-GROUPS-MASTER.md` under "Feature: Scan Tool Configuration".

### Problem
Scan tool invocation (image/host + command/args) is hardcoded in `DockerScanRunner.BuildToolInvocation` ([ShieldReport.Scanning/Runners/DockerScanRunner.cs](../ShieldReport.Scanning/Runners/DockerScanRunner.cs)). Naabu/Nuclei/Reconftw currently only run via a local `docker run`. Going forward, scan tools are expected to be hosted remotely, so the platform needs an admin-configurable record per tool instead of a hardcoded switch.

### Proposed shape
A new per-tool configuration record (`ScanToolConfiguration` or similar), one row per `ScanTool` enum value:

- `ScanTool Tool` — Naabu / Nuclei / Reconftw (unique)
- `string Url` — where the tool/host is reachable (remote Docker host endpoint, or a remote runner API — see open question below)
- `string CommandTemplate` — invocation/args template (placeholders for target, output path, etc.), replacing the hardcoded switch in `DockerScanRunner`
- `string? AuthToken` / credential field — whatever the remote endpoint needs to authenticate
- `int TimeoutSeconds`
- `bool IsEnabled`
- Audit fields: `CreatedAt`, `UpdatedAt`, `UpdatedByUserId`

CRUD via a new Application module (e.g. `ShieldReport.Application/ToolConfigurations/`) + controller, gated behind a new permission (e.g. `scans.tools.manage`) — almost certainly SuperAdmin/Admin only, not Pentester.

### Open questions (resolve before implementation)
1. **What does "hosted somewhere" mean mechanically?** A remote Docker daemon (`DOCKER_HOST`-style endpoint) that `DockerScanRunner` connects to instead of local Docker, or a remote HTTP scanning API per tool (trigger + poll/webhook for results)? This changes whether `ShieldReport.Scanning`'s runner stays Docker-CLI-based or becomes an HTTP client.
2. **Scope: global or per-client/per-engagement?** One configuration per tool platform-wide, or does each client engagement potentially point at different scanning infrastructure?
3. **Who can edit it?** SuperAdmin only, or also Admin.
4. **Does a config change affect in-flight/queued scans**, or only scans queued after the change?
