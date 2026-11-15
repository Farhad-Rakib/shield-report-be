using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Reports.Dtos;

// Options from the Generate Report dialog. There is no default template: the user picks one.
//   TemplateId        - an Engagement-kind ReportTemplate (required).
//   TestingType       - White/Gray/Black; when set it is also saved on the engagement.
//   DraftWithAi       - queue LLM drafting of the narrative (the report is generated immediately
//                       with placeholder text and re-rendered when the draft arrives).
//   CustomFieldValues - values for the template's dynamic fields ({{Custom.<key>}}), filled in the
//                       dialog. Required fields must have a value or a default.
public sealed record GenerateReportRequestDto(
    long? TemplateId = null,
    TestingType? TestingType = null,
    bool DraftWithAi = true,
    bool ShowToolNames = false,
    Dictionary<string, string>? CustomFieldValues = null);
