using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Infrastructure.Reporting;

// Fills the literal White Box Testing.docx (embedded resource, byte-identical to the attached
// file) by locating the exact placeholder text the template's own Word reviewer comments mark —
// see src/Docs/REQUIREMENTS.md's "White Box Testing Report Content Mapping" for the full anchor
// table this was built from. Every other run/style/logo/header/footer is left completely
// untouched; comments themselves are not removed.
//
// A ReportTemplate with a custom uploaded DocxTemplatePath instead goes through FillGeneric
// below, a completely separate {{Token}}-based engine (see its own doc comment) — the two never
// share logic, specifically so a custom upload can never regress this known-good default path.
public sealed class OpenXmlReportDocxTemplateFiller : IReportDocxTemplateFiller
{
    private const string TemplateResourceName = "ShieldReport.Infrastructure.Reporting.Templates.WhiteBoxTesting.docx";

    private readonly IFileStorageService _fileStorageService;

    public OpenXmlReportDocxTemplateFiller(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    public byte[] Fill(
        Engagement engagement,
        ClientOrganization clientOrganization,
        ReportTemplate template,
        IReadOnlyList<Vulnerability> includedFindings,
        ReportNarrative narrative,
        int version)
    {
        if (template.DocxTemplatePath is not null)
        {
            return FillGeneric(template, engagement, clientOrganization, includedFindings, narrative, version);
        }

        using var output = new MemoryStream();
        using (var templateStream = LoadTemplateStream())
        {
            templateStream.CopyTo(output);
        }
        output.Position = 0;

        using (var wordDocument = WordprocessingDocument.Open(output, true))
        {
            var body = wordDocument.MainDocumentPart!.Document.Body!;
            var numberingPart = wordDocument.MainDocumentPart.NumberingDefinitionsPart;

            ReplaceSimpleTokens(body, engagement, clientOrganization, includedFindings, version);
            ReplaceBulletsBetweenHeadings(numberingPart, TopLevelParagraphs(body), "KEY FINDINGS", "KEY RECOMMENDATIONS", narrative.KeyFindings);
            ReplaceBulletsBetweenHeadings(numberingPart, TopLevelParagraphs(body), "KEY RECOMMENDATIONS", "PROJECT OVERVIEW", narrative.KeyRecommendations);
            ReplaceScopeTableRow(body, includedFindings, engagement);
            ReplaceDiscoveredVulnerabilitiesTable(body, includedFindings);
            ReplaceDetailedFindingsBlock(numberingPart, body, includedFindings);
            RemoveComments(wordDocument.MainDocumentPart);

            wordDocument.MainDocumentPart.Document.Save();
        }

        return output.ToArray();
    }

    // The template's own Word review comments (from whoever authored White Box Testing.docx)
    // were only ever needed to locate the fill points during development — they have no place
    // in a generated client report, so strip them (and their anchor marks) entirely.
    private static void RemoveComments(MainDocumentPart mainPart)
    {
        var body = mainPart.Document.Body!;
        foreach (var element in body.Descendants<CommentRangeStart>().ToList())
        {
            element.Remove();
        }
        foreach (var element in body.Descendants<CommentRangeEnd>().ToList())
        {
            element.Remove();
        }
        foreach (var element in body.Descendants<CommentReference>().ToList())
        {
            element.Remove();
        }

        var commentParts = mainPart.Parts
            .Where(p => p.OpenXmlPart.Uri.OriginalString.Contains("comment", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.OpenXmlPart)
            .ToList();
        foreach (var part in commentParts)
        {
            mainPart.DeletePart(part);
        }
    }

    private static Stream LoadTemplateStream()
    {
        var assembly = typeof(OpenXmlReportDocxTemplateFiller).Assembly;
        return assembly.GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException($"Embedded report template '{TemplateResourceName}' not found.");
    }

    private static List<Paragraph> TopLevelParagraphs(Body body) => body.Elements<Paragraph>().ToList();

    private static string GetText(OpenXmlElement element) => string.Concat(element.Descendants<Text>().Select(t => t.Text));

    private static void SetParagraphText(Paragraph paragraph, string text)
    {
        var texts = paragraph.Descendants<Text>().ToList();
        if (texts.Count == 0)
        {
            return;
        }

        texts[0].Text = text;
        texts[0].Space = SpaceProcessingModeValues.Preserve;
        for (var i = 1; i < texts.Count; i++)
        {
            texts[i].Text = string.Empty;
        }
    }

    // ---- Simple token replacements (see the anchor table in REQUIREMENTS.md) ----

    private static void ReplaceSimpleTokens(Body body, Engagement engagement, ClientOrganization clientOrganization, IReadOnlyList<Vulnerability> includedFindings, int version)
    {
        // Descendants (not Elements) so this also reaches the version-table's isolated "1.1" cell.
        var paragraphs = body.Descendants<Paragraph>().ToList();

        var severityCounts = includedFindings
            .GroupBy(v => v.SeverityOverride ?? v.Severity)
            .ToDictionary(g => g.Key, g => g.Count());

        ReplaceExact(paragraphs, "1.1", version.ToString());
        ReplaceContains(paragraphs, "<Client Name>", clientOrganization.Name);
        ReplaceContains(paragraphs, "<XYZ client>", clientOrganization.Name);
        ReplaceContains(paragraphs, "27/03/2026 to 06/04/2026", BuildDateRange(engagement));
        ReplaceContains(paragraphs, "twenty-eight (28)", includedFindings.Count.ToString());
        ReplaceContains(paragraphs, "seven (7)", includedFindings.Count.ToString());
        ReplaceContains(
            paragraphs,
            "thirteen (13) were in a high-risk state, ten (10) were in a medium-risk state, and five (5) were in a low-risk state,",
            BuildSeverityBreakdown(severityCounts) + " finding(s),");
        ReplaceContains(
            paragraphs,
            "two (2) were classified as high-risk, three (3) as medium-risk, and two (2) as low-severity risk issues.",
            BuildSeverityBreakdown(severityCounts) + " finding(s).");
    }

    private static bool ReplaceExact(IEnumerable<Paragraph> paragraphs, string exactText, string replacement)
    {
        foreach (var paragraph in paragraphs)
        {
            if (GetText(paragraph).Trim() != exactText)
            {
                continue;
            }

            SetParagraphText(paragraph, replacement);
            return true;
        }

        return false;
    }

    private static bool ReplaceContains(IEnumerable<Paragraph> paragraphs, string containsText, string replacement)
    {
        foreach (var paragraph in paragraphs)
        {
            var full = GetText(paragraph);
            if (!full.Contains(containsText, StringComparison.Ordinal))
            {
                continue;
            }

            SetParagraphText(paragraph, full.Replace(containsText, replacement));
            return true;
        }

        return false;
    }

    private static string BuildDateRange(Engagement engagement)
    {
        var from = engagement.StartDate?.ToString("dd/MM/yyyy") ?? "N/A";
        var to = DateTime.UtcNow.ToString("dd/MM/yyyy");
        return $"{from} to {to}";
    }

    private static string BuildSeverityBreakdown(Dictionary<Severity, int> counts)
    {
        var parts = new List<string>();
        foreach (var severity in new[] { Severity.Critical, Severity.High, Severity.Medium, Severity.Low, Severity.Informational })
        {
            if (counts.TryGetValue(severity, out var count) && count > 0)
            {
                parts.Add($"{count} {severity.ToString().ToLowerInvariant()}-risk");
            }
        }

        return parts.Count switch
        {
            0 => "no findings were categorized by severity",
            1 => parts[0],
            _ => string.Join(", ", parts[..^1]) + ", and " + parts[^1]
        };
    }

    // ---- Repeating bullet paragraphs (Key Findings / Key Recommendations / per-finding lists) ----

    private static void ReplaceBulletsBetweenHeadings(NumberingDefinitionsPart? numberingPart, List<Paragraph> paragraphs, string startHeading, string? endHeading, string text)
    {
        var startIdx = paragraphs.FindIndex(p => GetText(p).Trim() == startHeading);
        if (startIdx < 0)
        {
            return;
        }

        var endIdx = endHeading is null
            ? paragraphs.Count
            : paragraphs.FindIndex(startIdx + 1, p => GetText(p).Trim() == endHeading);
        if (endIdx < 0)
        {
            endIdx = paragraphs.Count;
        }

        var templateBullets = paragraphs.GetRange(startIdx + 1, endIdx - startIdx - 1);
        if (templateBullets.Count == 0)
        {
            return;
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
        if (lines.Length == 0)
        {
            lines = [text];
        }

        var master = templateBullets[0];

        // Each cloned group of a *numbered* (not bulleted) list needs its own numbering instance
        // restarted at 1 — otherwise every finding's "Step of Reproduction" list keeps counting
        // up from wherever the previous finding's list left off, since OOXML numbering state is
        // tracked per numId, and CloneNode copies the same numId reference by default.
        var freshNumId = TryCreateRestartedNumbering(numberingPart, master);

        foreach (var line in lines)
        {
            var clone = (Paragraph)master.CloneNode(true);
            SetParagraphText(clone, line);
            if (freshNumId.HasValue)
            {
                SetNumberingId(clone, freshNumId.Value);
            }
            master.InsertBeforeSelf(clone);
        }

        foreach (var bullet in templateBullets)
        {
            bullet.Remove();
        }
    }

    // Returns a brand-new numId (pointing at the same abstract numbering definition, so the
    // visual style/glyph is unchanged) with its level restarted at 1, or null if the master
    // paragraph isn't part of a numbered list at all (e.g. plain body text, or a bulleted list —
    // bullets don't visibly increment so they don't need this).
    private static int? TryCreateRestartedNumbering(NumberingDefinitionsPart? numberingPart, Paragraph master)
    {
        if (numberingPart?.Numbering is null)
        {
            return null;
        }

        var numId = master.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
        if (numId is null)
        {
            return null;
        }

        var existingInstance = numberingPart.Numbering.Elements<NumberingInstance>().FirstOrDefault(n => n.NumberID?.Value == numId);
        var abstractNumId = existingInstance?.AbstractNumId?.Val?.Value;
        if (abstractNumId is null)
        {
            return null;
        }

        var nextNumId = numberingPart.Numbering.Elements<NumberingInstance>().Max(n => n.NumberID?.Value ?? 0) + 1;
        var newInstance = new NumberingInstance(
            new AbstractNumId { Val = abstractNumId },
            new LevelOverride(new StartOverrideNumberingValue { Val = 1 }) { LevelIndex = 0 })
        {
            NumberID = nextNumId
        };
        numberingPart.Numbering.Append(newInstance);
        return nextNumId;
    }

    private static void SetNumberingId(Paragraph paragraph, int numId)
    {
        var numberingId = paragraph.ParagraphProperties?.NumberingProperties?.NumberingId;
        if (numberingId is not null)
        {
            numberingId.Val = numId;
        }
    }

    private static void ReplaceSingleParagraphAfterHeading(List<Paragraph> paragraphs, string headingText, string newText)
    {
        var idx = paragraphs.FindIndex(p => GetText(p).Trim() == headingText);
        if (idx < 0 || idx + 1 >= paragraphs.Count)
        {
            return;
        }

        SetParagraphText(paragraphs[idx + 1], newText);
    }

    // ---- Scope-of-Engagement asset table (one row per distinct asset referenced by included findings) ----

    private static void ReplaceScopeTableRow(Body body, IReadOnlyList<Vulnerability> includedFindings, Engagement engagement)
    {
        var table = body.Descendants<Table>().FirstOrDefault(t => t.Descendants<TableCell>().Any(c => GetText(c).Trim() == "XYZ"));
        var templateRow = table?.Descendants<TableRow>().FirstOrDefault(r => r.Descendants<TableCell>().Any(c => GetText(c).Trim() == "XYZ"));
        if (templateRow is null)
        {
            return;
        }

        var assets = includedFindings
            .Where(f => f.ClientAsset is not null)
            .Select(f => f.ClientAsset!)
            .DistinctBy(a => a.Id)
            .ToList();

        if (assets.Count == 0)
        {
            templateRow.Remove();
            return;
        }

        foreach (var asset in assets)
        {
            var clone = (TableRow)templateRow.CloneNode(true);
            var cells = clone.Elements<TableCell>().ToList();
            SetCellText(cells, 0, asset.Name);
            SetCellText(cells, 1, DescribeAssetType(asset.AssetType));
            SetCellText(cells, 2, asset.Identifier);
            SetCellText(cells, 3, engagement.Status.ToString());
            templateRow.InsertBeforeSelf(clone);
        }

        templateRow.Remove();
    }

    private static string DescribeAssetType(AssetType assetType) => assetType switch
    {
        AssetType.WebUrl => "Web Application",
        AssetType.IpRange => "Network",
        AssetType.MobileApp => "Mobile Application",
        AssetType.Api => "API",
        AssetType.CloudEnvironment => "Cloud Environment",
        _ => "Other"
    };

    // ---- Discovered-Vulnerabilities summary table (one row per included finding) ----

    private static void ReplaceDiscoveredVulnerabilitiesTable(Body body, IReadOnlyList<Vulnerability> includedFindings)
    {
        var table = body.Descendants<Table>().FirstOrDefault(t =>
        {
            var headerRow = t.Elements<TableRow>().FirstOrDefault();
            if (headerRow is null)
            {
                return false;
            }

            var headerText = GetText(headerRow);
            return headerText.Contains("Vulnerabilities", StringComparison.Ordinal) && headerText.Contains("Vulnerable Systems", StringComparison.Ordinal);
        });
        if (table is null)
        {
            return;
        }

        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count < 2)
        {
            return;
        }

        var templateRow = rows[1];
        var exampleRows = rows.Skip(1).ToList();

        if (includedFindings.Count == 0)
        {
            foreach (var row in exampleRows)
            {
                row.Remove();
            }
            return;
        }

        foreach (var finding in includedFindings)
        {
            var clone = (TableRow)templateRow.CloneNode(true);
            var cells = clone.Elements<TableCell>().ToList();
            SetCellText(cells, 0, finding.Title);
            SetCellText(cells, 1, (finding.SeverityOverride ?? finding.Severity).ToString());
            SetCellText(cells, 2, "1");
            SetCellText(cells, 3, finding.PatchStatus.ToString());
            templateRow.InsertBeforeSelf(clone);
        }

        foreach (var row in exampleRows)
        {
            row.Remove();
        }
    }

    private static void SetCellText(List<TableCell> cells, int index, string text)
    {
        if (index >= cells.Count)
        {
            return;
        }

        var paragraph = cells[index].Elements<Paragraph>().FirstOrDefault();
        if (paragraph is not null)
        {
            SetParagraphText(paragraph, text);
        }
    }

    // ---- Detailed Findings block (the whole "SQL Injection Vulnerability" example, cloned once per finding) ----

    private static void ReplaceDetailedFindingsBlock(NumberingDefinitionsPart? numberingPart, Body body, IReadOnlyList<Vulnerability> includedFindings)
    {
        var allParagraphs = TopLevelParagraphs(body);
        var headingIndex = allParagraphs.FindIndex(p => GetText(p).Trim() == "SQL Injection Vulnerability");
        var annexureIndex = allParagraphs.FindIndex(p => GetText(p).Trim() == "Annexure");
        if (headingIndex < 0 || annexureIndex < 0 || annexureIndex <= headingIndex)
        {
            return;
        }

        var annexureParagraph = allParagraphs[annexureIndex];
        var headingParagraph = allParagraphs[headingIndex];

        var templateUnit = new List<OpenXmlElement>();
        var current = (OpenXmlElement?)headingParagraph;
        while (current is not null && current != annexureParagraph)
        {
            templateUnit.Add(current);
            current = current.NextSibling();
        }

        if (includedFindings.Count == 0)
        {
            annexureParagraph.InsertBeforeSelf(new Paragraph(new Run(new Text("No vulnerabilities are included in this report."))));
            foreach (var element in templateUnit)
            {
                element.Remove();
            }
            return;
        }

        for (var i = 0; i < includedFindings.Count; i++)
        {
            var scratch = new Body();
            foreach (var element in templateUnit)
            {
                scratch.AppendChild(element.CloneNode(true));
            }

            FillFindingBlock(numberingPart, scratch, includedFindings[i], i + 1);

            foreach (var child in scratch.ChildElements.ToList())
            {
                child.Remove();
                annexureParagraph.InsertBeforeSelf(child);
            }
        }

        foreach (var element in templateUnit)
        {
            element.Remove();
        }
    }

    private static void FillFindingBlock(NumberingDefinitionsPart? numberingPart, Body scratch, Vulnerability finding, int sequenceNumber)
    {
        var paragraphs = scratch.Elements<Paragraph>().ToList();

        var headingParagraph = paragraphs.FirstOrDefault(p => GetText(p).Trim() == "SQL Injection Vulnerability");
        if (headingParagraph is not null)
        {
            SetParagraphText(headingParagraph, finding.Title);
        }

        var table = scratch.Elements<Table>().FirstOrDefault();
        if (table is not null)
        {
            FillFindingInfoTable(table, finding, sequenceNumber);
        }

        ReplaceSingleParagraphAfterHeading(paragraphs, "Description", finding.Description ?? "No description recorded.");
        ReplaceSingleParagraphAfterHeading(paragraphs, "Impact", finding.Impact ?? "No impact recorded.");
        ReplaceBulletsBetweenHeadings(numberingPart, paragraphs, "Recommendations", "Proof of Concept", finding.Recommendation ?? "No recommendation recorded.");

        // The Recommendations bullet-clone above mutated paragraph siblings (removed/inserted) —
        // recompute before locating anything after it.
        paragraphs = scratch.Elements<Paragraph>().ToList();
        ReplaceSingleParagraphAfterHeading(paragraphs, "Proof of Concept", finding.ProofOfConcept ?? "No proof of concept recorded.");
        ReplaceBulletsBetweenHeadings(numberingPart, paragraphs, "Step of Reproduction:", null, finding.StepsToReproduce ?? "No reproduction steps recorded.");
    }

    private static void FillFindingInfoTable(Table table, Vulnerability finding, int sequenceNumber)
    {
        foreach (var row in table.Elements<TableRow>())
        {
            var cells = row.Elements<TableCell>().ToList();
            if (cells.Count < 2)
            {
                continue;
            }

            var label = GetText(cells[0]).Trim();
            string? value = label switch
            {
                "Finding ID" => $"VULN-{sequenceNumber:D3}",
                "Severity" => (finding.SeverityOverride ?? finding.Severity).ToString().ToUpperInvariant(),
                "CVSS Score" => finding.CvssScore.HasValue ? $"{finding.CvssScore} ({finding.CvssVector})" : finding.CvssVector ?? "N/A",
                "Affected System" => finding.ClientAsset?.Identifier ?? finding.Endpoint,
                "Vulnerable Endpoint" => finding.Endpoint,
                "Tools Used" => string.Join(", ", ParseJsonStringArray(finding.ToolsUsed).Select(t => t.ToClientLabel())),
                "References" => string.Join(", ", ParseJsonStringArray(finding.ReferenceLinks)),
                _ => null
            };

            if (value is null)
            {
                continue;
            }

            var paragraph = cells[1].Elements<Paragraph>().FirstOrDefault();
            if (paragraph is not null)
            {
                SetParagraphText(paragraph, value);
            }
        }
    }

    private static IReadOnlyList<string> ParseJsonStringArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // ======================================================================================
    // Generic {{Token}} engine — for a ReportTemplate with a custom uploaded .docx. Unlike the
    // default engine above (which locates fill points by matching literal English sentences
    // copied from one specific document), this one recognizes a documented placeholder
    // vocabulary that any uploaded .docx can embed as plain text:
    //
    //   Simple tokens (anywhere in any paragraph, including inside table cells):
    //     {{ClientName}} {{CompanyDisplayName}} {{ReportVersion}} {{EngagementDateRange}}
    //     {{TotalFindingsCount}} {{SeverityBreakdown}} {{GeneratedDate}}
    //
    //   Multi-line tokens (must occupy an entire paragraph by itself — newlines in the source
    //   text become line breaks in that paragraph):
    //     {{ExecutiveSummary}}
    //
    //   Repeating bullet-list tokens (a paragraph containing only this token is cloned once per
    //   line of the source narrative text, in the same list style as that paragraph):
    //     {{KeyFindingsList}} {{KeyRecommendationsList}}
    //
    //   Repeating table row — any table row containing at least one {{Finding.*}} token is
    //   treated as the row template and cloned once per included finding:
    //     {{Finding.Title}} {{Finding.Severity}} {{Finding.PatchStatus}} {{Finding.Endpoint}}
    //     {{Finding.AssetName}}
    //
    //   Repeating detailed-findings block — everything between a paragraph containing exactly
    //   {{DetailedFindingsStart}} and one containing exactly {{DetailedFindingsEnd}} is cloned
    //   once per included finding; within each clone, both the simple Finding.* tokens above and
    //   these multi-line ones (each on their own paragraph) resolve:
    //     {{Finding.Id}} {{Finding.CvssScore}} {{Finding.AffectedSystem}} {{Finding.ToolsUsed}}
    //     {{Finding.References}} {{Finding.Description}} {{Finding.Impact}}
    //     {{Finding.Recommendation}} {{Finding.ProofOfConcept}} {{Finding.StepsToReproduce}}
    // ======================================================================================

    private byte[] FillGeneric(
        ReportTemplate template,
        Engagement engagement,
        ClientOrganization clientOrganization,
        IReadOnlyList<Vulnerability> includedFindings,
        ReportNarrative narrative,
        int version)
    {
        using var output = new MemoryStream();
        using (var templateStream = _fileStorageService.OpenRead(template.DocxTemplatePath!)
            ?? throw new InvalidOperationException($"Uploaded report template file '{template.DocxTemplatePath}' is missing from storage."))
        {
            templateStream.CopyTo(output);
        }
        output.Position = 0;

        using (var wordDocument = WordprocessingDocument.Open(output, true))
        {
            var body = wordDocument.MainDocumentPart!.Document.Body!;
            var numberingPart = wordDocument.MainDocumentPart.NumberingDefinitionsPart;

            var severityCounts = includedFindings
                .GroupBy(v => v.SeverityOverride ?? v.Severity)
                .ToDictionary(g => g.Key, g => g.Count());

            var simpleTokens = new Dictionary<string, string>
            {
                ["{{ClientName}}"] = clientOrganization.Name,
                ["{{CompanyDisplayName}}"] = template.CompanyDisplayName,
                ["{{ReportVersion}}"] = version.ToString(),
                ["{{EngagementDateRange}}"] = BuildDateRange(engagement),
                ["{{TotalFindingsCount}}"] = includedFindings.Count.ToString(),
                ["{{SeverityBreakdown}}"] = BuildSeverityBreakdown(severityCounts),
                ["{{GeneratedDate}}"] = DateTime.UtcNow.ToString("dd/MM/yyyy"),
            };

            ReplaceGenericFindingRow(body, includedFindings);
            ReplaceGenericDetailedFindingsBlock(numberingPart, body, includedFindings);
            ReplaceGenericBulletListToken(numberingPart, body, "{{KeyFindingsList}}", narrative.KeyFindings);
            ReplaceGenericBulletListToken(numberingPart, body, "{{KeyRecommendationsList}}", narrative.KeyRecommendations);
            ReplaceGenericMultilineToken(body, "{{ExecutiveSummary}}", narrative.ExecutiveSummary);
            ReplaceGenericSimpleTokens(body, simpleTokens);

            wordDocument.MainDocumentPart.Document.Save();
        }

        return output.ToArray();
    }

    private static Dictionary<string, string> BuildFindingTokens(Vulnerability finding, int sequenceNumber) => new()
    {
        ["{{Finding.Id}}"] = $"VULN-{sequenceNumber:D3}",
        ["{{Finding.Title}}"] = finding.Title,
        ["{{Finding.Severity}}"] = (finding.SeverityOverride ?? finding.Severity).ToString(),
        ["{{Finding.PatchStatus}}"] = finding.PatchStatus.ToString(),
        ["{{Finding.Endpoint}}"] = finding.Endpoint,
        ["{{Finding.AssetName}}"] = finding.ClientAsset?.Name ?? "—",
        ["{{Finding.AffectedSystem}}"] = finding.ClientAsset?.Identifier ?? finding.Endpoint,
        ["{{Finding.CvssScore}}"] = finding.CvssScore.HasValue ? $"{finding.CvssScore} ({finding.CvssVector})" : finding.CvssVector ?? "N/A",
        ["{{Finding.ToolsUsed}}"] = string.Join(", ", ParseJsonStringArray(finding.ToolsUsed).Select(t => t.ToClientLabel())),
        ["{{Finding.References}}"] = string.Join(", ", ParseJsonStringArray(finding.ReferenceLinks)),
    };

    private static readonly (string Token, Func<Vulnerability, string?> Value)[] FindingMultilineTokens =
    [
        ("{{Finding.Description}}", f => f.Description),
        ("{{Finding.Impact}}", f => f.Impact),
        ("{{Finding.Recommendation}}", f => f.Recommendation),
        ("{{Finding.ProofOfConcept}}", f => f.ProofOfConcept),
        ("{{Finding.StepsToReproduce}}", f => f.StepsToReproduce),
    ];

    // Simple, single-line tokens — a straight find/replace across every paragraph's concatenated
    // text (including inside table cells, since Descendants<Paragraph>() reaches those too).
    private static void ReplaceGenericSimpleTokens(OpenXmlElement root, Dictionary<string, string> tokens)
    {
        foreach (var paragraph in root.Descendants<Paragraph>().ToList())
        {
            var text = GetText(paragraph);
            if (!tokens.Keys.Any(text.Contains))
            {
                continue;
            }

            foreach (var (token, value) in tokens)
            {
                text = text.Replace(token, value);
            }

            SetParagraphText(paragraph, text);
        }
    }

    // A token that must occupy its own paragraph — newlines in the value become <w:br/> line
    // breaks within that same paragraph, so multi-paragraph narrative text still reads correctly.
    private static void ReplaceGenericMultilineToken(OpenXmlElement root, string token, string value)
    {
        var paragraph = root.Descendants<Paragraph>().FirstOrDefault(p => GetText(p).Contains(token));
        if (paragraph is null)
        {
            return;
        }

        SetParagraphMultilineText(paragraph, value);
    }

    private static void SetParagraphMultilineText(Paragraph paragraph, string value)
    {
        var runProperties = paragraph.Descendants<Run>().FirstOrDefault()?.RunProperties?.CloneNode(true) as RunProperties;
        foreach (var run in paragraph.Elements<Run>().ToList())
        {
            run.Remove();
        }

        var lines = value.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        for (var i = 0; i < lines.Length; i++)
        {
            var run = new Run();
            if (runProperties is not null)
            {
                run.RunProperties = (RunProperties)runProperties.CloneNode(true);
            }

            if (i > 0)
            {
                run.AppendChild(new Break());
            }

            run.AppendChild(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
            paragraph.AppendChild(run);
        }
    }

    // A paragraph containing only this token is cloned once per line of `text`, in the same
    // paragraph/list style as the token paragraph itself (mirrors ReplaceBulletsBetweenHeadings'
    // numbering-restart handling for numbered — not bulleted — lists).
    private static void ReplaceGenericBulletListToken(NumberingDefinitionsPart? numberingPart, Body body, string token, string text)
    {
        var master = body.Descendants<Paragraph>().FirstOrDefault(p => GetText(p).Contains(token));
        if (master is null)
        {
            return;
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
        if (lines.Length == 0)
        {
            lines = [text];
        }

        var freshNumId = TryCreateRestartedNumbering(numberingPart, master);

        foreach (var line in lines)
        {
            var clone = (Paragraph)master.CloneNode(true);
            SetParagraphText(clone, line);
            if (freshNumId.HasValue)
            {
                SetNumberingId(clone, freshNumId.Value);
            }
            master.InsertBeforeSelf(clone);
        }

        master.Remove();
    }

    // Any table row containing a {{Finding.*}} token is the row template — cloned once per
    // included finding, with that finding's simple tokens resolved inside the clone.
    private static void ReplaceGenericFindingRow(Body body, IReadOnlyList<Vulnerability> includedFindings)
    {
        var templateRow = body.Descendants<TableRow>().FirstOrDefault(r => GetText(r).Contains("{{Finding."));
        if (templateRow is null)
        {
            return;
        }

        for (var i = 0; i < includedFindings.Count; i++)
        {
            var clone = (TableRow)templateRow.CloneNode(true);
            ReplaceGenericSimpleTokens(clone, BuildFindingTokens(includedFindings[i], i + 1));
            templateRow.InsertBeforeSelf(clone);
        }

        templateRow.Remove();
    }

    // Everything between a "{{DetailedFindingsStart}}"-only paragraph and a
    // "{{DetailedFindingsEnd}}"-only paragraph is the repeatable unit — cloned once per included
    // finding, with every Finding.* token (simple and multi-line) resolved inside each clone.
    // Both marker paragraphs themselves are removed along with the original template unit.
    private static void ReplaceGenericDetailedFindingsBlock(NumberingDefinitionsPart? numberingPart, Body body, IReadOnlyList<Vulnerability> includedFindings)
    {
        var allParagraphs = TopLevelParagraphs(body);
        var startIdx = allParagraphs.FindIndex(p => GetText(p).Trim() == "{{DetailedFindingsStart}}");
        var endIdx = allParagraphs.FindIndex(p => GetText(p).Trim() == "{{DetailedFindingsEnd}}");
        if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx)
        {
            return;
        }

        var startParagraph = allParagraphs[startIdx];
        var endParagraph = allParagraphs[endIdx];

        var templateUnit = new List<OpenXmlElement>();
        var current = startParagraph.NextSibling();
        while (current is not null && current != endParagraph)
        {
            templateUnit.Add(current);
            current = current.NextSibling();
        }

        for (var i = 0; i < includedFindings.Count; i++)
        {
            var scratch = new Body();
            foreach (var element in templateUnit)
            {
                scratch.AppendChild(element.CloneNode(true));
            }

            var tokens = BuildFindingTokens(includedFindings[i], i + 1);
            ReplaceGenericSimpleTokens(scratch, tokens);
            foreach (var (token, valueSelector) in FindingMultilineTokens)
            {
                ReplaceGenericMultilineToken(scratch, token, valueSelector(includedFindings[i]) ?? "—");
            }
            _ = numberingPart; // reserved for future numbered-list support inside the block

            foreach (var child in scratch.ChildElements.ToList())
            {
                child.Remove();
                endParagraph.InsertBeforeSelf(child);
            }
        }

        foreach (var element in templateUnit)
        {
            element.Remove();
        }
        startParagraph.Remove();
        endParagraph.Remove();
    }
}
