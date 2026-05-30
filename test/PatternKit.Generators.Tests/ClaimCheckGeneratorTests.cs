using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.Transformation;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Claim Check generator")]
public sealed partial class ClaimCheckGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates claim check factory")]
    [Fact]
    public Task Generates_Claim_Check_Factory()
        => Given("a valid claim check declaration", () => Compile("""
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
            """))
        .Then("the generated source creates the configured claim check", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var generated = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Equal("DocumentClaimCheck.ClaimCheck.g.cs", generated.HintName);
            ScenarioExpect.Contains("Build()", generated.Source);
            ScenarioExpect.Contains("ClaimCheck<global::Demo.LargeDocument>.Create(\"documents\")", generated.Source);
            ScenarioExpect.Contains(".InStore(\"blob-store\")", generated.Source);
            ScenarioExpect.Contains(".UseStore(CreateStore())", generated.Source);
            ScenarioExpect.Contains("\"doc:\" + (message.Headers.MessageId", generated.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid claim check declarations")]
    [Theory]
    [InlineData("public static class Host { [ClaimCheckStoreFactory] private static IClaimCheckStore<string> CreateStore() => new InMemoryClaimCheckStore<string>(); }", "PKCC001")]
    [InlineData("public static partial class Host;", "PKCC002")]
    [InlineData("public static partial class Host { [ClaimCheckStoreFactory] private static IClaimCheckStore<string> One() => new InMemoryClaimCheckStore<string>(); [ClaimCheckStoreFactory] private static IClaimCheckStore<string> Two() => new InMemoryClaimCheckStore<string>(); }", "PKCC002")]
    [InlineData("public partial class Host { [ClaimCheckStoreFactory] private IClaimCheckStore<string> CreateStore() => new InMemoryClaimCheckStore<string>(); }", "PKCC003")]
    [InlineData("public static partial class Host { [ClaimCheckStoreFactory] private static IClaimCheckStore<string> CreateStore<T>() => new InMemoryClaimCheckStore<string>(); }", "PKCC003")]
    [InlineData("public static partial class Host { [ClaimCheckStoreFactory] private static IClaimCheckStore<string> CreateStore(string name) => new InMemoryClaimCheckStore<string>(); }", "PKCC003")]
    [InlineData("public static partial class Host { [ClaimCheckStoreFactory] private static string CreateStore() => string.Empty; }", "PKCC003")]
    [InlineData("public static partial class Host { [ClaimCheckStoreFactory] private static IClaimCheckStore<int> CreateStore() => new InMemoryClaimCheckStore<int>(); }", "PKCC003")]
    public Task Reports_Diagnostics_For_Invalid_Claim_Check_Declarations(string declaration, string diagnosticId)
        => Given("an invalid claim check declaration", () => Compile($$"""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging.Transformation;

            namespace Demo;

            [GenerateClaimCheck(typeof(string))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generates claim check defaults and host shapes")]
    [Fact]
    public Task Generates_Claim_Check_Defaults_And_Host_Shapes()
        => Given("claim check declarations with default names and different host shapes", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging.Transformation;

            namespace Demo;

            public sealed record LargeDocument(string Id, string Content);

            [GenerateClaimCheck(typeof(LargeDocument))]
            internal abstract partial class AbstractClaimCheck
            {
                [ClaimCheckStoreFactory]
                private static IClaimCheckStore<LargeDocument> CreateStore() => new InMemoryClaimCheckStore<LargeDocument>();
            }

            [GenerateClaimCheck(typeof(LargeDocument), ClaimCheckName = "tenant\\\"claim", StoreName = "tenant\\\"store", ClaimIdPrefix = "tenant\\\"prefix")]
            public sealed partial class SealedClaimCheck
            {
                [ClaimCheckStoreFactory]
                private static IClaimCheckStore<LargeDocument> CreateStore() => new InMemoryClaimCheckStore<LargeDocument>();
            }

            [GenerateClaimCheck(typeof(LargeDocument))]
            internal partial struct StructClaimCheck
            {
                [ClaimCheckStoreFactory]
                private static IClaimCheckStore<LargeDocument> CreateStore() => new InMemoryClaimCheckStore<LargeDocument>();
            }
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources.Select(static source => source.Source));
            ScenarioExpect.Contains("internal abstract partial class AbstractClaimCheck", combined);
            ScenarioExpect.Contains("public sealed partial class SealedClaimCheck", combined);
            ScenarioExpect.Contains("internal partial struct StructClaimCheck", combined);
            ScenarioExpect.Contains("Create(\"claim-check\")", combined);
            ScenarioExpect.Contains(".InStore(\"claim-store\")", combined);
            ScenarioExpect.Contains("\"claim:\" + (message.Headers.MessageId", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"claim\")", combined);
            ScenarioExpect.Contains(".InStore(\"tenant\\\\\\\"store\")", combined);
            ScenarioExpect.Contains("\"tenant\\\\\\\"prefix:\" + (message.Headers.MessageId", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested claim check host wrappers")]
    [Fact]
    public Task Generates_Nested_Claim_Check_Host_Wrappers()
        => Given("nested claim check declarations", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging.Transformation;

            namespace Demo;

            public sealed record LargeDocument(string Id, string Content);

            public partial class ClaimCheckContainer
            {
                private partial class PrivateHost
                {
                    [GenerateClaimCheck(typeof(LargeDocument))]
                    protected partial class ProtectedClaimCheck
                    {
                        [ClaimCheckStoreFactory]
                        private static IClaimCheckStore<LargeDocument> CreateStore() => new InMemoryClaimCheckStore<LargeDocument>();
                    }

                    [GenerateClaimCheck(typeof(LargeDocument))]
                    private protected partial class PrivateProtectedClaimCheck
                    {
                        [ClaimCheckStoreFactory]
                        private static IClaimCheckStore<LargeDocument> CreateStore() => new InMemoryClaimCheckStore<LargeDocument>();
                    }

                    [GenerateClaimCheck(typeof(LargeDocument))]
                    protected internal partial class ProtectedInternalClaimCheck
                    {
                        [ClaimCheckStoreFactory]
                        private static IClaimCheckStore<LargeDocument> CreateStore() => new InMemoryClaimCheckStore<LargeDocument>();
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources.Select(static source => source.Source));
            ScenarioExpect.Contains("public partial class ClaimCheckContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedClaimCheck", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedClaimCheck", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalClaimCheck", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed claim check type arguments")]
    [Fact]
    public Task Skips_Malformed_Claim_Check_Type_Arguments()
        => Given("a claim check declaration with a null type argument", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging.Transformation;

            [GenerateClaimCheck(null!)]
            public static partial class DocumentClaimCheck
            {
                [ClaimCheckStoreFactory]
                private static IClaimCheckStore<string> CreateStore() => new InMemoryClaimCheckStore<string>();
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = CreateCompilation(source, "ClaimCheckGeneratorTests");
        _ = RoslynTestHelpers.Run(compilation, new ClaimCheckGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources
                .Select(static source => new GeneratedSource(source.HintName, source.SourceText.ToString()))
                .ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
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

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<GeneratedSource> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);

    private sealed record GeneratedSource(string HintName, string Source);
}
