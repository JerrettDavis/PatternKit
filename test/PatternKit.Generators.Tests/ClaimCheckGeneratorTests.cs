using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.Transformation;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ClaimCheckGeneratorTests
{
    [Scenario("Generates claim check factory")]
    [Fact]
    public void GeneratesClaimCheckFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging.Transformation;

            namespace Demo;

            public sealed record LargeDocument(string Id, string Content);

            [GenerateClaimCheck(typeof(LargeDocument), FactoryName = "Build", ClaimCheckName = "documents", StoreName = "blob-store", ClaimIdPrefix = "doc")]
            public static partial class DocumentClaimCheck
            {
                [ClaimCheckStoreFactory]
                private static IClaimCheckStore<LargeDocument> CreateStore() => new InMemoryClaimCheckStore<LargeDocument>();
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesClaimCheckFactory));
        var gen = new ClaimCheckGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("DocumentClaimCheck.ClaimCheck.g.cs", generated.HintName);
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("ClaimCheck<global::Demo.LargeDocument>.Create(\"documents\")", text);
        ScenarioExpect.Contains(".InStore(\"blob-store\")", text);
        ScenarioExpect.Contains(".UseStore(CreateStore())", text);
        ScenarioExpect.Contains("\"doc:\" + (message.Headers.MessageId", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Reports diagnostic for non-partial claim check host")]
    [Fact]
    public void ReportsDiagnosticForNonPartialClaimCheckHost()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace Demo;

            [GenerateClaimCheck(typeof(string))]
            public static class Host;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForNonPartialClaimCheckHost));

        ScenarioExpect.Equal("PKCC001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for missing claim check store factory")]
    [Fact]
    public void ReportsDiagnosticForMissingClaimCheckStoreFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace Demo;

            [GenerateClaimCheck(typeof(string))]
            public static partial class Host;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForMissingClaimCheckStoreFactory));

        ScenarioExpect.Equal("PKCC002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid claim check store factory")]
    [Fact]
    public void ReportsDiagnosticForInvalidClaimCheckStoreFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace Demo;

            [GenerateClaimCheck(typeof(string))]
            public static partial class Host
            {
                [ClaimCheckStoreFactory]
                private static string CreateStore() => "";
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidClaimCheckStoreFactory));

        ScenarioExpect.Equal("PKCC003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(IClaimCheckStore<>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(ClaimCheckGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new ClaimCheckGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
    }
}
