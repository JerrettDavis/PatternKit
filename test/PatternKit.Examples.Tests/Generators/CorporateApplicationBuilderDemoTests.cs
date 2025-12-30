using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;
using PatternKit.Examples.Generators.Factories;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Generators;

[Feature("Corporate application builder (fluent, string-free)")]
public sealed class CorporateApplicationBuilderDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Builds and initializes a production app with modules, secrets, and startup tasks")]
    [Fact]
    public async Task CorporateApp_Composes_Modules_And_Startup_Tasks()
    {
        await Given("a production builder with messaging and jobs enabled", CorporateApplicationBuilder () =>
                CorporateApplicationDemo.CreateBuilder()
                    .ForEnvironment(CorporateEnvironment.Production)
                    .EnableMessaging()
                    .EnableJobs()
                    .LoadSecrets()
                    .AddStartupTasks())
            .When<CorporateApp>("build and initialize", async ValueTask<CorporateApp> (b) => await b.BuildAndInitializeAsync())
            .Then("required services are registered", bool (app) =>
                {
                    var services = app.Host.Services;
                    return services.GetService<IMetricsSink>() is not null
                           && services.GetService<INotificationPublisher>() is not null
                           && services.GetService<IBackgroundJobScheduler>() is not null;
                })
            .And("configuration is applied", bool (app) =>
                {
                    var configuration = app.Host.Services.GetRequiredService<IConfiguration>();
                    return configuration["ConnectionStrings:Primary"] == "Server=prod;Database=Corporate;"
                           && configuration["Corporate:FeatureCount"] == "3";
                })
            .And("log captures setup steps", bool (app) =>
                {
                    var expected = new[]
                    {
                        "env:Production",
                        "module:observability",
                        "module:messaging",
                        "module:jobs",
                        "secrets:loaded",
                        "startup:notifications",
                        "startup:jobs"
                    };
                    return expected.All(app.Log.Contains);
                })
            .AssertPassed();
    }
}
