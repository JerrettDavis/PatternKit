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
    public Task Generates_MessagingBridge_Factory()
        => Given("a partial host marked with GenerateMessagingBridge", () => """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record PartnerOrder(string Id);
            public sealed record CommerceOrder(string Id);

            [GenerateMessagingBridge(typeof(PartnerOrder), typeof(CommerceOrder), FactoryName = "Build", BridgeName = "partner-commerce")]
            public static partial class PartnerCommerceBridge;
            """)
            .When("the generator runs", source =>
            {
                var comp = CreateCompilation(source, nameof(Generates_MessagingBridge_Factory));
                _ = RoslynTestHelpers.Run(comp, new MessagingBridgeGenerator(), out var run, out _);
                return run.Results.Single().GeneratedSources.Single().SourceText.ToString();
            })
            .Then("the generated factory returns a configured builder", text =>
            {
                ScenarioExpect.Contains("MessagingBridge<global::MyApp.PartnerOrder, global::MyApp.CommerceOrder>.Builder Build()", text);
                ScenarioExpect.Contains("MessagingBridge<global::MyApp.PartnerOrder, global::MyApp.CommerceOrder>.Create(@\"partner-commerce\")", text);
            })
            .AssertPassed();

    [Scenario("Reports messaging bridge diagnostics")]
    [Theory]
    [InlineData("[GenerateMessagingBridge(typeof(PartnerOrder), typeof(CommerceOrder))] public static class PartnerCommerceBridge;", "PKMBR001")]
    [InlineData("[GenerateMessagingBridge(typeof(PartnerOrder), typeof(CommerceOrder), BridgeName = \"\")] public static partial class PartnerCommerceBridge;", "PKMBR002")]
    public Task Reports_MessagingBridge_Diagnostics(string declaration, string expected)
        => Given("an invalid GenerateMessagingBridge declaration", () => $$"""
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record PartnerOrder(string Id);
            public sealed record CommerceOrder(string Id);

            {{declaration}}
            """)
            .When("the generator runs", source =>
            {
                var comp = CreateCompilation(source, nameof(Reports_MessagingBridge_Diagnostics) + expected);
                _ = RoslynTestHelpers.Run(comp, new MessagingBridgeGenerator(), out var run, out _);
                return run.Diagnostics.Select(static d => d.Id).ToArray();
            })
            .Then("the expected diagnostic is reported", ids =>
                ScenarioExpect.Contains(expected, ids))
            .AssertPassed();

    private static Compilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(source, assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(typeof(GenerateMessagingBridgeAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.Bridges.MessagingBridge<,>).Assembly.Location)
            ]);
}
