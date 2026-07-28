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

## New Requirement: LLM-Assisted Vulnerability Explanation & Scoring (implemented)

> Tracked as tasks **#122-127** in `../../TASK-GROUPS-MASTER.md` under "Feature: LLM-Assisted Vulnerability Explanation". Fully implemented — kept here for reference since, like Tool Configuration, no companion planning doc covered it before this pass.

Clients must not see which underlying scan tool (Naabu/Nuclei/Reconftw) produced a finding — the FE now renders generic labels ("Port Checking"/"Vulnerability Scanning"/"Reconnaissance") everywhere a tool name could leak, including the previously-public `/login` page's terminal demo (`SecurityShowcasePanel.tsx`, which hardcoded literal `naabu`/`nuclei` commands outside any auth guard). `Vulnerability.ToolsUsed` and the report's per-finding "Tools Used" cell (see mapping below) are unaffected — that's an internal/report-authoring detail, not the live-console/tool-picker labels this covers.

Separately, `LlmProviderConfiguration` (one row per `OpenRouter`/`Gemini`/`OpenAi`, single `IsActive` row enforced app-side, API key encrypted via `IDataProtector`) lets an admin wire up an LLM. Once active, every new/updated finding from `ScanWorkerBackgroundService` enqueues a Hangfire `LlmExplanationJob` that renders the active configuration's saved `PromptTemplate` (placeholders: `{ToolName}`/`{FindingTitle}`/`{Endpoint}`/`{RawFinding}`/`{CvssScore}`, strict-JSON-only output contract to bound tokens and curb hallucination) and calls the provider (`OpenAiCompatibleLlmClient` shared by OpenAI/OpenRouter since they're wire-compatible, `GeminiLlmClient` separately). The result populates `Vulnerability.AiExplanation`/`AiSuggestedSeverity` — the suggested severity is never auto-applied; a pentester must call `POST /vulnerabilities/{id}/ai-severity/apply` to write it into the existing `SeverityOverride`, matching the human-in-the-loop pattern already used for CVSS overrides.

## New Requirement: White Box Testing Report Content Mapping

> Extends the already-planned **Feature: Reporting** (tasks #59-68, 83, 92 — `Report`/`ReportTemplate` entities, `QuestPDF` generation, approval gate, send-to-client) rather than replacing it. That feature is speced generically ("branding fields" on `ReportTemplate`); this section supplies the concrete field-by-field mapping from a real Selise deliverable (`White Box Testing.docx`, workspace root) so #59/#62/#63 can be scoped precisely instead of just "a branded PDF." **Output format is already decided by #63 as PDF via QuestPDF — this is not a case for docx-template filling (e.g. OpenXML), despite the source document being a .docx.**

### Section-by-section mapping

| Report section (from the template) | Source data |
|---|---|
| Cover (client name, engagement dates) | `Engagement` (client via `ClientOrganization`), `StartDate`/`EndDate` |
| Version control table | New `Report.Version` (already in #62's spec) + `ApprovedByUserId`/`ApprovedAt` |
| Acronyms table | Static boilerplate — belongs on `ReportTemplate`, not per-report data |
| Executive Summary | Narrative prose — **no existing field holds this** (see open question below) |
| Summary of Findings (counts by severity + table) | Aggregated from `Vulnerability` rows filtered by `EngagementId` + `ReportIncluded`/severity filter (#62's JSON filter) |
| Key Findings / Key Recommendations (bullets) | Narrative — same gap as Executive Summary; candidate: derive from `Vulnerability.AiExplanation` across included findings, or a new editable field |
| Project Overview / Scope of Engagement table | `Engagement` scope fields + `ClientAsset` rows in scope |
| Introduction / Methodology / Tools Used for VAPT / Security Testing Components | Static boilerplate — belongs on `ReportTemplate` (per-template, not per-report) |
| Limitations | Static boilerplate — `ReportTemplate` |
| Discovered Vulnerabilities table | `Vulnerability` rows: Title, Severity/`SeverityOverride`, `ClientAsset`, `PatchStatus` |
| Detailed Findings (per finding: ID, Severity, CVSS, Affected System, Endpoint, **Tools Used**, References, Description, Impact, Recommendation, PoC, Steps to Reproduce) | `Vulnerability.Title`/`Severity`/`CvssScore`/`CvssVector`/`Endpoint`/`ClientAsset`/**`ToolsUsed`** (real tool names — this field is not subject to the client-masking above)/`ReferenceLinks`/`Description`/`Impact`/`Recommendation` (#31a)/`ProofOfConcept`/`StepsToReproduce`. **Should also surface `Vulnerability.AiExplanation`** when present, alongside `Recommendation`, the same way #31a's note ties `Recommendation` into #63 |
| Annexure — CVSS Scoring table | Static boilerplate — `ReportTemplate` |

### Open questions (resolve before starting #59/#62/#63 with this mapping in hand)
1. **Where do the narrative-only sections (Executive Summary, Key Findings, Key Recommendations) come from?** Three options: (a) new free-text fields on `Report` that a pentester authors by hand before approval, (b) auto-generated by summarizing the included findings' `AiExplanation` values through the same active LLM provider, or (c) both — LLM-drafted, pentester-editable before approval. This is a real scope decision for #62, not an implementation detail.
2. **Is `ReportTemplate` one row per client/engagement-type, or one global default?** The static boilerplate sections above (Acronyms, Methodology, Security Testing Components, Limitations, CVSS Annexure) rarely change between engagements — confirm whether `ReportTemplate` needs to support multiple variants or just one, before #59's schema is finalized.
3. **Does the per-finding "Tools Used" cell stay as real tool names indefinitely**, or does the client-facing masking requirement above eventually need to extend to the report too? Out of scope for this pass — flagged since it directly contradicts the template's own boilerplate (which names Nuclei/Nikto/Sqlmap/Nmap/etc. as SELISE's methodology) if masking were ever extended here.
