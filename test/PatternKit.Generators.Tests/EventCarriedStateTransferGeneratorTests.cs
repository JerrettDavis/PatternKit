using Microsoft.CodeAnalysis;
using PatternKit.EnterpriseIntegration.EventCarriedStateTransfer;
using PatternKit.Generators.EventCarriedStateTransfer;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Event-Carried State Transfer generator")]
public sealed partial class EventCarriedStateTransferGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates event-carried state transfer factory")]
    [Fact]
    public Task Generates_Event_Carried_State_Transfer_Factory()
        => Given("an event-carried state transfer declaration", () => Compile("""
            using PatternKit.Generators.EventCarriedStateTransfer;
            namespace Demo;
            public sealed record ProductStockChanged(string Sku, int QuantityOnHand, long Sequence);
            public sealed record ProductInventoryState(string Sku, int QuantityOnHand);
            [GenerateEventCarriedStateTransfer(typeof(ProductStockChanged), typeof(string), typeof(ProductInventoryState), FactoryMethodName = "Build", TransferName = "inventory-state")]
            public static partial class ProductInventoryStateTransfer
            {
                [EventCarriedStateKey]
                private static string Key(ProductStockChanged evt) => evt.Sku;
                [EventCarriedStateVersion]
                private static long Version(ProductStockChanged evt) => evt.Sequence;
                [EventCarriedStateMapper]
                private static ProductInventoryState Map(ProductStockChanged evt) => new(evt.Sku, evt.QuantityOnHand);
            }
            """))
        .Then("the generated source creates the configured transfer", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("EventCarriedStateTransfer<global::Demo.ProductStockChanged, string, global::Demo.ProductInventoryState>.Create(\"inventory-state\")", source);
            ScenarioExpect.Contains(".WithKey(Key)", source);
            ScenarioExpect.Contains(".WithVersion(Version)", source);
            ScenarioExpect.Contains(".WithState(Map)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid event-carried state transfer declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Event_Carried_State_Transfer_Declarations()
        => Given("invalid event-carried state transfer declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.EventCarriedStateTransfer;
                [GenerateEventCarriedStateTransfer(typeof(string), typeof(string), typeof(int))]
                public static class TransferHost;
                """),
            Compile("""
                using PatternKit.Generators.EventCarriedStateTransfer;
                [GenerateEventCarriedStateTransfer(typeof(string), typeof(string), typeof(int))]
                public static partial class TransferHost;
                """),
            Compile("""
                using PatternKit.Generators.EventCarriedStateTransfer;
                [GenerateEventCarriedStateTransfer(typeof(string), typeof(string), typeof(int))]
                public static partial class TransferHost
                {
                    [EventCarriedStateKey]
                    private static string Key(string value) => value;
                    [EventCarriedStateVersion]
                    private static string Version(string value) => value;
                    [EventCarriedStateMapper]
                    private static int Map(string value) => value.Length;
                }
                """)
        })
        .Then("diagnostics identify invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKECST001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKECST002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKECST003");
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "EventCarriedStateTransferGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(EventCarriedStateTransfer<,,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new EventCarriedStateTransferGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new(result.Diagnostics.ToArray(), result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(), emit.Success, emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources, bool EmitSuccess, IReadOnlyList<string> EmitDiagnostics);
}
