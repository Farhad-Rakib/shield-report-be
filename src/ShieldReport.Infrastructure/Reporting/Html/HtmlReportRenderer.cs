using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HandlebarsDotNet;
using HtmlToOpenXml;
using PuppeteerSharp;
using ShieldReport.Application.Reports.Rendering;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Infrastructure.Reporting.Html;

// HTML report engine. One Handlebars template → HTML snapshot → PDF and Word.
//   PDF:  headless Chromium + paged.js (running header, "Page X of Y", table-of-contents page
//         numbers via target-counter), printed with the template's @page CSS.
//   Word: CSS inlined with PreMailer (HtmlToOpenXml ignores <style>), a few Word-only adjustments
//         (cover colours, explicit image sizes, a real TOC field), converted onto the SELISE template
//         .docx so its styles (Heading 1–4, List Paragraph), header logo and footer are reused.
public sealed partial class HtmlReportRenderer : IHtmlReportRenderer
{
    private const string ResourcePrefix = "ShieldReport.Infrastructure.Reporting.Html.";
    private const string BaseDocxResource = "ShieldReport.Infrastructure.Reporting.Templates.WhiteBoxTesting.docx";

    // Word can't paint page backgrounds, so the cover's white-on-blue text would be invisible.
    // Appended after the template's own CSS so these rules win when PreMailer inlines.
    private const string WordOnlyCss = """
        .cover { background: #ffffff; color: #0b3d6b; height: auto; }
        .cover .title, .cover .subtitle, .cover .meta { color: #0b3d6b; }
        .cover .company { color: #555555; }
        #version-control { page-break-before: always; }
        """;

    private static readonly Assembly ThisAssembly = typeof(HtmlReportRenderer).Assembly;
    // Built-in templates, one per testing type, each following its SELISE Word sample.
    private static readonly Lazy<string> WhiteTemplate = new(() => ReadText("white-box-report.hbs"));
    private static readonly Lazy<string> GrayTemplate = new(() => ReadText("gray-box-report.hbs"));
    private static readonly Lazy<string> BlackTemplate = new(() => ReadText("black-box-report.hbs"));
    private static readonly Lazy<string> PagedJs = new(() => ReadText("paged.polyfill.js"));
    private static readonly Dictionary<string, string> AssetCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] NumberWords =
        ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve",
         "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty"];

    private readonly ChromiumBrowserProvider _browserProvider;
    private readonly IHandlebars _handlebars;

    public HtmlReportRenderer(ChromiumBrowserProvider browserProvider)
    {
        _browserProvider = browserProvider;
        _handlebars = CreateHandlebars();
    }

    public string GetBuiltInTemplate(TestingType testingType) => testingType switch
    {
        TestingType.Gray => GrayTemplate.Value,
        TestingType.Black => BlackTemplate.Value,
        _ => WhiteTemplate.Value,
    };

    public async Task<string> RenderHtmlAsync(ReportRenderModel model, string? customTemplate, CancellationToken cancellationToken = default)
    {
        var chart = await RenderSeverityChartAsync(model.Counts, cancellationToken);
        var template = _handlebars.Compile(string.IsNullOrWhiteSpace(customTemplate)
            ? GetBuiltInTemplate(Enum.TryParse<TestingType>(model.TestingType, out var type) ? type : TestingType.White)
            : customTemplate);
        return template(model with { SeverityChart = chart });
    }

    public Task<string?> ValidateTemplateAsync(string template, CancellationToken cancellationToken = default)
    {
        try
        {
            // Sample data of every testing type, so a branch that only runs for one type is checked too.
            var compiled = _handlebars.Compile(template);
            foreach (var type in Enum.GetValues<TestingType>())
            {
                compiled(SampleModel(type));
            }
            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            return Task.FromResult<string?>(ex.Message);
        }
    }

    public Task<string> RenderSampleAsync(string? template, IReadOnlyDictionary<string, string> custom, TestingType testingType = TestingType.White, CancellationToken cancellationToken = default) =>
        RenderHtmlAsync(SampleModel(testingType) with { Custom = custom }, template, cancellationToken);

    public Task<byte[]> RenderPdfAsync(string html, CancellationToken cancellationToken = default)
    {
        // paged.js lays the document out into pages, then flags completion for us to print.
        var pagedHtml = InsertBeforeBodyEnd(html,
            "<script>window.PagedConfig={auto:true,after:function(){window.__pagedDone=true;}};</script><script>" + PagedJs.Value + "</script>");

        return _browserProvider.WithPageAsync(async page =>
        {
            await page.SetContentAsync(pagedHtml, new NavigationOptions { WaitUntil = [WaitUntilNavigation.Load], Timeout = 60000 });
            await page.WaitForFunctionAsync("() => window.__pagedDone === true", new WaitForFunctionOptions { Timeout = 120000 });
            return await page.PdfDataAsync(new PdfOptions { PreferCSSPageSize = true, PrintBackground = true });
        }, cancellationToken);
    }

    public async Task<byte[]> RenderDocxAsync(string html, CancellationToken cancellationToken = default)
    {
        var wordHtml = PrepareWordHtml(html);

        using var output = new MemoryStream();
        await using (var baseDocx = ThisAssembly.GetManifestResourceStream(BaseDocxResource)
            ?? throw new InvalidOperationException("Base Word template is missing."))
        {
            await baseDocx.CopyToAsync(output, cancellationToken);
        }

        using (var document = WordprocessingDocument.Open(output, true))
        {
            var mainPart = document.MainDocumentPart!;
            var body = mainPart.Document.Body!;

            // Keep the section settings (page size, margins, header/footer references with the
            // SELISE logo), drop the sample content, and add ours.
            var sectionProperties = body.Elements<SectionProperties>().LastOrDefault()?.CloneNode(true);
            body.RemoveAllChildren();

            var converter = new HtmlConverter(mainPart);
            await converter.ParseBody(wordHtml, cancellationToken);

            ReplaceTocMarker(body);

            // HtmlToOpenXml emits "auto" table widths, which Word shrinks to fit the content; the
            // report's tables are all designed full-width.
            foreach (var table in body.Descendants<Table>())
            {
                var properties = table.GetFirstChild<TableProperties>() ?? table.PrependChild(new TableProperties());
                properties.TableWidth = new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct };
            }

            if (sectionProperties is not null)
            {
                body.Append(sectionProperties);
            }

            // Ask Word to refresh fields (the TOC) when the file is opened.
            var settings = mainPart.DocumentSettingsPart?.Settings;
            if (settings is not null)
            {
                settings.RemoveAllChildren<UpdateFieldsOnOpen>();
                settings.Append(new UpdateFieldsOnOpen { Val = true });
                settings.Save();
            }

            mainPart.Document.Save();
        }

        return output.ToArray();
    }

    // ------------------------------------------------------------------ Word pass

    private static string PrepareWordHtml(string html)
    {
        // The running header (logo) and the HTML contents list are PDF-only: the base .docx already
        // carries the logo header, and the list becomes a real TOC field.
        html = RunningHeaderRegex().Replace(html, string.Empty);
        html = WordTocRegex().Replace(html, "<p>[[WORD_TOC]]</p>");
        html = html.Replace("</style>", WordOnlyCss + "</style>", StringComparison.Ordinal);

        html = PreMailer.Net.PreMailer.MoveCssInline(html, removeStyleElements: true, ignoreElements: null, css: null, stripIdAndClassAttributes: false, removeComments: true).Html;

        // Explicit pixel sizes for Word (CSS widths like 100% / mm don't translate reliably).
        return WordWidthImageRegex().Replace(html, match =>
        {
            var tag = match.Value;
            var width = int.Parse(match.Groups["w"].Value, CultureInfo.InvariantCulture);
            var srcMatch = SrcAttributeRegex().Match(tag);
            var (naturalWidth, naturalHeight) = srcMatch.Success ? ImageSize(srcMatch.Groups["src"].Value) : (0, 0);
            var height = naturalWidth > 0 ? (int)Math.Round(width * (double)naturalHeight / naturalWidth) : 0;

            tag = StyleAttributeRegex().Replace(tag, string.Empty);
            tag = tag.Replace("<img", $"<img width=\"{width}\"" + (height > 0 ? $" height=\"{height}\"" : string.Empty), StringComparison.OrdinalIgnoreCase);
            return tag;
        });
    }

    private static void ReplaceTocMarker(Body body)
    {
        var marker = body.Descendants<Paragraph>().FirstOrDefault(p => p.InnerText.Contains("[[WORD_TOC]]", StringComparison.Ordinal));
        if (marker is null)
        {
            return;
        }

        var toc = new Paragraph(
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode(" TOC \\o \"1-2\" \\h \\z \\u ") { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            new Run(new Text("Open in Word and choose Update Field (or accept the update prompt) to build the table of contents.")),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End }));
        marker.InsertAfterSelf(toc);
        marker.Remove();
    }

    // ------------------------------------------------------------------ chart

    private Task<string> RenderSeverityChartAsync(ReportRenderCounts counts, CancellationToken cancellationToken)
    {
        if (counts.Total == 0)
        {
            return Task.FromResult(string.Empty);
        }

        // Rendered once as a PNG so the PDF and Word files show the identical chart.
        (string Label, int Value, string Colour)[] bars =
        [
            ("Critical", counts.Critical, "#c00000"), ("High", counts.High, "#ff0000"), ("Medium", counts.Medium, "#ffc000"),
            ("Low", counts.Low, "#00b050"), ("Info", counts.Informational, "#5b9bd5"),
        ];
        const int width = 700, height = 320, top = 50, baseline = 270, barWidth = 70;
        var max = Math.Max(1, bars.Max(b => b.Value));
        var svg = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' font-family='Arial, Helvetica, sans-serif'>");
        svg.Append($"<rect width='{width}' height='{height}' fill='#ffffff'/>");
        svg.Append($"<text x='{width / 2}' y='28' text-anchor='middle' font-size='17' fill='#333'>Vulnerabilities by Severity</text>");
        svg.Append($"<line x1='40' y1='{baseline}' x2='{width - 20}' y2='{baseline}' stroke='#999'/>");
        for (var i = 0; i < bars.Length; i++)
        {
            var (label, value, colour) = bars[i];
            var barHeight = (baseline - top) * value / (double)max;
            var x = 70 + i * 125;
            svg.Append(CultureInfo.InvariantCulture, $"<rect x='{x}' y='{baseline - barHeight:0.#}' width='{barWidth}' height='{barHeight:0.#}' fill='{colour}'/>");
            svg.Append(CultureInfo.InvariantCulture, $"<text x='{x + barWidth / 2}' y='{baseline - barHeight - 8:0.#}' text-anchor='middle' font-size='15' fill='#333'>{value}</text>");
            svg.Append($"<text x='{x + barWidth / 2}' y='{baseline + 22}' text-anchor='middle' font-size='14' fill='#333'>{label}</text>");
        }

        svg.Append("</svg>");

        return _browserProvider.WithPageAsync(async page =>
        {
            await page.SetViewportAsync(new ViewPortOptions { Width = width, Height = height, DeviceScaleFactor = 2 });
            await page.SetContentAsync($"<html><body style='margin:0;background:#fff'>{svg}</body></html>");
            var png = await page.ScreenshotDataAsync(new ScreenshotOptions { Type = ScreenshotType.Png, OmitBackground = false });
            return "data:image/png;base64," + Convert.ToBase64String(png);
        }, cancellationToken);
    }

    // ------------------------------------------------------------------ Handlebars

    private static IHandlebars CreateHandlebars()
    {
        var hb = Handlebars.Create();

        // {{asset "logo.png"}} → data URI of an embedded template image.
        hb.RegisterHelper("asset", (writer, _, args) => writer.WriteSafeString(Asset(args[0]?.ToString() ?? string.Empty)));

        // {{{text X}}}: user/LLM text, HTML-escaped. Blank lines separate paragraphs; lines starting
        // with "- ", "* " or "• " become a bullet list.
        hb.RegisterHelper("text", (writer, _, args) => writer.WriteSafeString(TextToHtml(args[0]?.ToString())));

        hb.RegisterHelper("formatDate", (writer, _, args) =>
            writer.WriteSafeString(args[0] is DateTime date ? date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "—"));

        hb.RegisterHelper("upper", (writer, _, args) => writer.Write(args[0]?.ToString()?.ToUpperInvariant() ?? string.Empty));

        // {{numberWords 28}} → "twenty-eight (28)" as written in the SELISE templates.
        hb.RegisterHelper("numberWords", (writer, _, args) =>
        {
            var n = Convert.ToInt32(args[0] ?? 0, CultureInfo.InvariantCulture);
            writer.WriteSafeString($"{ToWords(n)} ({n})");
        });

        // {{severityBreakdown Counts}} → "two (2) were critical, thirteen (13) high-risk and five (5) low-risk".
        hb.RegisterHelper("severityBreakdown", (writer, _, args) =>
        {
            if (args[0] is not ReportRenderCounts c)
            {
                return;
            }

            var parts = new List<string>();
            void Add(int n, string label) { if (n > 0) parts.Add($"{ToWords(n)} ({n}) {label}"); }
            Add(c.Critical, "critical");
            Add(c.High, "high-risk");
            Add(c.Medium, "medium-risk");
            Add(c.Low, "low-risk");
            Add(c.Informational, "informational");
            var text = parts.Count <= 1 ? string.Join(string.Empty, parts) : string.Join(", ", parts[..^1]) + " and " + parts[^1];
            writer.WriteSafeString(WebUtility.HtmlEncode(text + (parts.Count == 1 && c.Total == 1 ? " issue was identified" : " issues were identified")));
        });

        hb.RegisterHelper("eq", (_, args) => string.Equals(args[0]?.ToString(), args[1]?.ToString(), StringComparison.Ordinal));
        return hb;
    }

    private static string TextToHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var html = new StringBuilder();
        var inList = false;
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            var bullet = line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("• ", StringComparison.Ordinal);
            if (bullet)
            {
                if (!inList) { html.Append("<ul>"); inList = true; }
                html.Append("<li>").Append(WebUtility.HtmlEncode(line[2..].Trim())).Append("</li>");
                continue;
            }

            if (inList) { html.Append("</ul>"); inList = false; }
            if (line.Length > 0)
            {
                html.Append("<p>").Append(WebUtility.HtmlEncode(line)).Append("</p>");
            }
        }

        if (inList) html.Append("</ul>");
        return html.ToString();
    }

    private static string ToWords(int n)
    {
        if (n < NumberWords.Length) return NumberWords[n];
        string[] tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];
        if (n < 100) return tens[n / 10] + (n % 10 > 0 ? "-" + NumberWords[n % 10] : string.Empty);
        return n.ToString(CultureInfo.InvariantCulture);
    }

    private static string Asset(string name)
    {
        lock (AssetCache)
        {
            if (AssetCache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            using var stream = ThisAssembly.GetManifestResourceStream(ResourcePrefix + "assets." + name)
                ?? throw new InvalidOperationException($"Unknown report asset '{name}'.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var mime = Path.GetExtension(name).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".svg" => "image/svg+xml", _ => "image/png" };
            return AssetCache[name] = $"data:{mime};base64,{Convert.ToBase64String(memory.ToArray())}";
        }
    }

    private static string ReadText(string name)
    {
        using var stream = ThisAssembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new InvalidOperationException($"Missing embedded report resource '{name}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string InsertBeforeBodyEnd(string html, string snippet)
    {
        var index = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return index < 0 ? html + snippet : html.Insert(index, snippet);
    }

    // Reads pixel dimensions from a PNG or JPEG data URI header (for proportional Word image sizes).
    private static (int Width, int Height) ImageSize(string dataUri)
    {
        try
        {
            var comma = dataUri.IndexOf(',');
            if (!dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || comma < 0) return (0, 0);
            var bytes = Convert.FromBase64String(dataUri[(comma + 1)..]);
            if (bytes.Length > 24 && bytes[0] == 0x89 && bytes[1] == 0x50)
            {
                return ((bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19], (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23]);
            }

            for (var i = 2; i + 9 < bytes.Length;)
            {
                if (bytes[i] != 0xFF) { i++; continue; }
                var marker = bytes[i + 1];
                var length = (bytes[i + 2] << 8) | bytes[i + 3];
                if (marker is >= 0xC0 and <= 0xC3)
                {
                    return ((bytes[i + 7] << 8) | bytes[i + 8], (bytes[i + 5] << 8) | bytes[i + 6]);
                }

                i += 2 + length;
            }
        }
        catch
        {
            // Unknown format — let the converter use the image's own size.
        }

        return (0, 0);
    }

    private static ReportRenderModel SampleModel(TestingType testingType = TestingType.White) => new(
        testingType.ToString(), testingType == TestingType.White, testingType == TestingType.Gray, testingType == TestingType.Black, false, "Selise Digital",
        new ReportRenderClient("Sample Client"),
        new ReportRenderEngagement("Sample Engagement", "Sample scope", DateTime.UtcNow.AddDays(-7), DateTime.UtcNow),
        new ReportRenderReport(1, DateTime.UtcNow, true, "Summary", "- Finding", "- Recommendation", "Goals", "Limitations"),
        [new ReportRenderVersion(1, "1.0", DateTime.UtcNow, "analyst@example.com", "—", "Draft")],
        [new ReportRenderScopeRow("App", "Network", "https://example.com", "Completed")],
        new ReportRenderCounts(0, 1, 0, 0, 0),
        [new ReportRenderFinding("VULN-001", "vuln-001", "Sample finding", "High", "7.5", null, "https://example.com", "/login", "Vulnerability Scanning", [], "Description", null, "Fix it", null, null, null, "Not Fixed", 1, [])],
        string.Empty);

    [GeneratedRegex("<div class=\"running-header\">.*?</div>", RegexOptions.Singleline)]
    private static partial Regex RunningHeaderRegex();

    [GeneratedRegex("<ul[^>]*data-word-toc[^>]*>.*?</ul>", RegexOptions.Singleline)]
    private static partial Regex WordTocRegex();

    [GeneratedRegex("<img\\b[^>]*data-word-width=\"(?<w>\\d+)\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex WordWidthImageRegex();

    [GeneratedRegex("\\ssrc=\"(?<src>[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex SrcAttributeRegex();

    [GeneratedRegex("\\sstyle=\"[^\"]*\"", RegexOptions.IgnoreCase)]
    private static partial Regex StyleAttributeRegex();
}
