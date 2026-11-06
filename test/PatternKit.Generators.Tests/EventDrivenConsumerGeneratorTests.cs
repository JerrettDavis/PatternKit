using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class EventDrivenConsumerGeneratorTests
{
    [Scenario("GeneratesEventDrivenConsumerFactory")]
    [Fact]
    public void GeneratesEventDrivenConsumerFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.Consumers;
            namespace MyApp;
            public sealed record Command(string Sku);
            [GenerateEventDrivenConsumer(typeof(Command), FactoryName = "Build", ConsumerName = "inventory-events")]
            public static partial class InventoryEventDrivenConsumer
            {
                [EventDrivenConsumerHandler("audit")]
                private static EventDrivenConsumerHandlerResult Audit(Message<Command> message, MessageContext context)
                    => EventDrivenConsumerHandlerResult.Success("audit");
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesEventDrivenConsumerFactory));
        _ = RoslynTestHelpers.Run(comp, new EventDrivenConsumerGenerator(), out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("EventDrivenConsumer<global::MyApp.Command>", text);
        ScenarioExpect.Contains(".Handle(@\"audit\", Audit)", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("ReportsEventDrivenConsumerDiagnostics")]
    [Theory]
    [InlineData("public static class InventoryEventDrivenConsumer { }", "PKEVT001")]
    [InlineData("public static partial class InventoryEventDrivenConsumer { }", "PKEVT002")]
    public void ReportsEventDrivenConsumerDiagnostics(string declaration, string expected)
    {
        var source = $$"""
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record Command(string Sku);
            [GenerateEventDrivenConsumer(typeof(Command))]
            {{declaration}}
            """;

        var comp = CreateCompilation(source, nameof(ReportsEventDrivenConsumerDiagnostics) + expected);
        _ = RoslynTestHelpers.Run(comp, new EventDrivenConsumerGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    [Scenario("ReportsInvalidEventDrivenConsumerHandler")]
    [Fact]
    public void ReportsInvalidEventDrivenConsumerHandler()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record Command(string Sku);
            [GenerateEventDrivenConsumer(typeof(Command))]
            public static partial class InventoryEventDrivenConsumer
            {
                [EventDrivenConsumerHandler("audit")]
                private static string Audit(Message<Command> message, MessageContext context) => "bad";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsInvalidEventDrivenConsumerHandler));
        _ = RoslynTestHelpers.Run(comp, new EventDrivenConsumerGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKEVT003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.Consumers.EventDrivenConsumer<>).Assembly.Location));
}
