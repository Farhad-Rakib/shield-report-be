using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.ScanTools;

// Typed, validated per-tool options. There is deliberately NO free-form command/argument field:
// the API drives the host Docker daemon (effectively root on the host), so every option is a
// checked value that DockerScanRunner maps onto a known flag.

public sealed record NaabuOptions(
    string PortPreset,          // "top-100" | "top-1000" | "full" | "custom"
    string? CustomPorts,        // "80,443,8000-8100" when PortPreset == "custom"
    int Rate,                   // packets per second
    string ScanType)            // "connect" | "syn" (syn needs raw sockets: runs with NET_RAW)
{
    public static readonly NaabuOptions Default = new("top-1000", null, 1000, "connect");
}

public sealed record NucleiOptions(
    IReadOnlyList<string> Severities,   // subset of info, low, medium, high, critical
    IReadOnlyList<string> IncludeTags,  // empty = all templates
    IReadOnlyList<string> ExcludeTags,
    int RateLimit,                      // requests per second
    int Concurrency)                    // templates in parallel
{
    public static readonly NucleiOptions Default = new(
        ["info", "low", "medium", "high", "critical"], [], ["dos", "intrusive", "fuzz"], 150, 25);
}

// Reconnaissance (reconftw). Modules = the checked boxes on the Scan Tools page; each maps to
// explicit reconftw.cfg switches (ReconftwModules.ConfigFlags), everything else stays off.
// Mode/DnsBruteforce/Permutations/Screenshots/TakeoverCheck are the pre-checkbox shape, still read
// from older saved settings and queued scans (see EffectiveModules) but never written.
public sealed record ReconftwOptions(
    IReadOnlyList<string>? Modules,
    int Threads,                                                                        // httpx / resolver thread budget
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Mode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? DnsBruteforce = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Permutations = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Screenshots = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? TakeoverCheck = null)
{
    public static readonly ReconftwOptions Default = new(ReconftwModules.DefaultSelection, 20);

    public IReadOnlyList<string> EffectiveModules()
    {
        if (Modules is not null)
        {
            return Modules;
        }

        var legacy = new List<string> { "passive", "crt", "webprobe" };
        if (DnsBruteforce == true) legacy.Add("bruteforce");
        if (Permutations == true) legacy.Add("permutations");
        if (Screenshots == true) legacy.Add("screenshots");
        if (TakeoverCheck != false) legacy.Add("takeover");
        if (Mode == "full") legacy.AddRange(["osint", "portscan", "urls", "js", "waf", "nuclei"]);
        return legacy;
    }

    // Copy in the current shape (legacy fields dropped).
    public ReconftwOptions Normalized() => new(EffectiveModules().Distinct().ToList(), Threads);
}

public sealed record ReconftwModule(string Key, string Group, string Label, string Description, bool Heavy, string[] ConfigFlags);

// The selectable reconnaissance modules. Order = display order. ConfigFlags are reconftw.cfg
// booleans set to true when the module is checked (and false otherwise).
public static class ReconftwModules
{
    public static readonly IReadOnlyList<ReconftwModule> All =
    [
        new("passive", "Subdomain discovery", "Passive sources", "Subdomains from public passive sources (no traffic to the target).", false, ["SUBPASSIVE"]),
        new("crt", "Subdomain discovery", "Certificate transparency", "Subdomains from certificate transparency logs.", false, ["SUBCRT"]),
        new("bruteforce", "Subdomain discovery", "DNS brute-force", "Resolve a wordlist of common subdomain names.", true, ["SUBBRUTE"]),
        new("permutations", "Subdomain discovery", "Permutations", "Generate and resolve variations of found subdomains.", true, ["SUBPERMUTE", "SUBREGEXPERMUTE"]),
        new("scraping", "Subdomain discovery", "Web scraping", "Extract subdomains by crawling discovered sites.", false, ["SUBSCRAPING"]),
        new("analytics", "Subdomain discovery", "Analytics IDs", "Find related subdomains that share Google Analytics IDs.", false, ["SUBANALYTICS"]),
        new("zonetransfer", "Subdomain discovery", "Zone transfer", "Check DNS servers for open zone transfers.", false, ["ZONETRANSFER"]),
        new("takeover", "Subdomain discovery", "Takeover check", "Flag subdomains that can be taken over.", false, ["SUBTAKEOVER"]),
        new("s3buckets", "Subdomain discovery", "Cloud buckets", "Look for misconfigured S3/cloud storage buckets.", false, ["S3BUCKETS"]),

        new("webprobe", "Web detection", "Web probe (80/443)", "Find which hosts serve a website on the standard ports.", false, ["WEBPROBESIMPLE"]),
        new("webprobe-full", "Web detection", "Web probe (uncommon ports)", "Also probe a large list of uncommon web ports.", true, ["WEBPROBEFULL"]),
        new("screenshots", "Web detection", "Screenshots", "Capture screenshots of discovered websites (headless browser, memory heavy).", true, ["WEBSCREENSHOT"]),
        new("virtualhosts", "Web detection", "Virtual hosts", "Fuzz the Host header for hidden virtual hosts.", true, ["VIRTUALHOSTS"]),

        new("osint", "OSINT & hosts", "OSINT", "Dorks, emails, domain/IP info, leaked API keys and spoofable domains.", false, ["OSINT"]),
        new("favicon", "OSINT & hosts", "Favicon lookup", "Find related hosts that share the site's favicon.", false, ["FAVICON"]),
        new("portscan", "OSINT & hosts", "Port scan", "Port and service scan of discovered hosts (nmap).", true, ["PORTSCANNER"]),
        new("geo", "OSINT & hosts", "Geolocation", "Geolocate discovered IP addresses.", false, ["GEO_INFO"]),
        new("cdn", "OSINT & hosts", "CDN detection", "Mark which IPs belong to a CDN.", false, ["CDN_IP"]),

        new("waf", "Web analysis", "WAF detection", "Detect web application firewalls.", false, ["WAF_DETECTION"]),
        new("nuclei", "Web analysis", "Template checks", "Run vulnerability templates against discovered sites.", true, ["NUCLEICHECK"]),
        new("urls", "Web analysis", "URL collection", "Collect URLs from archives and crawling, sorted by pattern and extension.", true, ["URL_CHECK", "URL_GF", "URL_EXT"]),
        new("js", "Web analysis", "JavaScript analysis", "Find endpoints and secrets in JavaScript files.", true, ["JSCHECKS"]),
        new("fuzz", "Web analysis", "Directory fuzzing", "Brute-force common paths on each site.", true, ["FUZZ"]),
        new("cms", "Web analysis", "CMS scan", "Identify and scan content management systems.", true, ["CMS_SCANNER"]),
        new("iis", "Web analysis", "IIS short names", "Check IIS servers for the short-name disclosure.", false, ["IIS_SHORTNAME"]),
        new("wordlists", "Web analysis", "Wordlists", "Build target-specific wordlists and password dictionaries.", false, ["WORDLIST", "ROBOTSWORDLIST", "PASSWORD_DICT"]),
    ];

    public static readonly IReadOnlyList<string> DefaultSelection = ["passive", "crt", "takeover", "webprobe"];

    // Modules covered by reconftw's subdomain mode (-s). Anything else needs the full recon flow (-r),
    // which still only runs the modules switched on in the generated config.
    public static readonly IReadOnlySet<string> SubdomainModeModules = new HashSet<string>
    {
        "passive", "crt", "bruteforce", "permutations", "scraping", "analytics", "zonetransfer", "takeover", "s3buckets",
        "webprobe", "webprobe-full", "screenshots",
    };

    public static readonly IReadOnlySet<string> Keys = All.Select(m => m.Key).ToHashSet();
}

public static partial class ScanToolOptionsValidator
{
    private static readonly HashSet<string> NaabuPresets = ["top-100", "top-1000", "full", "custom"];
    private static readonly HashSet<string> NaabuScanTypes = ["connect", "syn"];
    private static readonly HashSet<string> NucleiSeverities = ["info", "low", "medium", "high", "critical"];

    [GeneratedRegex(@"^\d{1,5}(-\d{1,5})?(,\d{1,5}(-\d{1,5})?)*$")]
    private static partial Regex PortListRegex();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9._-]{0,39}$")]
    private static partial Regex TagRegex();

    public static IReadOnlyList<string> Validate(NaabuOptions o)
    {
        var errors = new List<string>();
        if (!NaabuPresets.Contains(o.PortPreset)) errors.Add("Port preset must be top-100, top-1000, full or custom.");
        if (o.PortPreset == "custom")
        {
            if (string.IsNullOrWhiteSpace(o.CustomPorts) || !PortListRegex().IsMatch(o.CustomPorts))
            {
                errors.Add("Custom ports must look like 80,443,8000-8100.");
            }
            else if (o.CustomPorts.Split(',', '-').Any(p => int.Parse(p) is < 1 or > 65535))
            {
                errors.Add("Ports must be between 1 and 65535.");
            }
        }

        if (o.Rate is < 10 or > 10000) errors.Add("Rate must be between 10 and 10000 packets/second.");
        if (!NaabuScanTypes.Contains(o.ScanType)) errors.Add("Scan type must be connect or syn.");
        return errors;
    }

    public static IReadOnlyList<string> Validate(NucleiOptions o)
    {
        var errors = new List<string>();
        if (o.Severities.Count == 0 || o.Severities.Any(s => !NucleiSeverities.Contains(s))) errors.Add("Pick at least one severity from info, low, medium, high, critical.");
        if (o.IncludeTags.Concat(o.ExcludeTags).Any(t => !TagRegex().IsMatch(t))) errors.Add("Tags may only contain lowercase letters, digits, '.', '_' and '-'.");
        if (o.IncludeTags.Count + o.ExcludeTags.Count > 50) errors.Add("At most 50 tags in total.");
        if (o.RateLimit is < 1 or > 1000) errors.Add("Rate limit must be between 1 and 1000 requests/second.");
        if (o.Concurrency is < 1 or > 100) errors.Add("Concurrency must be between 1 and 100.");
        return errors;
    }

    public static IReadOnlyList<string> Validate(ReconftwOptions o)
    {
        var errors = new List<string>();
        var modules = o.EffectiveModules();
        var unknown = modules.Where(m => !ReconftwModules.Keys.Contains(m)).ToList();
        if (unknown.Count > 0) errors.Add($"Unknown reconnaissance module(s): {string.Join(", ", unknown)}.");
        if (!modules.Any(m => m is "passive" or "crt" or "bruteforce" or "scraping" or "analytics"))
        {
            errors.Add("Select at least one subdomain discovery source (passive sources, certificate transparency, DNS brute-force, web scraping or analytics IDs).");
        }
        if (o.Threads is < 1 or > 200) errors.Add("Threads must be between 1 and 200.");
        return errors;
    }
}

// Everything DockerScanRunner needs for one run, resolved when the scan is queued and stored on
// the scan (Scan.ToolSettingsJson) so a settings change never alters an already-queued scan.
public sealed record ScanToolRuntimeSettings(
    ScanTool Tool,
    string Image,
    int TimeoutMinutes,
    int MemoryMb,
    double Cpus,
    int PidsLimit,
    NaabuOptions? Naabu,
    NucleiOptions? Nuclei,
    ReconftwOptions? Reconftw,
    CustomToolOptions? Custom = null);

// Snapshot of a CustomScanTool for one run (Tool == Custom).
public sealed record CustomToolOptions(
    long Id,
    string Name,
    string ClientLabel,
    IReadOnlyList<string> Arguments,   // may contain {target} {host} {url} {port}
    string OutputFormat,               // lines | nuclei-jsonl | raw
    string DefaultSeverity,
    bool RunAsRoot);

// Deployment-owned values (not editable in the UI): pinned image references and process limits.
// Implemented in ShieldReport.Scanning from configuration (Scanning:Tools:{Tool}:Image/PidsLimit).
public interface IScanToolDeploymentSettings
{
    string GetImage(ScanTool tool);
    int GetPidsLimit(ScanTool tool);
}
