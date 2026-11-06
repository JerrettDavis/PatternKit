using PatternKit.Examples.ProductionReadiness;

namespace PatternKit.Benchmarks.Coverage;

public static class GeneratorBenchmarkCoverage
{
    private static readonly Lazy<IReadOnlyList<GeneratorBenchmarkRoute>> LazyRoutes = new(CreateRoutes);

    public static IReadOnlyList<GeneratorBenchmarkRoute> Routes => LazyRoutes.Value;

    private static IReadOnlyList<GeneratorBenchmarkRoute> CreateRoutes()
    {
        var repositoryRoot = BenchmarkRepository.FindRoot();
        var generatorRoot = Path.Combine(repositoryRoot, "src", "PatternKit.Generators");
        var catalog = new PatternKitPatternCatalog();
        var catalogBySource = catalog.Patterns
            .Where(static pattern => pattern.Implementation.HasSourceGeneratedPath)
            .GroupBy(static pattern => pattern.Implementation.GeneratorSourcePath!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        return Directory
            .EnumerateFiles(generatorRoot, "*Generator.cs", SearchOption.AllDirectories)
            .Where(static path => !Path.GetFileName(path).StartsWith("Generate", StringComparison.Ordinal))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var relativePath = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
                catalogBySource.TryGetValue(relativePath, out var pattern);
                return new GeneratorBenchmarkRoute(
                    Path.GetFileNameWithoutExtension(path),
                    relativePath,
                    pattern?.Implementation.GeneratorTestPath ?? string.Empty,
                    pattern?.Implementation.GeneratorDocumentationPath ?? string.Empty);
            })
            .ToArray();
    }
}
