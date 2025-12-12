using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PatternKit.Examples.Generators.Factories;

namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public sealed class ObservabilityModule : IAppModule
{
    public void Configure(IHostApplicationBuilder builder, IList<string> log)
    {
        builder.Services.AddSingleton<IMetricsSink, ConsoleMetricsSink>();
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
        log.Add("module:observability");
    }
}