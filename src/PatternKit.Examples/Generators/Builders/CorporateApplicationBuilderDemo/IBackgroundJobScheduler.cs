namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public interface IBackgroundJobScheduler
{
    Task ScheduleAsync(string jobName, TimeSpan cadence, CancellationToken cancellationToken = default);
}