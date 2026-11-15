using ShieldReport.Domain.Enums;

namespace ShieldReport.Domain.Entities;

// One row with DocxTemplatePath == null (the seeded default) always fills the embedded, byte-
// identical White Box Testing.docx using OpenXmlReportDocxTemplateFiller's original hardcoded
// literal-text anchors — that path is untouched and keeps working exactly as before. A row with
// DocxTemplatePath set instead fills *that* uploaded .docx using a generic {{Token}} engine
// (see OpenXmlReportDocxTemplateFiller.FillGeneric) — the two are deliberately separate engines
// so uploading a custom template can never regress the known-good default.
public sealed class ReportTemplate : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string CompanyDisplayName { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public string? DocxTemplatePath { get; private set; }
    public string? DocxTemplateFileName { get; private set; }
    // Custom Handlebars HTML template. Null = the built-in default (White/Gray/Black Box) template.
    public string? HtmlContent { get; private set; }
    // Admin-defined dynamic fields ([{key,label,type,defaultValue,help}]), filled per report and
    // available to the template as {{Custom.<key>}} (escape user text with {{{text Custom.<key>}}}).
    public string CustomFieldsJson { get; private set; } = "[]";
    // Which built-in HTML template this row falls back to while HtmlContent is null (White, Gray
    // or Black Box). Null = a user-created template, which falls back to the template matching
    // the engagement's testing type. Also used to pick the default template for an engagement.
    public TestingType? BuiltInTestingType { get; private set; }

    public void SetBuiltInTestingType(TestingType? testingType) => BuiltInTestingType = testingType;

    public void SetCustomFields(string customFieldsJson)
    {
        CustomFieldsJson = string.IsNullOrWhiteSpace(customFieldsJson) ? "[]" : customFieldsJson;
    }

    public void SetHtmlContent(string? htmlContent)
    {
        HtmlContent = string.IsNullOrWhiteSpace(htmlContent) ? null : htmlContent;
    }

    private ReportTemplate()
    {
    }

    public ReportTemplate(string name, string companyDisplayName, bool isDefault = false)
    {
        Name = !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : throw new ArgumentException("Name is required.", nameof(name));
        CompanyDisplayName = companyDisplayName;
        IsDefault = isDefault;
    }

    public void UpdateDetails(string name, string companyDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        CompanyDisplayName = companyDisplayName;
    }

    public void SetDocxTemplate(string relativePath, string originalFileName)
    {
        DocxTemplatePath = !string.IsNullOrWhiteSpace(relativePath)
            ? relativePath
            : throw new ArgumentException("Path is required.", nameof(relativePath));
        DocxTemplateFileName = originalFileName;
    }

    public void ClearDocxTemplate()
    {
        DocxTemplatePath = null;
        DocxTemplateFileName = null;
    }

    public void MarkAsDefault() => IsDefault = true;

    public void UnmarkAsDefault() => IsDefault = false;
}
