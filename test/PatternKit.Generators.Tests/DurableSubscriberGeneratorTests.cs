using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class DurableSubscriberGeneratorTests
{
    [Scenario("GeneratesDurableSubscriberFactory")]
    [Fact]
    public void GeneratesDurableSubscriberFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.Consumers;
            using PatternKit.Messaging.Storage;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateDurableSubscriber(typeof(Order), FactoryName = "Build", SubscriberName = "shipping-projection")]
            public static partial class ShippingSubscriber
            {
                [DurableSubscriberHandler("project")]
                private static DurableSubscriberHandlerResult Project(StoredMessage<Order> message, MessageContext context)
                    => DurableSubscriberHandlerResult.Success("project");
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesDurableSubscriberFactory));
        _ = RoslynTestHelpers.Run(comp, new DurableSubscriberGenerator(), out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("ShippingSubscriber.DurableSubscriber.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("DurableSubscriber<global::MyApp.Order>", text);
        ScenarioExpect.Contains(".From(store)", text);
        ScenarioExpect.Contains(".TrackWith(checkpoints)", text);
        ScenarioExpect.Contains(".Handle(@\"project\", Project)", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ReportsDurableSubscriberDiagnostics")]
    [Theory]
    [InlineData("public static class ShippingSubscriber { }", "PKDS001")]
    [InlineData("public static partial class ShippingSubscriber { }", "PKDS002")]
    public void ReportsDurableSubscriberDiagnostics(string declaration, string expected)
    {
        var source = $$"""
            using PatternKit.Generators.Messaging;

            namespace MyApp;
            public sealed record Order(string Id);
            [GenerateDurableSubscriber(typeof(Order))]
            {{declaration}}
            """;

        var comp = CreateCompilation(source, nameof(ReportsDurableSubscriberDiagnostics) + expected);
        _ = RoslynTestHelpers.Run(comp, new DurableSubscriberGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    [Scenario("ReportsInvalidDurableSubscriberHandler")]
    [Fact]
    public void ReportsInvalidDurableSubscriberHandler()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;
            public sealed record Order(string Id);
            [GenerateDurableSubscriber(typeof(Order))]
            public static partial class ShippingSubscriber
            {
                [DurableSubscriberHandler("project")]
                private static int Project(Order order) => 1;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsInvalidDurableSubscriberHandler));
        _ = RoslynTestHelpers.Run(comp, new DurableSubscriberGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKDS003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Consumers.DurableSubscriber<>).Assembly.Location));
}
