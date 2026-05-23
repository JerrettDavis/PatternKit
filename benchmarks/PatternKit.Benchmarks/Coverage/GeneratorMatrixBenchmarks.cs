using BenchmarkDotNet.Attributes;

namespace PatternKit.Benchmarks.Coverage;

[BenchmarkCategory("Coverage", "GeneratorMatrix")]
public class GeneratorMatrixBenchmarks
{
    private static readonly string RepositoryRoot = BenchmarkRepository.FindRoot();

    [ParamsSource(nameof(GeneratorRoutes))]
    public GeneratorBenchmarkRoute GeneratorRoute { get; set; } = default!;

    public static IEnumerable<GeneratorBenchmarkRoute> GeneratorRoutes => GeneratorBenchmarkCoverage.Routes;

    [Benchmark(Description = "Generator: source coverage route")]
    [BenchmarkCategory("Generated", "Generator")]
    public int Generator_Source_Route()
    {
        var sourcePath = Path.Combine(RepositoryRoot, GeneratorRoute.SourcePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Generator source is missing: {GeneratorRoute.SourcePath}", sourcePath);

        return GeneratorRoute.GeneratorName.Length + GeneratorRoute.SourcePath.Length;
    }
}
