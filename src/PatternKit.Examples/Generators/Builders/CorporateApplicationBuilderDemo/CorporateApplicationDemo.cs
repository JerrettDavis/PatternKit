using Microsoft.Extensions.DependencyInjection;

namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public static class CorporateApplicationDemo
{
    public static ValueTask<CorporateApp> BuildAsync(string environment, params string[] features)
    {
        return CorporateApplication.BuildAsync(async builder =>
        {
            builder.With(state =>
            {
                state.Builder.Environment.EnvironmentName = environment;
                state.Log.Add($"env:{environment}");
                return state;
            });

            builder.With(state =>
            {
                state.Modules.Add(new ObservabilityModule());
                return state;
            });

            builder.With(state =>
            {
                if (features.Contains("messaging", StringComparer.OrdinalIgnoreCase))
                {
                    state.Modules.Add(new MessagingModule());
                }

                if (features.Contains("jobs", StringComparer.OrdinalIgnoreCase))
                {
                    state.Modules.Add(new BackgroundJobsModule());
                }

                return state;
            });

            builder.With(state =>
            {
                state.Customizations.Add(hostBuilder =>
                {
                    hostBuilder.Configuration["Corporate:FeatureCount"] = state.Modules.Count.ToString();
                });
                return state;
            });

            builder.WithAsync(async state =>
            {
                // Simulate async configuration (e.g., secrets from a vault).
                var connectionString = await SecretsProvider.GetConnectionStringAsync(environment).ConfigureAwait(false);
                state.Customizations.Add(hostBuilder =>
                {
                    hostBuilder.Configuration["ConnectionStrings:Primary"] = connectionString;
                });
                state.Log.Add("secrets:loaded");
                return state;
            });

            builder.With(state =>
            {
                state.StartupTasks.Add(async services =>
                {
                    if (services.GetService<INotificationPublisher>() is { } publisher)
                    {
                        await publisher.PublishAsync("system", $"boot:{environment}");
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

            builder.Require(state => state.Modules.Count == 0 ? "At least one module must be registered." : null);
            
            await Task.CompletedTask.ConfigureAwait(false);
        });
    }
}