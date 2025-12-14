using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public enum CorporateEnvironment
{
    Development,
    Staging,
    Production
}

public static class CorporateApplicationBuilderExtensions
{
    public static CorporateApplicationBuilder ForEnvironment(this CorporateApplicationBuilder builder, CorporateEnvironment environment)
        => builder.With(state =>
        {
            var envName = environment switch
            {
                CorporateEnvironment.Production => "Production",
                CorporateEnvironment.Staging => "Staging",
                _ => "Development"
            };

            state.Builder.Environment.EnvironmentName = envName;
            state.Log.Add($"env:{envName}");
            return state;
        });

    public static CorporateApplicationBuilder EnableObservability(this CorporateApplicationBuilder builder)
        => builder.With(state =>
        {
            if (!state.Modules.OfType<ObservabilityModule>().Any())
            {
                state.Modules.Add(new ObservabilityModule());
                state.Log.Add("module:observability");
            }

            return state;
        });

    public static CorporateApplicationBuilder EnableMessaging(this CorporateApplicationBuilder builder)
        => builder.With(state =>
        {
            if (!state.Modules.OfType<MessagingModule>().Any())
            {
                state.Modules.Add(new MessagingModule());
                state.Log.Add("module:messaging");
            }

            return state;
        });

    public static CorporateApplicationBuilder EnableJobs(this CorporateApplicationBuilder builder)
        => builder.With(state =>
        {
            if (!state.Modules.OfType<BackgroundJobsModule>().Any())
            {
                state.Modules.Add(new BackgroundJobsModule());
                state.Log.Add("module:jobs");
            }

            return state;
        });

    public static CorporateApplicationBuilder ConfigureFeatureCount(this CorporateApplicationBuilder builder)
        => builder.With(state =>
        {
            state.Customizations.Add(hostBuilder =>
            {
                hostBuilder.Configuration["Corporate:FeatureCount"] = state.Modules.Count.ToString();
            });
            return state;
        });

    public static CorporateApplicationBuilder LoadSecrets(this CorporateApplicationBuilder builder)
        => builder.WithAsync(async state =>
        {
            var envName = state.Builder.Environment.EnvironmentName ?? "Development";
            var environment = ParseEnvironment(envName);
            var connectionString = await SecretsProvider.GetConnectionStringAsync(environment).ConfigureAwait(false);
            state.Customizations.Add(hostBuilder =>
            {
                hostBuilder.Configuration["ConnectionStrings:Primary"] = connectionString;
            });
            state.Log.Add("secrets:loaded");
            return state;
        });

    public static CorporateApplicationBuilder AddStartupTasks(this CorporateApplicationBuilder builder)
        => builder.With(state =>
        {
            state.StartupTasks.Add(async services =>
            {
                if (services.GetService<INotificationPublisher>() is { } publisher)
                {
                    await publisher.PublishAsync("system", $"boot:{state.Builder.Environment.EnvironmentName}");
                    state.Log.Add("startup:notifications");
                }
            });

            state.StartupTasks.Add(async services =>
            {
                if (services.GetService<IBackgroundJobScheduler>() is { } scheduler)
                {
                    await scheduler.ScheduleAsync("cleanup", TimeSpan.FromMinutes(30));
                    state.Log.Add("startup:jobs");
                }
            });

            return state;
        });

    public static CorporateApplicationBuilder RequireModules(this CorporateApplicationBuilder builder)
        => builder.Require(state => state.Modules.Count == 0 ? "At least one module must be registered." : null);

    public static async ValueTask<CorporateApp> BuildAndInitializeAsync(this CorporateApplicationBuilder builder)
    {
        var app = await builder.BuildAsync().ConfigureAwait(false);
        await app.InitializeAsync().ConfigureAwait(false);
        return app;
    }

    private static CorporateEnvironment ParseEnvironment(string env)
        => env switch
        {
            "Production" => CorporateEnvironment.Production,
            "Staging" => CorporateEnvironment.Staging,
            _ => CorporateEnvironment.Development
        };
}
