namespace ShieldReport.Domain.Enums;

// What a report template renders. Stored as a byte: append new values, never reorder.
//   Engagement      - one engagement's pentest report (White/Gray/Black Box), generated from the
//                     engagement page.
//   FindingsSummary - the cross-client Findings Report export (clients -> assets -> findings).
public enum ReportTemplateKind
{
    Engagement,
    FindingsSummary
}
