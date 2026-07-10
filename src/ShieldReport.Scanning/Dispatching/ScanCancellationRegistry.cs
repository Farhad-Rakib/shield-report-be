using System.Collections.Concurrent;
using ShieldReport.Application.Common.Interfaces.Services;

namespace ShieldReport.Scanning.Dispatching;

// Singleton — one registry shared by every ScanWorkerBackgroundService tool loop and every
// scoped ScanService instance created per request, so a Cancel API call can reach whichever
// in-flight run owns a given scanId regardless of which DI scope registered it.
public sealed class ScanCancellationRegistry : IScanCancellationRegistry
{
    private readonly ConcurrentDictionary<long, CancellationTokenSource> _tokens = new();

    public CancellationTokenSource Register(long scanId, CancellationToken linkedTo)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(linkedTo);
        _tokens[scanId] = cts;
        return cts;
    }

    public bool TryCancel(long scanId)
    {
        if (!_tokens.TryGetValue(scanId, out var cts))
        {
            return false;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Run finished and unregistered between the lookup and the cancel — fine, there's
            // nothing left to stop.
            return false;
        }

        return true;
    }

    public void Unregister(long scanId)
    {
        if (_tokens.TryRemove(scanId, out var cts))
        {
            cts.Dispose();
        }
    }
}
