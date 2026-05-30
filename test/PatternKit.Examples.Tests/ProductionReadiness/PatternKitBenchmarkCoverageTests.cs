using PatternKit.Benchmarks.Coverage;
using PatternKit.Examples.ProductionReadiness;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ProductionReadiness;

[Feature("Benchmark coverage")]
public sealed class PatternKitBenchmarkCoverageTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Every catalog pattern has fluent and generated benchmark routes")]
    [Fact]
    public Task Every_Catalog_Pattern_Has_Fluent_And_Generated_Benchmark_Routes()
        => Given("the production pattern catalog", () => new PatternKitPatternCatalog())
            .When("comparing catalog patterns with benchmark routes", catalog => new
            {
                CatalogPatterns = catalog.Patterns.Select(static pattern => pattern.Name).OrderBy(static name => name).ToArray(),
                BenchmarkPatterns = PatternBenchmarkCoverage.Routes.Select(static route => route.PatternName).Distinct().OrderBy(static name => name).ToArray(),
                Routes = PatternBenchmarkCoverage.Routes
            })
            .Then("every catalog pattern is represented in the benchmark matrix", ctx =>
                ScenarioExpect.Equal(ctx.CatalogPatterns, ctx.BenchmarkPatterns))
            .And("every pattern has fluent construction execution and generated construction execution routes", ctx =>
            {
                var missing = ctx.CatalogPatterns
                    .SelectMany(patternName =>
                    {
                        var routes = ctx.Routes.Where(route => route.PatternName == patternName).ToArray();
                        return new[]
                            {
                                HasRoute(routes, BenchmarkRoute.Fluent, BenchmarkPhase.Construction) ? null : $"{patternName} missing fluent construction benchmark",
                                HasRoute(routes, BenchmarkRoute.Fluent, BenchmarkPhase.Execution) ? null : $"{patternName} missing fluent execution benchmark",
                                HasRoute(routes, BenchmarkRoute.SourceGenerated, BenchmarkPhase.Construction) ? null : $"{patternName} missing generated construction benchmark",
                                HasRoute(routes, BenchmarkRoute.SourceGenerated, BenchmarkPhase.Execution) ? null : $"{patternName} missing generated execution benchmark"
                            }
                            .Where(static issue => issue is not null)
                            .Select(static issue => issue!);
                    })
                    .ToArray();

                ScenarioExpect.Empty(missing);
            })
            .AssertPassed();

    [Scenario("Reusable hosting integrations have benchmark routes")]
    [Fact]
    public Task Reusable_Hosting_Integrations_Have_Benchmark_Routes()
        => Given("the hosting integration catalog and benchmark routes", () => new
        {
            Hosting = new PatternKitHostingIntegrationCatalog().Integrations
                .Where(static integration => integration.Kind == PatternHostingIntegrationKind.ReusableHostingExtension)
                .Select(static integration => integration.PatternName)
                .OrderBy(static name => name)
                .ToArray(),
            BenchmarkRoutes = PatternBenchmarkCoverage.HostingIntegrationRoutes
                .Select(static route => route.PatternName)
                .OrderBy(static name => name)
                .ToArray()
        })
            .Then("each reusable IServiceCollection integration has a reportable BenchmarkDotNet route", ctx =>
                ScenarioExpect.Equal(ctx.Hosting, ctx.BenchmarkRoutes))
            .AssertPassed();

    [Scenario("Every generator source is represented in the benchmark matrix")]
    [Fact]
    public Task Every_Generator_Source_Is_Represented_In_The_Benchmark_Matrix()
        => Given("the repository root and generator benchmark routes", () => new
        {
            RepositoryRoot = FindRepoRoot(),
            BenchmarkRoutes = GeneratorBenchmarkCoverage.Routes
        })
            .When("reading generator source files", ctx => new
            {
                SourceFiles = Directory
                    .EnumerateFiles(Path.Combine(ctx.RepositoryRoot, "src", "PatternKit.Generators"), "*Generator.cs", SearchOption.AllDirectories)
                    .Where(static path => !Path.GetFileName(path).StartsWith("Generate", StringComparison.Ordinal))
                    .Select(path => Path.GetRelativePath(ctx.RepositoryRoot, path).Replace('\\', '/'))
                    .OrderBy(static path => path)
                    .ToArray(),
                BenchmarkFiles = ctx.BenchmarkRoutes
                    .Select(static route => route.SourcePath)
                    .OrderBy(static path => path)
                    .ToArray()
            })
            .Then("the benchmark matrix includes every generator source file", ctx =>
                ScenarioExpect.Equal(ctx.SourceFiles, ctx.BenchmarkFiles))
            .AssertPassed();

    [Scenario("Published benchmark results include every catalog pattern")]
    [Fact]
    public Task Published_Benchmark_Results_Include_Every_Catalog_Pattern()
        => Given("the benchmark results guide and production pattern catalog", () => new
        {
            ResultsGuide = File.ReadAllText(Path.Combine(FindRepoRoot(), "docs", "guides", "benchmark-results.md")),
            Catalog = new PatternKitPatternCatalog()
        })
            .When("checking catalog names against the published results", ctx => new
            {
                ctx.ResultsGuide,
                MissingPatterns = ctx.Catalog.Patterns
                    .Select(static pattern => pattern.Name)
                    .Where(patternName => !ctx.ResultsGuide.Contains($"| {patternName} |", StringComparison.Ordinal))
                    .OrderBy(static patternName => patternName)
                    .ToArray()
            })
            .Then("every catalog pattern appears in the benchmark results matrix", ctx =>
                ScenarioExpect.Empty(ctx.MissingPatterns))
            .And("the guide publishes the route result total", ctx =>
                ScenarioExpect.Contains("444 pattern route results", ctx.ResultsGuide))
            .AssertPassed();

    [Scenario("Published benchmark results include reusable hosting integrations")]
    [Fact]
    public Task Published_Benchmark_Results_Include_Reusable_Hosting_Integrations()
        => Given("the benchmark results guide and reusable hosting benchmark routes", () => new
        {
            ResultsGuide = File.ReadAllText(Path.Combine(FindRepoRoot(), "docs", "guides", "benchmark-results.md")),
            HostingRoutes = PatternBenchmarkCoverage.HostingIntegrationRoutes.ToArray()
        })
            .When("checking hosting route names against the published results", ctx => new
            {
                ctx.ResultsGuide,
                ctx.HostingRoutes,
                MissingRows = ctx.HostingRoutes
                    .Where(route => !ctx.ResultsGuide.Contains($"| {route.PatternName} | `IServiceCollection` |", StringComparison.Ordinal))
                    .Select(static route => route.PatternName)
                    .OrderBy(static name => name)
                    .ToArray()
            })
            .Then("every reusable hosting route appears in the benchmark results matrix", ctx =>
                ScenarioExpect.Empty(ctx.MissingRows))
            .And("the guide publishes the hosting route total", ctx =>
                ScenarioExpect.Contains($"{ctx.HostingRoutes.Length} reusable hosting integration route results", ctx.ResultsGuide))
            .AssertPassed();

    [Scenario("Published benchmark results include every generator source")]
    [Fact]
    public Task Published_Benchmark_Results_Include_Every_Generator_Source()
        => Given("the benchmark results guide and generator benchmark routes", () => new
        {
            ResultsGuide = File.ReadAllText(Path.Combine(FindRepoRoot(), "docs", "guides", "benchmark-results.md")),
            BenchmarkRoutes = GeneratorBenchmarkCoverage.Routes
        })
            .When("checking generator names against the published results", ctx => new
            {
                ctx.ResultsGuide,
                GeneratorCount = ctx.BenchmarkRoutes.Count,
                MissingGenerators = ctx.BenchmarkRoutes
                    .Where(route => !ctx.ResultsGuide.Contains($"| {route.GeneratorName} |", StringComparison.Ordinal))
                    .Select(static route => route.GeneratorName)
                    .OrderBy(static generatorName => generatorName)
                    .ToArray()
            })
            .Then("every generator source appears in the benchmark results matrix", ctx =>
                ScenarioExpect.Empty(ctx.MissingGenerators))
            .And("the guide publishes the generator route total", ctx =>
                ScenarioExpect.Contains($"{ctx.GeneratorCount} generator source route results", ctx.ResultsGuide))
            .AssertPassed();

    [Scenario("Published benchmark results include every dedicated scenario benchmark")]
    [Fact]
    public Task Published_Benchmark_Results_Include_Every_Dedicated_Scenario_Benchmark()
        => Given("the benchmark results guide and dedicated scenario benchmark classes", () =>
            {
                var repositoryRoot = FindRepoRoot();
                return new
                {
                    ResultsGuide = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "guides", "benchmark-results.md")),
                    ScenarioBenchmarks = Directory
                        .EnumerateFiles(Path.Combine(repositoryRoot, "benchmarks", "PatternKit.Benchmarks"), "*Benchmarks.cs", SearchOption.AllDirectories)
                        .Where(static path => !path.Replace('\\', '/').Contains("/Coverage/", StringComparison.Ordinal))
                        .Select(static path => HumanizeScenarioBenchmarkName(Path.GetFileNameWithoutExtension(path)))
                        .OrderBy(static name => name)
                        .ToArray()
                };
            })
            .When("checking scenario benchmark names against the published timing table", ctx => new
            {
                ctx.ResultsGuide,
                MissingRows = ctx.ScenarioBenchmarks
                    .SelectMany(patternName => new[]
                    {
                        ctx.ResultsGuide.Contains($"| {patternName} | Construction |", StringComparison.Ordinal)
                            ? null
                            : $"{patternName} missing construction timing row",
                        ctx.ResultsGuide.Contains($"| {patternName} | Execution |", StringComparison.Ordinal)
                            ? null
                            : $"{patternName} missing execution timing row"
                    })
                    .Where(static issue => issue is not null)
                    .Select(static issue => issue!)
                    .OrderBy(static issue => issue)
                    .ToArray()
            })
            .Then("each dedicated scenario benchmark has construction and execution results", ctx =>
                ScenarioExpect.Empty(ctx.MissingRows))
            .AssertPassed();

    private static bool HasRoute(IEnumerable<PatternBenchmarkRoute> routes, BenchmarkRoute route, BenchmarkPhase phase)
        => routes.Any(candidate => candidate.Route == route && candidate.Phase == phase);

    private static string HumanizeScenarioBenchmarkName(string benchmarkClassName)
    {
        var patternName = benchmarkClassName.EndsWith("Benchmarks", StringComparison.Ordinal)
            ? benchmarkClassName[..^"Benchmarks".Length]
            : benchmarkClassName;

        if (patternName == "CacheAside")
            return "Cache-Aside";

        if (patternName == "ReadWriteThroughCache")
            return "Read-Through / Write-Through Cache";

        if (patternName == "ChainOfResponsibility")
            return "Chain of Responsibility";

        if (patternName == "QueueLoadLeveling")
            return "Queue-Based Load Leveling";

        if (patternName == "PipesAndFilters")
            return "Pipes and Filters";

        if (patternName == "EventCarriedStateTransfer")
            return "Event-Carried State Transfer";

        if (patternName == "EventDrivenConsumer")
            return "Event-Driven Consumer";

        if (patternName == "ContentBasedRouter")
            return "Content-Based Router";

        if (patternName == "Cqrs")
            return "CQRS";

        if (patternName == "SagaProcessManager")
            return "Saga / Process Manager";

        if (patternName == "AntiCorruptionLayer")
            return "Anti-Corruption Layer";

        if (patternName == "EventSourcing")
            return "Event Sourcing";

        if (patternName == "FeatureToggle")
            return "Feature Toggle";

        if (patternName == "IdentityMap")
            return "Identity Map";

        if (patternName == "PublishSubscribe")
            return "Publish-Subscribe";

        if (patternName == "RequestReply")
            return "Request-Reply";

        if (patternName == "ScatterGather")
            return "Scatter-Gather";

        var chars = new List<char>(patternName.Length + 4);
        for (var index = 0; index < patternName.Length; index++)
        {
            var current = patternName[index];
            if (index > 0 && char.IsUpper(current) && char.IsLower(patternName[index - 1]))
                chars.Add(' ');

            chars.Add(current);
        }

        return new string(chars.ToArray());
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PatternKit.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find PatternKit repository root.");
    }
}
