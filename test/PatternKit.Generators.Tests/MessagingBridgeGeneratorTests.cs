using Microsoft.CodeAnalysis;
using PatternKit.Generators.Messaging;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Messaging Bridge generator")]
public sealed partial class MessagingBridgeGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates messaging bridge factory")]
    [Fact]
    public Task Generates_Messaging_Bridge_Factory()
        => Given("a partial host marked with GenerateMessagingBridge", () => Compile("""
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record PartnerOrder(string Id);
            public sealed record CommerceOrder(string Id);

            [GenerateMessagingBridge(typeof(PartnerOrder), typeof(CommerceOrder), FactoryName = "Build", BridgeName = "partner-commerce")]
            public static partial class PartnerCommerceBridge;
            """))
        .Then("the generated factory returns a configured builder", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class PartnerCommerceBridge", source);
            ScenarioExpect.Contains("MessagingBridge<global::MyApp.PartnerOrder, global::MyApp.CommerceOrder>.Builder Build()", source);
            ScenarioExpect.Contains("MessagingBridge<global::MyApp.PartnerOrder, global::MyApp.CommerceOrder>.Create(@\"partner-commerce\")", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostic for non-partial messaging bridge declarations")]
    [Fact]
    public Task Reports_Diagnostic_For_Non_Partial_Messaging_Bridge_Declarations()
        => Given("a non-partial messaging bridge declaration", () => Compile("""
            using PatternKit.Generators.Messaging;

            public sealed record PartnerOrder(string Id);
            public sealed record CommerceOrder(string Id);

            [GenerateMessagingBridge(typeof(PartnerOrder), typeof(CommerceOrder))]
            public static class PartnerCommerceBridge;
            """))
        .Then("the diagnostic identifies the host", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKMBR001"))
        .AssertPassed();

    [Scenario("Reports diagnostic for invalid messaging bridge configuration")]
    [Theory]
    [InlineData("FactoryName = \"\"", "PKMBR002")]
    [InlineData("BridgeName = \"\"", "PKMBR002")]
    [InlineData("FactoryName = \"   \"", "PKMBR002")]
    [InlineData("BridgeName = \"   \"", "PKMBR002")]
    public Task Reports_Diagnostic_For_Invalid_Messaging_Bridge_Configuration(string invalidConfiguration, string expected)
        => Given("an invalid messaging bridge declaration", () => Compile($$"""
            using PatternKit.Generators.Messaging;

            public sealed record PartnerOrder(string Id);
            public sealed record CommerceOrder(string Id);

            [GenerateMessagingBridge(typeof(PartnerOrder), typeof(CommerceOrder), {{invalidConfiguration}})]
            public static partial class PartnerCommerceBridge;
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == expected))
        .AssertPassed();

    [Scenario("Generates messaging bridge defaults and type shapes")]
    [Fact]
    public Task Generates_Messaging_Bridge_Defaults_And_Type_Shapes()
        => Given("messaging bridge declarations using default names and different host shapes", () => Compile("""
            using PatternKit.Generators.Messaging;

            namespace Demo;

            public sealed record PartnerOrder(string Id);
            public sealed record CommerceOrder(string Id);

            [GenerateMessagingBridge(typeof(PartnerOrder), typeof(CommerceOrder))]
            internal abstract partial class AbstractBridge;

            [GenerateMessagingBridge(typeof(PartnerOrder), typeof(CommerceOrder), BridgeName = "tenant\"bridge")]
            public sealed partial class SealedBridge;

            [GenerateMessagingBridge(typeof(PartnerOrder), typeof(CommerceOrder))]
            internal partial struct StructBridge;
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractBridge", combined);
            ScenarioExpect.Contains("Create()", combined);
            ScenarioExpect.Contains("MessagingBridge<global::Demo.PartnerOrder, global::Demo.CommerceOrder>.Create(@\"messaging-bridge\")", combined);
            ScenarioExpect.Contains("public sealed partial class SealedBridge", combined);
            ScenarioExpect.Contains("MessagingBridge<global::Demo.PartnerOrder, global::Demo.CommerceOrder>.Create(@\"tenant\"\"bridge\")", combined);
            ScenarioExpect.Contains("internal partial struct StructBridge", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested messaging bridge host wrappers")]
    [Fact]
    public Task Generates_Nested_Messaging_Bridge_Host_Wrappers()
        => Given("nested messaging bridge declarations with non-public accessibility", () => Compile("""
            using PatternKit.Generators.Messaging;

            namespace Demo;

            public sealed record PartnerOrder(string Id);
            public sealed record CommerceOrder(string Id);

            public partial class BridgeContainer
            {
                private partial class PrivateHost
                {
                    [GenerateMessagingBridge(typeof(PartnerOrder), typeof(CommerceOrder))]
                    protected partial class ProtectedBridge;

                    [GenerateMessagingBridge(typeof(PartnerOrder), typeof(CommerceOrder))]
                    private protected partial class PrivateProtectedBridge;

                    [GenerateMessagingBridge(typeof(PartnerOrder), typeof(CommerceOrder))]
                    protected internal partial class ProtectedInternalBridge;
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class BridgeContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedBridge", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedBridge", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalBridge", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed messaging bridge type arguments")]
    [Theory]
    [InlineData("null!", "typeof(CommerceOrder)")]
    [InlineData("typeof(PartnerOrder)", "null!")]
    public Task Skips_Malformed_Messaging_Bridge_Type_Arguments(string inboundType, string outboundType)
        => Given("a messaging bridge declaration with a null message type", () => Compile($$"""
            using PatternKit.Generators.Messaging;

            public sealed record PartnerOrder(string Id);
            public sealed record CommerceOrder(string Id);

            [GenerateMessagingBridge({{inboundType}}, {{outboundType}})]
            public static partial class PartnerCommerceBridge;
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(source, "MessagingBridgeGeneratorTests",
            extra:
            [
                MetadataReference.CreateFromFile(typeof(GenerateMessagingBridgeAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Bridges.MessagingBridge<,>).Assembly.Location)
            ]);
        _ = RoslynTestHelpers.Run(compilation, new MessagingBridgeGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);
}
