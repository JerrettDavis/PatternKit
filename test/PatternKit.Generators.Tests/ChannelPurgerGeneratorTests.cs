using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ChannelPurgerGeneratorTests
{
    [Scenario("GeneratesChannelPurgerFactory")]
    [Fact]
    public void GeneratesChannelPurgerFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record Command(string Sku);
            [GenerateChannelPurger(typeof(Command), FactoryName = "Build", PurgerName = "inventory-maintenance")]
            public static partial class InventoryPurger
            {
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesChannelPurgerFactory));
        _ = RoslynTestHelpers.Run(comp, new ChannelPurgerGenerator(), out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("ChannelPurger<global::MyApp.Command>", text);
        ScenarioExpect.Contains("Build(global::PatternKit.Messaging.Channels.MessageChannel<global::MyApp.Command> channel)", text);
        ScenarioExpect.Contains("ChannelPurger<global::MyApp.Command>.Create(@\"inventory-maintenance\")", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("ReportsChannelPurgerDiagnostics")]
    [Fact]
    public void ReportsChannelPurgerDiagnostics()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record Command(string Sku);
            [GenerateChannelPurger(typeof(Command))]
            public static class InventoryPurger
            {
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsChannelPurgerDiagnostics));
        _ = RoslynTestHelpers.Run(comp, new ChannelPurgerGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKCP001", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.Channels.ChannelPurger<>).Assembly.Location));
}
