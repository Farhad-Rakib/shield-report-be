using System.Text.Json;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.BackgroundJobs;

public sealed class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly IBackgroundJobRepository _repository;

    public BackgroundJobQueue(IBackgroundJobRepository repository)
    {
        _repository = repository;
    }

    public async Task EnqueueAsync<TPayload>(string jobType, TPayload payload, int maxAttempts = 3, DateTime? runAfter = null, CancellationToken cancellationToken = default)
    {
        var job = new BackgroundJob(jobType, JsonSerializer.Serialize(payload), maxAttempts, runAfter);
        await _repository.AddAsync(job, cancellationToken);
    }
}
