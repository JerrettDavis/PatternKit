using Microsoft.CodeAnalysis;
using PatternKit.Generators.Messaging;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Message History generator")]
public sealed partial class MessageHistoryGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates message history factory")]
    [Fact]
    public Task Generates_MessageHistory_Factory()
        => Given("a partial host marked with GenerateMessageHistory", () => """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateMessageHistory(typeof(Order), "checkout-api", FactoryName = "Build", Action = "received", HeaderName = "X-History")]
            public static partial class CheckoutHistory;
            """)
            .When("the generator runs", source =>
            {
                var comp = CreateCompilation(source, nameof(Generates_MessageHistory_Factory));
                _ = RoslynTestHelpers.Run(comp, new MessageHistoryGenerator(), out var run, out _);
                return run.Results.Single().GeneratedSources.Single().SourceText.ToString();
            })
            .Then("the generated factory returns a configured builder", text =>
            {
                ScenarioExpect.Contains("MessageHistory<global::MyApp.Order>.Builder Build()", text);
                ScenarioExpect.Contains("MessageHistory<global::MyApp.Order>.Create(@\"checkout-api\")", text);
                ScenarioExpect.Contains(".Action(@\"received\")", text);
                ScenarioExpect.Contains(".Header(@\"X-History\")", text);
            })
            .AssertPassed();

    [Scenario("Reports message history diagnostics")]
    [Theory]
    [InlineData("[GenerateMessageHistory(typeof(Order), \"api\")] public static class CheckoutHistory;", "PKMH001")]
    [InlineData("[GenerateMessageHistory(typeof(Order), \"api\", FactoryName = \"\")] public static partial class CheckoutHistory;", "PKMH002")]
    public Task Reports_MessageHistory_Diagnostics(string declaration, string expected)
        => Given("an invalid GenerateMessageHistory declaration", () => $$"""
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            {{declaration}}
            """)
            .When("the generator runs", source =>
            {
                var comp = CreateCompilation(source, nameof(Reports_MessageHistory_Diagnostics) + expected);
                _ = RoslynTestHelpers.Run(comp, new MessageHistoryGenerator(), out var run, out _);
                return run.Diagnostics.Select(static d => d.Id).ToArray();
            })
            .Then("the expected diagnostic is reported", ids =>
                ScenarioExpect.Contains(expected, ids))
            .AssertPassed();

    private static Compilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(source, assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(typeof(GenerateMessageHistoryAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.Diagnostics.MessageHistory<>).Assembly.Location)
            ]);
}
