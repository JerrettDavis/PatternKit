using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;
using PatternKit.Examples.Generators.Factories;

namespace PatternKit.Examples.Tests.Generators;

public class CorporateApplicationBuilderDemoTests
{
    [Fact]
    public async Task CorporateApp_Composes_Modules_And_Startup_Tasks()
    {
        var app = await CorporateApplicationDemo.BuildAsync("Production", "messaging", "jobs");

        await app.InitializeAsync();

        var services = app.Host.Services;
        Assert.NotNull(services.GetService<IMetricsSink>());
        Assert.NotNull(services.GetService<INotificationPublisher>());
        Assert.NotNull(services.GetService<IBackgroundJobScheduler>());

        var configuration = services.GetRequiredService<IConfiguration>();
        Assert.Equal("Server=prod;Database=Corporate;", configuration["ConnectionStrings:Primary"]);
        Assert.Equal("3", configuration["Corporate:FeatureCount"]);

        Assert.Contains("env:Production", app.Log);
        Assert.Contains("module:observability", app.Log);
        Assert.Contains("module:messaging", app.Log);
        Assert.Contains("module:jobs", app.Log);
        Assert.Contains("secrets:loaded", app.Log);
        Assert.Contains("startup:notifications", app.Log);
        Assert.Contains("startup:jobs", app.Log);
    }
}
