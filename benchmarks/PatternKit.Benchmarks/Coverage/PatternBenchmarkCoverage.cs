using PatternKit.Examples.ProductionReadiness;

namespace PatternKit.Benchmarks.Coverage;

public static class PatternBenchmarkCoverage
{
    private static readonly Lazy<IReadOnlyList<PatternBenchmarkRoute>> LazyRoutes = new(CreateRoutes);

    public static IReadOnlyList<PatternBenchmarkRoute> Routes => LazyRoutes.Value;

    public static IEnumerable<PatternBenchmarkRoute> FluentConstructionRoutes => Routes
        .Where(static route => route.Route == BenchmarkRoute.Fluent && route.Phase == BenchmarkPhase.Construction);

    public static IEnumerable<PatternBenchmarkRoute> FluentExecutionRoutes => Routes
        .Where(static route => route.Route == BenchmarkRoute.Fluent && route.Phase == BenchmarkPhase.Execution);

    public static IEnumerable<PatternBenchmarkRoute> SourceGeneratedConstructionRoutes => Routes
        .Where(static route => route.Route == BenchmarkRoute.SourceGenerated && route.Phase == BenchmarkPhase.Construction);

    public static IEnumerable<PatternBenchmarkRoute> SourceGeneratedExecutionRoutes => Routes
        .Where(static route => route.Route == BenchmarkRoute.SourceGenerated && route.Phase == BenchmarkPhase.Execution);

    public static IEnumerable<PatternBenchmarkRoute> HostingIntegrationRoutes => Routes
        .Where(static route => route.Route == BenchmarkRoute.HostingIntegration);

    private static IReadOnlyList<PatternBenchmarkRoute> CreateRoutes()
    {
        var catalog = new PatternKitPatternCatalog();
        var hostingCatalog = new PatternKitHostingIntegrationCatalog();
        var hostingIntegrations = hostingCatalog.Integrations
            .Where(static integration => integration.Kind == PatternHostingIntegrationKind.ReusableHostingExtension)
            .ToDictionary(static integration => integration.PatternName, StringComparer.Ordinal);
        var routes = new List<PatternBenchmarkRoute>(catalog.Patterns.Count * 4);

        foreach (var pattern in catalog.Patterns)
        {
            var implementation = pattern.Implementation;
            routes.Add(new(
                pattern.Name,
                pattern.Family,
                BenchmarkRoute.Fluent,
                BenchmarkPhase.Construction,
                implementation.FluentSourcePath,
                implementation.FluentTestPath,
                implementation.FluentDocumentationPath));
            routes.Add(new(
                pattern.Name,
                pattern.Family,
                BenchmarkRoute.Fluent,
                BenchmarkPhase.Execution,
                implementation.ExampleSourcePath,
                implementation.ExampleTestPath,
                implementation.ExampleDocumentationPath));

            if (!implementation.HasSourceGeneratedPath)
                throw new InvalidOperationException($"{pattern.Name} does not have a source-generated path to benchmark.");

            routes.Add(new(
                pattern.Name,
                pattern.Family,
                BenchmarkRoute.SourceGenerated,
                BenchmarkPhase.Construction,
                implementation.GeneratorSourcePath!,
                implementation.GeneratorTestPath!,
                implementation.GeneratorDocumentationPath!));
            routes.Add(new(
                pattern.Name,
                pattern.Family,
                BenchmarkRoute.SourceGenerated,
                BenchmarkPhase.Execution,
                implementation.ExampleSourcePath,
                implementation.ExampleTestPath,
                implementation.ExampleDocumentationPath));

            if (hostingIntegrations.TryGetValue(pattern.Name, out var hostingIntegration))
            {
                routes.Add(new(
                    pattern.Name,
                    pattern.Family,
                    BenchmarkRoute.HostingIntegration,
                    BenchmarkPhase.Construction,
                    "src/PatternKit.Hosting.Extensions/DependencyInjection/PatternKitServiceCollectionExtensions.cs",
                    hostingIntegration.TestPath,
                    hostingIntegration.DocumentationPath));
            }
        }

        return routes;
    }
}
