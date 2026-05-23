using PatternKit.Examples.ProductionReadiness;

namespace PatternKit.Benchmarks.Coverage;

public enum BenchmarkRoute
{
    Fluent,
    SourceGenerated
}

public enum BenchmarkPhase
{
    Construction,
    Execution
}

public sealed record PatternBenchmarkRoute(
    string PatternName,
    PatternFamily Family,
    BenchmarkRoute Route,
    BenchmarkPhase Phase,
    string SourcePath,
    string TestPath,
    string DocumentationPath)
{
    public override string ToString()
        => $"{Family}/{PatternName}/{Route}/{Phase}";
}

public sealed record GeneratorBenchmarkRoute(
    string GeneratorName,
    string SourcePath,
    string TestPath,
    string DocumentationPath)
{
    public override string ToString() => GeneratorName;
}
