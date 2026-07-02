using System.Threading.Channels;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Scanning.Queueing;

// Singleton — one unbounded channel per ScanTool so scans of different tools never queue
// behind each other; only scans of the same tool do. Built once from Enum.GetValues so a
// future tool just needs the enum extended, no code change here.
public sealed class PerToolScanWorkQueue : IScanWorkQueue
{
    private readonly Dictionary<ScanTool, Channel<long>> _channels =
        Enum.GetValues<ScanTool>().ToDictionary(tool => tool, _ => Channel.CreateUnbounded<long>());

    public async ValueTask EnqueueAsync(long scanId, ScanTool tool, CancellationToken cancellationToken = default)
    {
        await _channels[tool].Writer.WriteAsync(scanId, cancellationToken);
    }

    public IAsyncEnumerable<long> ReadAllAsync(ScanTool tool, CancellationToken cancellationToken = default)
    {
        return _channels[tool].Reader.ReadAllAsync(cancellationToken);
    }
}
