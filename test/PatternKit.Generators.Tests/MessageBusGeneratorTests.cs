using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class MessageBusGeneratorTests
{
    [Scenario("GeneratesMessageBusFactory")]
    [Fact]
    public void GeneratesMessageBusFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging.Channels;
            namespace MyApp;
            public sealed record OrderEvent(string OrderId);
            [GenerateMessageBus(typeof(OrderEvent), FactoryName = "Build", BusName = "orders")]
            public static partial class OrderBus
            {
                [MessageBusRoute("accepted")]
                private static MessageChannel<OrderEvent> Accepted()
                    => MessageChannel<OrderEvent>.Create("accepted-orders").Build();
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesMessageBusFactory));
        _ = RoslynTestHelpers.Run(comp, new MessageBusGenerator(), out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("MessageBus<global::MyApp.OrderEvent>", text);
        ScenarioExpect.Contains(".Route(@\"accepted\", Accepted())", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("ReportsMessageBusDiagnostics")]
    [Theory]
    [InlineData("public static class OrderBus { }", "PKBUS001")]
    [InlineData("public static partial class OrderBus { }", "PKBUS002")]
    public void ReportsMessageBusDiagnostics(string declaration, string expected)
    {
        var source = $$"""
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record OrderEvent(string OrderId);
            [GenerateMessageBus(typeof(OrderEvent))]
            {{declaration}}
            """;

        var comp = CreateCompilation(source, nameof(ReportsMessageBusDiagnostics) + expected);
        _ = RoslynTestHelpers.Run(comp, new MessageBusGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    [Scenario("ReportsInvalidRouteDiagnostics")]
    [Fact]
    public void ReportsInvalidRouteDiagnostics()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record OrderEvent(string OrderId);
            [GenerateMessageBus(typeof(OrderEvent))]
            public static partial class OrderBus
            {
                [MessageBusRoute("accepted")]
                private static string Accepted() => "bad";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsInvalidRouteDiagnostics));
        _ = RoslynTestHelpers.Run(comp, new MessageBusGenerator(), out var run, out _);

        ScenarioExpect.Equal("PKBUS003", ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics)).Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.Channels.MessageBus<>).Assembly.Location));
}
