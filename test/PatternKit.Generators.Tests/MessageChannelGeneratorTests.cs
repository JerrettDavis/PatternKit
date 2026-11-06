using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class MessageChannelGeneratorTests
{
    [Scenario("GeneratesMessageChannelFactory")]
    [Fact]
    public void GeneratesMessageChannelFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record Command(string Sku);
            [GenerateMessageChannel(typeof(Command), FactoryName = "Build", ChannelName = "inventory", Capacity = 5, BackpressurePolicy = "DropOldest")]
            public static partial class InventoryChannel
            {
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesMessageChannelFactory));
        _ = RoslynTestHelpers.Run(comp, new MessageChannelGenerator(), out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("MessageChannel<global::MyApp.Command>", text);
        ScenarioExpect.Contains(".WithCapacity(5, global::PatternKit.Messaging.Channels.MessageChannelBackpressurePolicy.DropOldest)", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("ReportsMessageChannelDiagnostics")]
    [Theory]
    [InlineData("public static class InventoryChannel { }", "PKCHN001")]
    [InlineData("public static partial class InventoryChannel { }", "PKCHN002")]
    public void ReportsMessageChannelDiagnostics(string declaration, string expected)
    {
        var capacity = expected == "PKCHN002" ? ", Capacity = 0" : "";
        var source = $$"""
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record Command(string Sku);
            [GenerateMessageChannel(typeof(Command){{capacity}})]
            {{declaration}}
            """;

        var comp = CreateCompilation(source, nameof(ReportsMessageChannelDiagnostics) + expected);
        _ = RoslynTestHelpers.Run(comp, new MessageChannelGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.Channels.MessageChannel<>).Assembly.Location));
}
