namespace ShieldReport.Application.Reports.Dtos;

// Everything the FE needs to assemble an OnlyOffice DocEditor config — see
// ReportEditorModal.tsx. documentServerUrl is browser-reachable; apiHostForContainer is only
// reachable from inside the Document Server's own container (see the plan's networking note).
public sealed record ReportEditorSessionDto(
    string DocumentServerUrl,
    string ApiHostForContainer,
    string AccessToken,
    string DocumentKey,
    string FileName
);
