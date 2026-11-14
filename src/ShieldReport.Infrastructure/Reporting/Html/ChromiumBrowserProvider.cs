using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace ShieldReport.Infrastructure.Reporting.Html;

// One shared headless Chromium for report rendering (launching per render costs ~1s and ~150 MB).
// Relaunched automatically if it crashed. Concurrent renders are capped so a burst of report
// generations can't exhaust memory on a small host.
//
// Every page is isolated: request interception aborts anything that isn't a data:/about: URL, so a
// template (or text that slipped through) can never make the server fetch internal or external
// URLs (SSRF) — all images are embedded as data URIs by the renderer.
public sealed class ChromiumBrowserProvider : IAsyncDisposable
{
    private static readonly string[] CandidatePaths =
    [
        "/usr/bin/chromium",                 // Debian/Ubuntu package (API Docker image)
        "/usr/bin/chromium-browser",
        "/usr/bin/google-chrome",
        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",   // local development
        "/Applications/Chromium.app/Contents/MacOS/Chromium",
    ];

    private readonly IConfiguration _configuration;
    private readonly ILogger<ChromiumBrowserProvider> _logger;
    private readonly SemaphoreSlim _launchLock = new(1, 1);
    private readonly SemaphoreSlim _renderSlots = new(2, 2);
    private IBrowser? _browser;

    public ChromiumBrowserProvider(IConfiguration configuration, ILogger<ChromiumBrowserProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    // Runs `work` on a fresh isolated page, holding one of the render slots.
    public async Task<T> WithPageAsync<T>(Func<IPage, Task<T>> work, CancellationToken cancellationToken)
    {
        await _renderSlots.WaitAsync(cancellationToken);
        try
        {
            var browser = await GetBrowserAsync(cancellationToken);
            await using var page = await browser.NewPageAsync();
            await page.SetRequestInterceptionAsync(true);
            page.Request += async (_, e) =>
            {
                var url = e.Request.Url;
                if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                {
                    await e.Request.ContinueAsync();
                }
                else
                {
                    _logger.LogWarning("Blocked network request from report renderer: {Url}", url.Length > 200 ? url[..200] : url);
                    await e.Request.AbortAsync();
                }
            };

            return await work(page);
        }
        finally
        {
            _renderSlots.Release();
        }
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is { IsConnected: true })
        {
            return _browser;
        }

        await _launchLock.WaitAsync(cancellationToken);
        try
        {
            if (_browser is { IsConnected: true })
            {
                return _browser;
            }

            if (_browser is not null)
            {
                try { await _browser.DisposeAsync(); } catch { /* already gone */ }
            }

            var executable = ResolveExecutable();
            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = executable,
                // --no-sandbox: the API runs as root inside its container, where Chromium's own
                // sandbox can't start; the page-level network block above is the containment.
                Args = ["--no-sandbox", "--disable-gpu", "--disable-dev-shm-usage", "--font-render-hinting=none"],
            });
            _logger.LogInformation("Started headless Chromium for report rendering: {Executable}", executable);
            return _browser;
        }
        finally
        {
            _launchLock.Release();
        }
    }

    private string ResolveExecutable()
    {
        var configured = _configuration["Reporting:ChromiumPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return CandidatePaths.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("Chromium was not found for PDF rendering. Install it (the API Docker image includes it) or set Reporting:ChromiumPath.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            try { await _browser.DisposeAsync(); } catch { /* shutting down */ }
        }
    }
}
