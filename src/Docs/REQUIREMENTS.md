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

## Scan Reliability Hardening (implemented 2026-11-14)

Scans were being lost or left stuck. Hangfire only performs a one-line handoff (`ScanDispatchJob` writes the scan ID into an in-memory per-tool channel and immediately reports success); the real work runs in `ScanWorkerBackgroundService`, which had no persistence or recovery. Root causes found and fixed:

| Problem | Fix |
|---|---|
| `DockerScanRunner`'s stdout/stderr handlers are `async void`; an exception from a SignalR push would terminate the whole API process | Callbacks wrapped (`SafeOnOutputLineAsync`); pushes use `CancellationToken.None` |
| A server shutdown cancelled the run through the same path as a user Stop, so the scan was left `Running` forever | Shutdown, timeout and user Stop are tracked separately; shutdown → `Failed: Interrupted by a server restart` |
| Final status writes used the cancellation token, so a shutdown could cancel the write itself | All final writes use `CancellationToken.None` |
| Loading/starting a scan ran outside any try/catch in a fire-and-forget task | Outer guard in `ProcessScanAsync` plus `TryMarkFailedAsync` with a fresh scope |
| Fire-and-forget runs weren't awaited on shutdown | `StopAsync` awaits in-flight runs; `HostOptions.ShutdownTimeout = 40s`; compose `stop_grace_period: 50s` |
| Queued scans vanished from memory on restart; orphaned scan containers kept running on the host daemon | `ScanRecoveryHostedService` at boot: removes `shieldreport-scan-*` containers, fails scans left `Running`, re-queues `Queued` scans (chain successors waiting on a queued predecessor are left for the worker) |
| No time limit; one hung tool blocked its queue forever | Per-tool timeout (`Scanning:Tools:{Tool}:TimeoutMinutes`) |
| A heavy scan could starve SQL Server/API of memory | `--memory`, `--memory-swap`, `--cpus`, `--pids-limit` per tool; exit 137 reported as "exceeded memory limit" |
| LLM explanation jobs retried 10× for hours | `[AutomaticRetry(Attempts = 3)]` |

Stuck scans also counted against `MaxConcurrentScansPerClient = 3`, eventually locking a client out with 409s; recovery removes that failure mode. Longer-term replacement (DB-backed queue with leases, or a separate Go runner using a pull protocol) is still open.

### Lean reconftw

`--recon` ran reconftw's full pipeline (OSINT, DNS brute-force, permutations, screenshots, port scan, nuclei, JS analysis, fuzzing) while the parser only kept hostnames scraped from stdout, and every result file was deleted with the `--rm` container. Now:

- Derived image `shieldreport/reconftw-lean:latest` (`docker/reconftw/Dockerfile`, upstream pinned by digest) adds `reconftw.lean.cfg`: passive + crt + TLS subdomains, simple web probe, takeover check; brute-force/permutations/screenshots/OSINT/port scan/nuclei/fuzzing off; thread counts and DNS rate capped; resolver/tool updates disabled.
- Invocation: `-d <host> -s -f /reconftw/reconftw.lean.cfg -o /scan-output/scan-<id>`, with the `shieldreport-scan-output` volume shared with the API at `/app/scan-output`. The worker parses `subdomains/subdomains.txt` from there (falls back to stdout when the volume isn't mounted, e.g. local `dotnet run`) and deletes the folder.
- Fixed: `HOME=/tmp` was applied to reconftw too, so `tools=$HOME/Tools`, `GOPATH` and the resolver lists resolved to empty paths. Reconftw now keeps `HOME=/root`.
- Upstream only ships linux/arm64. On an amd64 server it runs under QEMU; building a native amd64 image is a follow-up.

## Scan Tool Settings (planned)

Admin page (Configuration → Scan Tools, new `ScanToolsManage` permission for SuperAdmin/Admin) replacing the hardcoded values in `DockerScanRunner` and the interim `Scanning:Tools` appsettings section. This is the local subset of tasks #117-121; remote tool hosting and per-client settings stay deferred.

- `ScanToolSetting` entity, one seeded row per `ScanTool`: `IsEnabled`, `ClientLabel` (replaces the FE's hardcoded `SCAN_TOOL_LABELS`), `Image` (pinned), `TimeoutMinutes`, `MemoryMb`, `Cpus`, `MaxParallel`, plus typed tool options:
  - Naabu: port preset (top-100 / top-1000 / full) or numeric list, rate, scan type (connect/SYN).
  - Nuclei: severities, include/exclude tags (default exclude `dos`, `intrusive`), rate limit, concurrency.
  - Reconftw: mode (`-s` / `-p` / `-r`), DNS brute-force, permutations, screenshots, takeover check, thread counts → rendered into a per-scan config file on a shared volume.
- Disabled tools are hidden from tool pickers (`GET /scan-tools/available`) and rejected by `CreateScanRequestDtoValidator`/`ScanService` with 400.
- Resolved settings are snapshotted onto the scan when queued, so changes apply only to new scans (answers open question 4 under Tool Configuration).
- "Test" action runs the tool's version command and reports image presence/version (and Nuclei template age).
- **Security rule:** no free-form command/argument field. The API holds the host Docker socket, so every option is a validated field mapped to known flags server-side.

## HTML Report Generation (planned — replaces docx filling + LibreOffice)

Templates: `White Box Testing.docx`, `Gray Box Testing.docx`, `Black Box Testing.docx` (workspace root). They are ~90% identical: Gray = White with the testing type and methodology paragraph changed; Black omits Project Overview, the VAPT introduction and Tools Used, and has shorter methodology/limitations text. (The Black template's Executive Summary wrongly says "Gray Box" — fix at source.)

- One Handlebars template (HTML + CSS) rendered server-side with Handlebars.Net; `TestingType` (White/Gray/Black) on `Engagement` drives `{{#if}}` sections. Helpers: `formatDate`, `numberWords` ("twenty-eight (28)"), `eq`, `pad`, `upper`, `severityColor`, `pageBreak`.
- Generate stores the rendered HTML snapshot on the report (what gets approved); outputs from that snapshot:
  - PDF: headless Chromium (PuppeteerSharp/Playwright) + paged.js for TOC page numbers, running headers/footers and page breaks.
  - Word: HtmlToOpenXml (MIT, OpenXML SDK) into a base `.docx` carrying the cover, header/footer, Word styles and a TOC field (`updateFields`). Same content/structure as the PDF, not pixel-identical; charts embedded as PNG.
- Gaps the removed OnlyOffice editor used to cover, now to be generated: PoC screenshots from `VulnerabilityAttachment` (new "include in report" flag + caption); severity chart from counts (**the current docx generator leaves the template's sample chart in place**); version-control table from report versions/approvals; per-report editable sections (executive summary, key findings/recommendations, project goals, limitations, scope status) pre-filled from template defaults.
- Template editing: admin Report Templates page with code editor, variable list and server-rendered preview (replaces the `.docx` `{{Token}}` upload).
- Safety: sanitize rich text before `{{{ }}}` insertion (HtmlSanitizer); Chromium renders with network blocked, images embedded as data URIs.
- Open decisions: does anyone need Word output (clients / tracked-changes review)? Which sections are per-report editable? Keep real tool names in the methodology boilerplate or make them generic/configurable (conflicts with client masking)?

## Database-Backed Queues (implemented 2026-11-14 — Hangfire removed)

Supersedes the in-memory channel described under "Scan Reliability Hardening" above.

- **Scans:** the `scans` table is the queue. `ScanWorkerBackgroundService` runs one loop per tool; each claims work with a single atomic `UPDATE … WITH (UPDLOCK, READPAST, ROWLOCK) … OUTPUT` (`ScanRepository.TryClaimNextAsync`). Eligible: a Queued scan with no Queued/Running chain predecessor, or a Running scan whose lease expired. Lease 60s, renewed every 20s (`LeaseOwner`, `LeaseExpiresAt` columns). Losing the lease detaches rather than risking two owners.
- **Detached containers:** `docker run -d` with label `shieldreport.scan-id`, followed via `docker logs -f`. API shutdown → detach + release lease (container keeps running). Next claim re-attaches; the full output is re-read with `docker logs` after exit. Container gone (host reboot) → Failed "Interrupted". Containers/output are removed only after the result is saved (`IScanRunner.CleanupAsync`); `ScanContainerJanitorService` sweeps leftovers every 5 minutes. Timeout deadline is anchored to `StartedAt`, so re-attach doesn't reset it.
- **Background jobs:** `background_jobs` table + `BackgroundJobWorkerService` (2 loops, lease 10 min, exponential back-off 30s→30min, purge succeeded after 7 days). `IBackgroundJobQueue.EnqueueAsync` adds the row to the caller's unit of work (transactional outbox). First handler: `LlmExplanationJobHandler`. `VulnerabilityAiExplanationService` now rethrows provider errors so jobs actually retry.
- Verified end to end against SQL Server + Docker: normal run, graceful restart mid-scan, `kill -9` mid-scan, user Stop after re-attach. Bugs found by that test and fixed: `SqlParameter(name, 0)` binding to the `SqlDbType` overload; DataProtection key path hardcoded to `/app`.
- Remaining limitation: single API instance (user Stop is delivered in-process via `ScanCancellationRegistry`). A separate runner (Go) would lift this.

## Deployment Hardening (implemented 2026-11-14)

- `proxy` (Caddy) is the only service with published ports. `SITE_ADDRESS` selects Let's Encrypt (domain), internal-CA HTTPS (IP) or plain HTTP. `/api/*`, `/hubs/*`, `/health*` → api; everything else → fe. The SPA uses a relative `API_BASE_URL=/api/v1`, so it works on any host and needs no CORS.
- SQL Server: `MSSQL_PID=Express` (free for production; 10 GB per-database limit, watch `scans.RawOutput` growth), `MSSQL_MEMORY_LIMIT_MB`. No host port.
- All images pinned by digest via `.env` (`MSSQL_IMAGE`, `NAABU_IMAGE`, `NUCLEI_IMAGE`, …); the API is told the same scanner image references (`Scanning__Tools__*__Image`).
- `backup` service: full `BACKUP DATABASE … WITH CHECKSUM` + tar of uploads and DataProtection keys, on start and daily, with retention. Restore guide in `docker/backup/README.md`. Off-server copy is a host-side step (documented).
- `/health/ready`: database (Unhealthy → 503), Docker daemon (Degraded), disk (Degraded < 5 GB, Unhealthy < 1 GB).
- Not done in this phase: log shipping (Seq) and alerting; secrets still in `.env` (fine for a single server, move to a secret store later).
- Rate limits are configuration, not code: `RateLimiting:PerUserPerMinute` (default 600, per signed-in user) and `RateLimiting:AnonymousPerMinute` (default 30, per client IP for unauthenticated calls such as login; keep it low, it throttles password guessing). Compose passes `RATE_LIMIT_PER_USER_PER_MINUTE` / `RATE_LIMIT_ANONYMOUS_PER_MINUTE` from `.env`. The old hard-coded 100/min per user was reachable by one SPA user on polling pages.

## Audit Logging & Scan Authorization Records (implemented 2026-11-14)

**Audit log (#73).**
- `AuditSaveChangesInterceptor` (Persistence, scoped, registered on the DbContext): every Added/Modified/Deleted entity, including non-`BaseEntity` join tables (`UserRole`, `RolePermission`, `EngagementAssignee`, identified by composite key such as `UserId=1,RoleId=4`). Writes `{"Prop":{"old":…,"new":…}}` after the business save, in a second save that can't fail the first. Masked: names containing Password/Secret/Token/ApiKey/Hash/Mfa/Otp. Skipped: audit columns, `RawOutput`, lease columns, `PayloadJson`; ignored tables: AuditLog, BackgroundJob, RefreshToken, Notification, NotificationDeliveryLog, UserDashboardPreference, ScanFindingRaw, MfaRecoveryCode, PasswordResetToken. Raw-SQL statements (queue claims/heartbeats) are not audited. Startup seeding is recorded as system (no user).
- `IAuditLogger` (Application) for non-data security events, written in its own scope and never throwing: `LoginSucceeded`, `LoginFailed` (email + reason), `MfaChallengeIssued`, `MfaSucceeded`, `MfaFailed`, `Downloaded` (report docx/pdf, authorization evidence).
- Client IP: `UseForwardedHeaders` trusts one X-Forwarded-For hop (the Caddy proxy). **This also fixes the anonymous rate limiter**, which behind the proxy would otherwise have shared one bucket across all anonymous users.
- `GET /api/v1/audit-logs` (filters: userId, entityName, entityId, action, search, from, to; page size ≤ 200) and `GET /audit-logs/entity-names`; permission `audit-logs.read` (Admin via bulk grant, SuperAdmin bypass). FE: Audit Log page under Configuration (`/audit-logs`). Retention: `Audit:RetentionDays` (default 365), purged daily.

**Scan authorization records.**
- `scan_authorizations`: asset, optional engagement, approver (`AuthorizedByUserId`), client contact name/email, `ValidFrom`/`ValidUntil` (defaults: engagement dates, else now → +30 days; legacy-migrated rows have no end date), `AllowedTools`, notes, evidence file (pdf/png/jpg/eml/msg/txt/docx, 25 MB, stored under `uploads/scan-authorizations/{id}`), revocation (who/when/reason). Rows are never edited or deleted.
- `ScanService.CreateAsync` requires an authorization active now that allows every requested tool (`IScanAuthorizationService.EnsureCoveredAsync`), with specific 400 messages using client-safe tool labels; scans store `ScanAuthorizationId`. The worker re-checks before starting a queued scan.
- API: `GET/POST /assets/{id}/scan-authorizations`, `POST /scan-authorizations/{id}/evidence`, `GET /scan-authorizations/{id}/evidence`, `POST /scan-authorizations/{id}/revoke`. Permissions: `scan-authorizations.read` (Admin, Pentester), `scan-authorizations.manage` (Admin, SuperAdmin). The legacy one-click authorize/revoke endpoints now create/revoke records and require `manage`.
- `ClientAsset.IsAuthorizedForScanning` is kept as a derived "has a non-revoked authorization" flag for existing list views. It is refreshed on create/revoke, not on expiry; an expired authorization is still blocked at scan time with an "expired on …" message.
- Migration `AddAuditIndexesAndScanAuthorizations` converts every asset with the legacy flag into an open-ended, all-tools record so nothing stops scanning on upgrade.
- Not built: a pentester "request authorization" workflow (pentesters can view only).

## Scan Tool Settings (implemented 2026-11-14)

Implements the "Scan Tool Settings (planned)" section above, with two deliberate changes: **images are not editable** (choosing the image is choosing what runs on the host Docker daemon, so they stay pinned in `.env` and are shown read-only), and **client labels stay fixed** (they're hard-coded in several BE/FE places; partial editability would show clients inconsistent names).

- `scan_tool_settings` (one row per tool, seeded by `DefaultScanToolSettingsSeeder`, never overwritten): `IsEnabled`, `TimeoutMinutes`, `MemoryMb`, `Cpus`, `MaxParallel` (1–3), `OptionsJson` (typed `NaabuOptions` / `NucleiOptions` / `ReconftwOptions`, validated by `ScanToolOptionsValidator`).
- Flag mapping in `DockerScanRunner.BuildToolArguments`: Naabu `-top-ports 100|1000` / `-p -` / `-p <list>`, `-rate`, `-scan-type c|s` (SYN drops `--user 1000` so raw sockets work); Nuclei `-severity`, `-tags`, `-etags`, `-rate-limit`, `-c`; reconftw `-s|-p|-r` with a generated `/scan-output/scan-{id}.cfg` that sources the lean (or, for full mode, stock) config and sets `SUBBRUTE`, `SUBPERMUTE`/`SUBREGEXPERMUTE`, `WEBSCREENSHOT`, `SUBTAKEOVER`, thread counts. The file is removed with the scan's output; the janitor sweeps leftovers.
- `Scan.ToolSettingsJson`: `ScanToolRuntimeSettings` snapshot taken in `ScanService.CreateAsync`; the worker and runner use it, so saved changes only affect new scans. A disabled tool fails the whole request with 400 before any row is created.
- Worker: up to 3 loops per tool; loop `slot` only claims while `slot < MaxParallel` (re-read every 15s).
- API: `GET /scan-tools/available` (any signed-in user; drives the tool picker), `GET /scan-tools`, `PUT /scan-tools/{tool}`, `POST /scan-tools/{tool}/test` (`scan-tools.manage`). The test runs a version/help command in the local image and never pulls.
- `appsettings` `Scanning:Tools:*` now only holds `Image` and `PidsLimit` (deployment-owned).
- Note: reconftw silently falls back to its stock config if the `-f` file is missing, so `Scanning:ReconftwOutputPath` (API side) and `Scanning:ReconftwOutputVolume` (scan side) must point at the same storage — they do in `docker-compose.yml`.

## HTML Report Engine (implemented 2026-11-14)

Supersedes "HTML Report Generation (planned)" above. Decisions: PDF + Word output; editable sections = executive summary, key findings, key recommendations, project goals, limitations, per-asset scope status; real tool names hidden by default with a per-report "name the actual tools" option.

- **Templates**: `Infrastructure/Reporting/Html/white-box-report.hbs`, `gray-box-report.hbs`, `black-box-report.hbs` (embedded) — one built-in Handlebars template per testing type, each following its own SELISE Word sample (sections and wording), images in `Html/assets/`. Seeded as three ReportTemplate rows ("White/Gray/Black Box Testing — Selise Standard", `BuiltInTestingType` 0/1/2). A row with no `HtmlContent` renders the built-in of its `BuiltInTestingType`; a user-created row (null) renders the built-in matching the engagement's testing type. Generating without an explicit template picks the row whose `BuiltInTestingType` equals the engagement's testing type, then the default row. Variables = `ReportRenderModel` (Application/Reports/Rendering). Helpers: `text` (HTML-escaped; `- ` lines → bullets), `asset`, `formatDate`, `numberWords`, `severityBreakdown`, `upper`, `eq`. Admins can replace it per ReportTemplate (`HtmlContent`, validated by rendering sample data) or reset to the row's built-in one.
- **Rendering** (`HtmlReportRenderer`, `ChromiumBrowserProvider`): shared headless Chromium (2 concurrent renders), every page blocks all non-`data:` requests (no SSRF). Severity chart = SVG rendered to PNG once, used in both outputs. PDF: paged.js inline (TOC `target-counter`, running logo header, "Page X of Y", Confidential/DRAFT footer). Word: PreMailer inlines CSS, Word-only CSS (cover colours, page break after cover), explicit image sizes, TOC list → real TOC field (`updateFields` on open), tables forced to 100% width, converted with HtmlToOpenXml onto the embedded SELISE .docx (styles, header/footer).
- **Report**: `RenderedHtml` snapshot (what is approved), `ProjectGoals`, `Limitations` (defaults per testing type, `ReportSectionDefaults`), `ScopeStatusesJson`, `ShowToolNames`. Generate/Regenerate produce HTML + PDF + Word in one go (~1–3 s); Convert to PDF re-prints from the snapshot. Approved/rejected reports cannot be regenerated (409). API: `PATCH /reports/{id}/sections`, `GET /reports/{id}/preview` (JSON; FE shows it in a `sandbox=""` iframe), `GET/PUT/DELETE /ReportTemplates/{id}/html`.
- **Evidence**: `VulnerabilityAttachment.IncludeInReport` + `Caption` (`PATCH .../attachments/{id}/report-inclusion`, images only, ≤ 5 MB embedded).
- **Version table**: built from the engagement's report history (prepared by = generator, reviewed by = approver, comment from status).
- **Deployment**: API image installs `chromium`, `fonts-liberation`, `fonts-crosextra-carlito`, `fonts-dejavu-core`; override with `Reporting:ChromiumPath`.
- Verified end to end (White/Gray/Black): escaping of `<script>`/`onerror` payloads, scope status override, generic tool wording, screenshot + caption, approval lock, custom template save/use/reset, broken template rejected.
- Known limits: Word output matches content/structure, not pixel layout (Word applies its own pagination; the TOC fills when Word updates fields). Legacy-engine reports (custom .docx templates) have no preview and their PDF conversion still needs LibreOffice. Pre-existing bug noticed: creating a client organization with a duplicate name returns 500 instead of a clear 400/409.

## Report Delivery & Client Portal Reports (implemented 2026-11-14, tasks #66-68, #96)

- **Send** (`POST /reports/{id}/send`, `vulnerability-reports.send`, Admin/SuperAdmin): `{ recipients[], message?, attachWord }`. Only `Approved` reports with a PDF (409 otherwise). Recipients must be the client organization's own contacts — `GET /reports/{id}/recipient-options` returns the primary contact plus active CLIENT_ADMIN/CLIENT_USER users; anything else is rejected with 400, so a confidential report can't be mailed to an arbitrary address. Max 20 recipients, message ≤ 4,000 chars (HTML-escaped in the email).
- Creates a `ReportDelivery` row and a durable `report.send-email` background job (5 attempts, exponential back-off). `ReportEmailJobHandler` sends one email per recipient (addresses not shared), PDF always attached, Word optional, and saves progress after each email (`DeliveredTo`), so retries never duplicate. Re-checks approval before sending. On completion, in-app `REPORT_AVAILABLE` notifications go to the organization's portal users. `GET /reports/{id}/deliveries` = history/status (Pending/Sent/PartiallySent/Failed + last error). `IEmailService` gained an attachments overload.
- **Client portal** (`/api/v1/portal/reports`, `client-reports.read` for CLIENT_ADMIN/CLIENT_USER; caller must be a portal user, organization taken from the token): list Approved reports; `GET {id}/download-pdf|download-docx`. Draft, rejected, unknown and other-organization reports all return the same 404. Downloads are audited. FE: Portal → Reports.
- Verified end to end: invite email → client admin registration; send blocked before approval; portal hides the draft; recipient restriction; delivery Sent with one email per recipient carrying PDF + Word; message escaped; portal list + PDF download; staff blocked from portal endpoints (403).
- Also fixed (pre-existing): request logging ran inside the exception handler, so every handled 4xx was logged as a 500 error, and expected `AppException`s were logged as unhandled errors. Now request logs show the real status, and 4xx client errors are logged at Information.

## LLM Providers, Non-Blocking Report Drafting, Generate Dialog, Recon Modules (implemented 2026-11-14)

- **LLM provider CRUD.** `LlmProviderConfiguration.Name` is unique; several configurations per provider are allowed. Providers: OpenRouter, Gemini, OpenAI, **Anthropic** (official C# SDK, default model `claude-opus-5-5`, temperature not sent, server-side refusal fallback enabled), and **OpenAI-compatible** (any chat-completions endpoint; BaseUrl required). Endpoints: CRUD, `POST /{id}/activate`, `POST /{id}/deactivate`, `POST /{id}/test` (returns `{success, latencyMs, reply, error}` and never 5xx on provider failure). One configuration is active at a time. LLM HTTP calls time out after 60s.
- **Report narrative drafting never blocks a request.** Generate renders PDF and Word right away with placeholder narrative. If AI drafting is requested and a provider is active, it queues `report.draft-narrative` (`Report.NarrativeStatus = Drafting`). The job drafts, re-checks the database state, applies the draft and re-renders. A human edit (`SetNarrative`) switches the status to `Manual`, and the job then discards its draft. Failure → `Failed` + `NarrativeError`, and `POST /reports/{id}/draft-narrative` retries.
- **Generate dialog.** `GET /reports/template-options` (Generate permission), `POST /engagements/{id}/reports/preview {templateId, testingType, showToolNames}` (HTML only, nothing saved; legacy Word templates return 400), `POST /engagements/{id}/reports {templateId, testingType, draftWithAi, showToolNames}`. A testing type chosen at generation is saved on the engagement.
- **Reconnaissance modules.** `ReconftwOptions = {modules[], threads}`. The catalogue is `ReconftwModules.All` (26 modules: subdomain discovery, web detection, OSINT & hosts, web analysis), exposed at `GET /scan-tools/reconftw/modules`. The per-scan config writes every module's flags explicitly; `VULNS_GENERAL` is always false. Launch flag: `-s` when every checked module is a subdomain/web-detection one, else `-r`. At least one subdomain source is required. Old `{mode, dnsBruteforce, …}` JSON is still read (`EffectiveModules`).

## Prompt Library, Custom Scan Tools, Template Dynamic Fields, Grouped Menu (implemented 2026-11-14)

- **Prompt Library** (`/api/v1/llm-prompts`: GET, PUT `{key}`, POST `{key}/reset`, POST `{key}/test`). Built-in prompts are defined in `LlmPromptCatalog`; an `LlmPrompt` row overrides one. Saving requires the catalog's required placeholders and the JSON field names the caller parses. The explanation service uses the override when there is one, otherwise the provider's legacy `PromptTemplate`. The narrative service always uses the library, with `{FindingsList}`, `{ClientName}`, `{EngagementTitle}`, `{TestingType}`.
- **Custom scan tools** (`/api/v1/custom-scan-tools`, permission `custom-scan-tools.manage`, SuperAdmin only).
  - Image reference validated by regex; at most 40 argv tokens, each one line of up to 500 chars. At least one token must contain `{target}`/`{host}`/`{url}`. Only the known placeholders are allowed.
  - Runs detached with `--user 1000:1000` (unless run-as-root), `no-new-privileges`, memory/cpu/pids limits, no volumes.
  - Parsing: `lines` (one finding per non-empty line, capped at 500), `nuclei-jsonl`, or `raw`.
  - Scan creation accepts `customToolIds[]`. The authorization must allow `Custom`. `GET /scan-tools/available` lists enabled custom tools with `customToolId` and their client label.
- **Report template dynamic fields**: `[{key,label,type: text|multiline|date|number,defaultValue,help,required}]` (max 30, key `^[A-Za-z][A-Za-z0-9_]{0,39}$`). Values per report are set via `PATCH /reports/{id}/sections` (`customFieldValues`) and rendered as `{{Custom.key}}` (report value, else default). Also available: template upload (`.html/.htm/.hbs`, 2 MB, validated against sample data), sample preview, and a variable catalogue.
- **Menu**: `Menu.SortOrder`. Top-level entries without a URL are section headings, and empty headings are hidden per user. The seeder is idempotent and renames former titles (Configuration→Administration, LLM Provider Configuration→Providers & Models, Scan Tools→Tool Configuration).

## Per-user data scoping, list filters, Findings Report, LLM Provider List (2026-11-15)

- **Data scoping.** SuperAdmin sees all data. Every other staff user sees only engagements they lead, are assigned to or created, and the clients, assets, scans, vulnerabilities, reports, tasks, scan authorizations and retests of those engagements (rows without an engagement follow their client). The `data.view-all` permission lifts this per role; it is granted on the Role Permissions page and is deliberately not auto-granted to Admin. Enforced with EF global query filters in `ApplicationDbContext`, so lists, detail endpoints (404 when hidden) and dashboard aggregates are all scoped. Client-portal users stay scoped by organization.
- **List filters.** Engagements, client organizations, client assets, scans and vulnerabilities accept `clientOrganizationId`, `clientAssetId`, `from`, `to` (inclusive UTC days) and filter server-side.
- **Findings Report.** `GET /findings-report` (`findings-report.read`): clients and assets tested in a date range, with vulnerabilities grouped client → asset, filterable by severity and patch status.
- **LLM Provider List.** `llm_providers` (name, optional model) with CRUD at `/llm-provider-catalog`; a lookup that prefills the LLM configuration form.
- **User lifecycle.** Users are never deleted, only deactivated/reactivated (`PUT /Users/{id}/status`). There is no anonymous self-registration: staff are created by an admin (`POST /Users`, `users.create`, client roles refused, SuperAdmin only by a SuperAdmin) and client users join through registration invites.

## Load-test follow-ups (planned 2026-11-15, start 2026-11-16)

Found by `shield-report-fe/loadtest/scan-load.mjs` (25 users, 100 scans). Order: 1 and 2 first, because they decide whether 25 testers can work in parallel. Azure sizing is in `../AZURE-DEPLOYMENT-SIZING.md` (workspace root).

### 1. Scans can stall in Running (bug)

- **Seen once.** Scans 70100/70101 stayed Running for 34 minutes (claimed 13:04, finished 13:38) and blocked the client's queue. Not reproduced in two later full runs.
- **What is known:**
  - The 60 s lease is taken at claim, but `RenewLeaseUntilCancelledAsync` only starts after `scan.Start()` + SaveChanges, the status/phase pushes and settings resolution (`ScanWorkerBackgroundService.ProcessClaimedScanAsync`). A slow step lets the lease expire and a second worker re-claim the scan. 70101 ended Completed in the database although the log says it timed out, which suggests two workers handled it.
  - The timeout deadline is `StartedAt + TimeoutMinutes`, but its `CancellationTokenSource` is created after those steps, and `docker run` / `docker inspect` (`DockerScanRunner.RunDockerCommandAsync`) don't take the stop token (they have their own 10 min / 30 s limits).
- **Fix:**
  - Start the lease heartbeat immediately after claim, before any other step.
  - Create the timeout/stop tokens at claim and pass them to SaveChanges, the realtime pushes, and every docker command (`RunDockerCommandAsync` accepts a token and kills the process on cancel).
  - Add a safety sweep to `ScanContainerJanitorService`: fail any scan Running longer than `TimeoutMinutes` + grace (for example 5 min) whose lease has expired, stop and remove its container, release the lease, and start the next chain step.
  - Log each stage with its duration (claimed, started, container created, following, finished) so the next stall shows the step that hung.

### 2. Per-client scan cap: queue instead of reject, and make it configurable

- **Now:** `ScanQueueService.MaxConcurrentScansPerClient => 3` (hardcoded) counts queued + running scans, and `ScanService.CreateAsync` rejects anything over it with 409. In the load test about 690 requests were rejected and had to be resent.
- **Done 2026-11-16:** N is a global setting (`scan_queue_settings.MaxConcurrentScansPerClient`, 1–100, Tool Configuration → Scan limits), `CustomToolMaxParallel` is a setting too, and per-tool parallel goes up to 20. Still open below: queue instead of 409, enforcement in the claim SQL, per-client override, queue position.
- **Fix:**
  - Always accept scans as Queued, with a sanity limit per client such as 50 queued, which returns 409 when exceeded.
  - Enforce the "max N running per client" rule in the claim SQL (`ScanRepository.ClaimSql`): skip a candidate whose client already has N running scans with live leases. Use `sp_getapplock` (or serializable) around the claim so two workers can't both take the last slot.
  - Make N a global setting (Scan Tools page; `ScanToolSettingsService`), with an optional per-client override on `ClientOrganization`.
  - Show the queue position in the UI ("Queued, position 4").
  - Also make `CustomToolMaxParallel` (hardcoded 2) a setting.

### 3. Hide report fields the template doesn't use (UX)

- **Problem:** the edit dialog always offers Project Goals, but the Black Box template (like its SELISE sample) has no such section, so edits there have no effect.
- **Fix:**
  - The backend detects which section variables the effective template HTML references (`Report.ProjectGoals`, `Report.Limitations`, scope statuses, narrative fields) and returns them on `ReportDto` / template options as `UsedSections`.
  - The FE (`EngagementDetailPage` edit dialog) shows only those fields.
  - This works for custom templates too.

### 4. Report generation throughput (performance)

- **Now:** one generation takes 0.7–1 s (HTML → PDF in the shared Chromium, plus HTML → Word). `ChromiumBrowserProvider` allows only 2 concurrent renders (`_renderSlots = new(2, 2)`), so 25 at once took 18 s for the last.
- **Fix:**
  - **Quick:** make the render-slot count configurable (`Reporting:MaxConcurrentRenders`, default = CPU count).
  - **Better:** run generation as a background job. `POST .../reports` returns at once with status Generating, and SignalR notifies when the report is ready (same pattern as AI narrative drafting).
  - Produce the Word file lazily on first download instead of at every generate/regenerate, which saves about a third of the work.
