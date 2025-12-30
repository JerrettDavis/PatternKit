namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public static class CorporateApplicationDemo
{
    public static CorporateApplicationBuilder CreateBuilder()
        => CorporateApplicationBuilder.New()
            .EnableObservability()
            .ConfigureFeatureCount();

    public static ValueTask<CorporateApp> BuildProductionAsync()
        => CreateBuilder()
            .ForEnvironment(CorporateEnvironment.Production)
            .EnableMessaging()
            .EnableJobs()
            .LoadSecrets()
            .AddStartupTasks()
            .RequireModules()
            .BuildAndInitializeAsync();
}
