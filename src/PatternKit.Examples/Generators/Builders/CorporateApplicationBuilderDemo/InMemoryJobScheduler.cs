using Microsoft.Extensions.Logging;

namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public sealed class InMemoryJobScheduler(ILogger<InMemoryJobScheduler> logger) : IBackgroundJobScheduler
{
    public Task ScheduleAsync(string jobName, TimeSpan cadence, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Scheduled {Job} every {Cadence}", jobName, cadence);
        return Task.CompletedTask;
    }
}