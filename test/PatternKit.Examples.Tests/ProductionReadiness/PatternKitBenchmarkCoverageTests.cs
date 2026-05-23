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

    private static bool HasRoute(IEnumerable<PatternBenchmarkRoute> routes, BenchmarkRoute route, BenchmarkPhase phase)
        => routes.Any(candidate => candidate.Route == route && candidate.Phase == phase);

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
