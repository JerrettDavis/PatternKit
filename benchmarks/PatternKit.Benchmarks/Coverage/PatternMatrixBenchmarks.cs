using BenchmarkDotNet.Attributes;

namespace PatternKit.Benchmarks.Coverage;

public abstract class PatternMatrixBenchmarkBase
{
    private static readonly string RepositoryRoot = BenchmarkRepository.FindRoot();

    protected static int ValidateRoute(PatternBenchmarkRoute route)
    {
        var sourcePath = Path.Combine(RepositoryRoot, route.SourcePath.Replace('/', Path.DirectorySeparatorChar));
        var testPath = Path.Combine(RepositoryRoot, route.TestPath.Replace('/', Path.DirectorySeparatorChar));
        var documentationPath = Path.Combine(RepositoryRoot, route.DocumentationPath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Benchmark route source is missing: {route.SourcePath}", sourcePath);
        if (!File.Exists(testPath))
            throw new FileNotFoundException($"Benchmark route test is missing: {route.TestPath}", testPath);
        if (!File.Exists(documentationPath))
            throw new FileNotFoundException($"Benchmark route docs are missing: {route.DocumentationPath}", documentationPath);

        return route.PatternName.Length + route.SourcePath.Length + route.TestPath.Length + route.DocumentationPath.Length;
    }
}

[BenchmarkCategory("Coverage", "PatternMatrix", "Fluent", "Construction")]
public class FluentConstructionPatternMatrixBenchmarks : PatternMatrixBenchmarkBase
{
    [ParamsSource(nameof(Routes))]
    public PatternBenchmarkRoute Route { get; set; } = default!;

    public static IEnumerable<PatternBenchmarkRoute> Routes => PatternBenchmarkCoverage.FluentConstructionRoutes;

    [Benchmark(Baseline = true, Description = "Fluent: construction coverage route")]
    public int Fluent_Construction_Route()
        => ValidateRoute(Route);
}

[BenchmarkCategory("Coverage", "PatternMatrix", "Fluent", "Execution")]
public class FluentExecutionPatternMatrixBenchmarks : PatternMatrixBenchmarkBase
{
    [ParamsSource(nameof(Routes))]
    public PatternBenchmarkRoute Route { get; set; } = default!;

    public static IEnumerable<PatternBenchmarkRoute> Routes => PatternBenchmarkCoverage.FluentExecutionRoutes;

    [Benchmark(Description = "Fluent: execution coverage route")]
    public int Fluent_Execution_Route()
        => ValidateRoute(Route);
}

[BenchmarkCategory("Coverage", "PatternMatrix", "Generated", "Construction")]
public class SourceGeneratedConstructionPatternMatrixBenchmarks : PatternMatrixBenchmarkBase
{
    [ParamsSource(nameof(Routes))]
    public PatternBenchmarkRoute Route { get; set; } = default!;

    public static IEnumerable<PatternBenchmarkRoute> Routes => PatternBenchmarkCoverage.SourceGeneratedConstructionRoutes;

    [Benchmark(Description = "Generated: construction coverage route")]
    public int SourceGenerated_Construction_Route()
        => ValidateRoute(Route);
}

[BenchmarkCategory("Coverage", "PatternMatrix", "Generated", "Execution")]
public class SourceGeneratedExecutionPatternMatrixBenchmarks : PatternMatrixBenchmarkBase
{
    [ParamsSource(nameof(Routes))]
    public PatternBenchmarkRoute Route { get; set; } = default!;

    public static IEnumerable<PatternBenchmarkRoute> Routes => PatternBenchmarkCoverage.SourceGeneratedExecutionRoutes;

    [Benchmark(Description = "Generated: execution coverage route")]
    public int SourceGenerated_Execution_Route()
        => ValidateRoute(Route);
}

[BenchmarkCategory("Coverage", "PatternMatrix", "HostingIntegration")]
public class HostingIntegrationPatternMatrixBenchmarks : PatternMatrixBenchmarkBase
{
    [ParamsSource(nameof(Routes))]
    public PatternBenchmarkRoute Route { get; set; } = default!;

    public static IEnumerable<PatternBenchmarkRoute> Routes => PatternBenchmarkCoverage.HostingIntegrationRoutes;

    [Benchmark(Description = "Hosting: IServiceCollection integration coverage route")]
    public int HostingIntegration_Route()
        => ValidateRoute(Route);
}
