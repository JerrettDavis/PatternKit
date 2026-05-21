using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.AntiCorruption;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class AntiCorruptionLayerGeneratorTests
{
    [Scenario("Generates anti-corruption layer factory")]
    [Fact]
    public void GeneratesAntiCorruptionLayerFactory()
    {
        var source = """
            using PatternKit.Generators.AntiCorruption;

            namespace Demo;

            public sealed record LegacyOrder(string Id, decimal Amount);
            public sealed record Order(string OrderId, decimal Total);

            [GenerateAntiCorruptionLayer(typeof(LegacyOrder), typeof(Order), FactoryMethodName = "Build", LayerName = "orders", SourceSystem = "legacy-erp")]
            public static partial class OrderAcl
            {
                [AntiCorruptionTranslator]
                private static Order Translate(LegacyOrder order) => new(order.Id, order.Amount);

                [AntiCorruptionExternalRule("Legacy order id is required.")]
                private static bool HasId(LegacyOrder order) => order.Id.Length > 0;

                [AntiCorruptionDomainRule("Domain total must be positive.")]
                private static bool HasPositiveTotal(Order order) => order.Total > 0m;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesAntiCorruptionLayerFactory));
        var gen = new AntiCorruptionLayerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("OrderAcl.AntiCorruptionLayer.g.cs", generated.HintName);
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("AntiCorruptionLayer<global::Demo.LegacyOrder, global::Demo.Order>.Create(\"orders\")", text);
        ScenarioExpect.Contains(".FromSource(\"legacy-erp\")", text);
        ScenarioExpect.Contains(".TranslateWith(static external => Translate(external));", text);
        ScenarioExpect.Contains("builder.RequireExternal(static external => HasId(external), \"Legacy order id is required.\");", text);
        ScenarioExpect.Contains("builder.RequireDomain(static domain => HasPositiveTotal(domain), \"Domain total must be positive.\");", text);

        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Reports diagnostic for non-partial anti-corruption host")]
    [Fact]
    public void ReportsDiagnosticForNonPartialAntiCorruptionHost()
    {
        var source = """
            using PatternKit.Generators.AntiCorruption;

            namespace Demo;

            [GenerateAntiCorruptionLayer(typeof(string), typeof(int))]
            public static class Host;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForNonPartialAntiCorruptionHost));

        ScenarioExpect.Equal("PKACL001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for missing anti-corruption translator")]
    [Fact]
    public void ReportsDiagnosticForMissingAntiCorruptionTranslator()
    {
        var source = """
            using PatternKit.Generators.AntiCorruption;

            namespace Demo;

            [GenerateAntiCorruptionLayer(typeof(string), typeof(int))]
            public static partial class Host;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForMissingAntiCorruptionTranslator));

        ScenarioExpect.Equal("PKACL002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid anti-corruption translator")]
    [Fact]
    public void ReportsDiagnosticForInvalidAntiCorruptionTranslator()
    {
        var source = """
            using PatternKit.Generators.AntiCorruption;

            namespace Demo;

            [GenerateAntiCorruptionLayer(typeof(string), typeof(int))]
            public static partial class Host
            {
                [AntiCorruptionTranslator]
                private static string Translate(string value) => value;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidAntiCorruptionTranslator));

        ScenarioExpect.Equal("PKACL003", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid anti-corruption rule")]
    [Fact]
    public void ReportsDiagnosticForInvalidAntiCorruptionRule()
    {
        var source = """
            using PatternKit.Generators.AntiCorruption;

            namespace Demo;

            [GenerateAntiCorruptionLayer(typeof(string), typeof(int))]
            public static partial class Host
            {
                [AntiCorruptionTranslator]
                private static int Translate(string value) => value.Length;

                [AntiCorruptionExternalRule("required")]
                private static string HasValue(string value) => value;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidAntiCorruptionRule));

        ScenarioExpect.Equal("PKACL004", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(PatternKit.Application.AntiCorruption.AntiCorruptionLayer<,>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(AntiCorruptionLayerGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new AntiCorruptionLayerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
    }
}
