using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Reports.Rendering;

// Default wording for the per-report editable sections, taken from the SELISE White/Gray/Black Box
// templates. Pre-filled when a report is generated; pentesters edit them before approval.
// Lines starting with "- " render as bullet points.
public static class ReportSectionDefaults
{
    public static string ProjectGoals(string clientName) =>
        $"""
        The primary goal of this project is to conduct a comprehensive vulnerability assessment and penetration testing of the scope provided by {clientName} to identify vulnerabilities, security risks, misconfigurations, and potential attack vectors that could be exploited by intruders.
        This assessment aims to:
        - Evaluate the overall security posture of the target systems.
        - Perform VAPT, code assessments, and exploitation of identified vulnerabilities.
        - Test their resilience against a broad spectrum of real-world threats.
        - Ensure alignment with industry best practices and relevant regulatory compliance requirements.
        The outcome of this project includes detailed, actionable recommendations designed to remediate identified risks, enhance the organization's security controls, and safeguard sensitive data, supporting the confidentiality, integrity, and availability of the application.
        """;

    public static string Limitations(TestingType testingType) => testingType == TestingType.Black
        ? "This assessment was performed within a defined scope and timeframe and represents the application's security posture at the time of testing. While reasonable efforts were made to identify vulnerabilities, no assessment can guarantee the discovery of all security issues or provide assurance against future threats."
        : "This report is a best-effort security assessment conducted within a limited timeframe and does not guarantee complete coverage or full security certification. It reflects the application's security posture only during the time of testing, as new vulnerabilities may emerge afterward. While a structured methodology was used and remediation will improve security, it does not eliminate all risks. Continuous monitoring, regular updates, and ongoing security testing are necessary to maintain a strong security posture.";

    public const string DefaultScopeStatus = "Completed";
}
