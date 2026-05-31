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

    [Scenario("Generates anti-corruption layer factory for abstract and sealed hosts")]
    [Fact]
    public void GeneratesAntiCorruptionLayerFactoryForAbstractAndSealedHosts()
    {
        var source = """
            using PatternKit.Generators.AntiCorruption;

            namespace Demo;

            public sealed record ExternalOrder(string Id);
            public sealed record DomainOrder(string Id);

            [GenerateAntiCorruptionLayer(typeof(ExternalOrder), typeof(DomainOrder), FactoryMethodName = "CreateAbstract")]
            public abstract partial class AbstractAcl
            {
                [AntiCorruptionTranslator]
                private static DomainOrder Translate(ExternalOrder order) => new(order.Id);
            }

            [GenerateAntiCorruptionLayer(typeof(ExternalOrder), typeof(DomainOrder), FactoryMethodName = "CreateSealed")]
            public sealed partial class SealedAcl
            {
                [AntiCorruptionTranslator]
                private static DomainOrder Translate(ExternalOrder order) => new(order.Id);
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesAntiCorruptionLayerFactoryForAbstractAndSealedHosts));
        var gen = new AntiCorruptionLayerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = run.Results.SelectMany(result => result.GeneratedSources).ToArray();
        ScenarioExpect.Equal(2, generated.Length);
        var abstractText = ScenarioExpect.Single(generated.Where(source => source.HintName == "AbstractAcl.AntiCorruptionLayer.g.cs")).SourceText.ToString();
        var sealedText = ScenarioExpect.Single(generated.Where(source => source.HintName == "SealedAcl.AntiCorruptionLayer.g.cs")).SourceText.ToString();
        ScenarioExpect.Contains("public abstract partial class AbstractAcl", abstractText);
        ScenarioExpect.Contains("CreateAbstract()", abstractText);
        ScenarioExpect.Contains(".FromSource(\"external\")", abstractText);
        ScenarioExpect.Contains("public sealed partial class SealedAcl", sealedText);
        ScenarioExpect.Contains("CreateSealed()", sealedText);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Generates anti-corruption layer source for struct and nested accessibility variants")]
    [Fact]
    public void GeneratesAntiCorruptionLayerSourceForStructAndNestedAccessibilityVariants()
    {
        var source = """
            using PatternKit.Generators.AntiCorruption;

            namespace Demo;

            public sealed record ExternalOrder(string Id);
            public sealed record DomainOrder(string Id);

            [GenerateAntiCorruptionLayer(typeof(ExternalOrder), typeof(DomainOrder), FactoryMethodName = "CreateInternal")]
            internal partial struct InternalAcl
            {
                [AntiCorruptionTranslator]
                private static DomainOrder Translate(ExternalOrder order) => new(order.Id);
            }

            public partial class Outer
            {
                [GenerateAntiCorruptionLayer(typeof(ExternalOrder), typeof(DomainOrder), FactoryMethodName = "CreatePrivate")]
                private partial class PrivateAcl
                {
                    [AntiCorruptionTranslator]
                    private static DomainOrder Translate(ExternalOrder order) => new(order.Id);
                }

                [GenerateAntiCorruptionLayer(typeof(ExternalOrder), typeof(DomainOrder), FactoryMethodName = "CreateProtected")]
                protected partial class ProtectedAcl
                {
                    [AntiCorruptionTranslator]
                    private static DomainOrder Translate(ExternalOrder order) => new(order.Id);
                }

                [GenerateAntiCorruptionLayer(typeof(ExternalOrder), typeof(DomainOrder), FactoryMethodName = "CreateProtectedInternal")]
                protected internal partial class ProtectedInternalAcl
                {
                    [AntiCorruptionTranslator]
                    private static DomainOrder Translate(ExternalOrder order) => new(order.Id);
                }

                [GenerateAntiCorruptionLayer(typeof(ExternalOrder), typeof(DomainOrder), FactoryMethodName = "CreatePrivateProtected")]
                private protected partial class PrivateProtectedAcl
                {
                    [AntiCorruptionTranslator]
                    private static DomainOrder Translate(ExternalOrder order) => new(order.Id);
                }
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesAntiCorruptionLayerSourceForStructAndNestedAccessibilityVariants));
        var gen = new AntiCorruptionLayerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generatedText = string.Join("\n", run.Results.SelectMany(result => result.GeneratedSources).Select(source => source.SourceText.ToString()));
        ScenarioExpect.Contains("internal partial struct InternalAcl", generatedText);
        ScenarioExpect.Contains("private partial class PrivateAcl", generatedText);
        ScenarioExpect.Contains("protected partial class ProtectedAcl", generatedText);
        ScenarioExpect.Contains("protected internal partial class ProtectedInternalAcl", generatedText);
        ScenarioExpect.Contains("private protected partial class PrivateProtectedAcl", generatedText);
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
