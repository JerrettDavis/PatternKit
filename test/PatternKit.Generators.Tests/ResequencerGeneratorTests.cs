using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ResequencerGeneratorTests
{
    [Scenario("GeneratesResequencerFactory")]
    [Fact]
    public void GeneratesResequencerFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record Event(long Sequence);
            [GenerateResequencer(typeof(Event), FactoryName = "Build", Name = "orders", StartsAt = 10)]
            public static partial class OrderEvents
            {
                [ResequencerSequence]
                private static long Select(Message<Event> message, MessageContext context) => message.Payload.Sequence;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesResequencerFactory));
        _ = RoslynTestHelpers.Run(comp, new ResequencerGenerator(), out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("Resequencer<global::MyApp.Event>", text);
        ScenarioExpect.Contains(".StartsAt(10)", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("ReportsResequencerDiagnostics")]
    [Theory]
    [InlineData("public static class OrderEvents { }", "PKRSEQ001")]
    [InlineData("public static partial class OrderEvents { }", "PKRSEQ002")]
    public void ReportsResequencerDiagnostics(string declaration, string expected)
    {
        var source = $$"""
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record Event(long Sequence);
            [GenerateResequencer(typeof(Event))]
            {{declaration}}
            """;

        var comp = CreateCompilation(source, nameof(ReportsResequencerDiagnostics) + expected);
        _ = RoslynTestHelpers.Run(comp, new ResequencerGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    [Scenario("ReportsInvalidResequencerSelector")]
    [Fact]
    public void ReportsInvalidResequencerSelector()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record Event(long Sequence);
            [GenerateResequencer(typeof(Event))]
            public static partial class OrderEvents
            {
                [ResequencerSequence]
                private static string Select(Message<Event> message, MessageContext context) => "bad";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsInvalidResequencerSelector));
        _ = RoslynTestHelpers.Run(comp, new ResequencerGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKRSEQ003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.Routing.Resequencer<>).Assembly.Location));
}
