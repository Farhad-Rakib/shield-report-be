namespace ShieldReport.Application.Common.Interfaces.Security;

// A narrow-purpose, short-lived credential distinct from the app's real JWTs — OnlyOffice's
// Document Server fetches/saves a report's draft from inside its own container, with no user
// session or bearer token, so the source/callback URLs we hand it carry this instead. Scoped to
// exactly one report id, expires quickly (see implementation), and grants nothing else.
public interface IReportEditorAccessTokenService
{
    string Issue(long reportId);
    bool TryValidate(string token, out long reportId);
}
