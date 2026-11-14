namespace ShieldReport.Scanning.Queueing;

// Wakes the scan worker's tool loops immediately when a scan is queued or a chain step finishes,
// instead of waiting for the next poll. Purely an optimization: the database is the queue, and
// the loops still poll on their own, so a missed signal only costs one poll interval.
public sealed class ScanQueueSignal
{
    private TaskCompletionSource _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Notify()
    {
        Interlocked.Exchange(ref _signal, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
    }

    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            await Volatile.Read(ref _signal).Task.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            // Poll interval elapsed — the caller polls the database again.
        }
    }
}
