using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class InvalidMessageChannelGeneratorTests
{
    [Scenario("GeneratesInvalidMessageChannelFactory")]
    [Fact]
    public void GeneratesInvalidMessageChannelFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record OrderImport(string Sku);
            [GenerateInvalidMessageChannel(typeof(OrderImport), FactoryName = "Build", ChannelName = "invalid-order-imports")]
            public static partial class OrderImportInvalids
            {
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesInvalidMessageChannelFactory));
        _ = RoslynTestHelpers.Run(comp, new InvalidMessageChannelGenerator(), out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("InvalidMessageChannel<global::MyApp.OrderImport>.Builder", text);
        ScenarioExpect.Contains("Build(global::PatternKit.Messaging.Channels.MessageChannel<global::PatternKit.Messaging.Channels.InvalidMessage<global::MyApp.OrderImport>> invalidChannel)", text);
        ScenarioExpect.Contains("InvalidMessageChannel<global::MyApp.OrderImport>.Create(@\"invalid-order-imports\")", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("ReportsInvalidMessageChannelDiagnostics")]
    [Fact]
    public void ReportsInvalidMessageChannelDiagnostics()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record OrderImport(string Sku);
            [GenerateInvalidMessageChannel(typeof(OrderImport))]
            public static class OrderImportInvalids
            {
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsInvalidMessageChannelDiagnostics));
        _ = RoslynTestHelpers.Run(comp, new InvalidMessageChannelGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKIMC001", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.Channels.InvalidMessageChannel<>).Assembly.Location));
}
