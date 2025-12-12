using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public sealed class BackgroundJobsModule : IAppModule
{
    public void Configure(IHostApplicationBuilder builder, IList<string> log)
    {
        builder.Services.AddSingleton<IBackgroundJobScheduler, InMemoryJobScheduler>();
        log.Add("module:jobs");
    }
}