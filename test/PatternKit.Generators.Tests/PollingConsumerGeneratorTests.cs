using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class PollingConsumerGeneratorTests
{
    [Scenario("GeneratesPollingConsumerFactory")]
    [Fact]
    public void GeneratesPollingConsumerFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record Command(string Sku);
            [GeneratePollingConsumer(typeof(Command), FactoryName = "Build", ConsumerName = "inventory-poller")]
            public static partial class InventoryPollingConsumer
            {
                [PollingConsumerSource]
                private static Message<Command>? Poll(MessageContext context) => null;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesPollingConsumerFactory));
        _ = RoslynTestHelpers.Run(comp, new PollingConsumerGenerator(), out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("PollingConsumer<global::MyApp.Command>", text);
        ScenarioExpect.Contains(".From(Poll)", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("ReportsPollingConsumerDiagnostics")]
    [Theory]
    [InlineData("public static class InventoryPollingConsumer { }", "PKPOLL001")]
    [InlineData("public static partial class InventoryPollingConsumer { }", "PKPOLL002")]
    public void ReportsPollingConsumerDiagnostics(string declaration, string expected)
    {
        var source = $$"""
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record Command(string Sku);
            [GeneratePollingConsumer(typeof(Command))]
            {{declaration}}
            """;

        var comp = CreateCompilation(source, nameof(ReportsPollingConsumerDiagnostics) + expected);
        _ = RoslynTestHelpers.Run(comp, new PollingConsumerGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    [Scenario("ReportsInvalidPollingConsumerSource")]
    [Fact]
    public void ReportsInvalidPollingConsumerSource()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record Command(string Sku);
            [GeneratePollingConsumer(typeof(Command))]
            public static partial class InventoryPollingConsumer
            {
                [PollingConsumerSource]
                private static string Poll(MessageContext context) => "bad";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsInvalidPollingConsumerSource));
        _ = RoslynTestHelpers.Run(comp, new PollingConsumerGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKPOLL003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.Consumers.PollingConsumer<>).Assembly.Location));
}
