using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Application.Repository;
using PatternKit.Generators.Repository;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class RepositoryGeneratorTests
{
    [Scenario("Generates repository factory")]
    [Fact]
    public void GeneratesRepositoryFactory()
    {
        var source = """
            using PatternKit.Generators.Repository;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateRepository(typeof(Order), typeof(string), FactoryName = "Build")]
            public static partial class OrderRepositoryFactory
            {
                [RepositoryKeySelector]
                private static string SelectKey(Order order) => order.Id;
            }

            public static class Demo
            {
                public static object Run() => OrderRepositoryFactory.Build();
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesRepositoryFactory));
        var gen = new RepositoryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderRepositoryFactory.Repository.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("Build", text);
        ScenarioExpect.Contains("InMemoryRepository<global::MyApp.Order, string>.Create(SelectKey).Build()", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Reports diagnostic for non partial repository")]
    [Fact]
    public void ReportsDiagnosticForNonPartialRepository()
    {
        var source = """
            using PatternKit.Generators.Repository;
            public sealed record Order(string Id);
            [GenerateRepository(typeof(Order), typeof(string))]
            public static class OrderRepositoryFactory;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialRepository));
        var gen = new RepositoryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKREP001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for missing repository key selector")]
    [Fact]
    public void ReportsDiagnosticForMissingRepositoryKeySelector()
    {
        var source = """
            using PatternKit.Generators.Repository;
            public sealed record Order(string Id);
            [GenerateRepository(typeof(Order), typeof(string))]
            public static partial class OrderRepositoryFactory;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForMissingRepositoryKeySelector));
        var gen = new RepositoryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKREP002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid repository key selector")]
    [Fact]
    public void ReportsDiagnosticForInvalidRepositoryKeySelector()
    {
        var source = """
            using PatternKit.Generators.Repository;
            public sealed record Order(string Id);
            [GenerateRepository(typeof(Order), typeof(string))]
            public static partial class OrderRepositoryFactory
            {
                [RepositoryKeySelector]
                private static int SelectKey(Order order) => 1;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidRepositoryKeySelector));
        var gen = new RepositoryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKREP003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(InMemoryRepository<,>).Assembly.Location));
}
