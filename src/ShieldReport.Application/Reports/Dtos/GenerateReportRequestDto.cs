using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Reports.Dtos;

// Options from the Generate Report dialog. All optional: an empty body keeps the old behaviour
// (default template, engagement's testing type, AI drafting when a provider is active).
//   TemplateId   - a ReportTemplate; null = the default one.
//   TestingType  - White/Gray/Black; when set it is also saved on the engagement.
//   DraftWithAi  - queue LLM drafting of the narrative (the report is generated immediately with
//                  placeholder text and re-rendered when the draft arrives).
public sealed record GenerateReportRequestDto(
    long? TemplateId = null,
    TestingType? TestingType = null,
    bool DraftWithAi = true,
    bool ShowToolNames = false);
