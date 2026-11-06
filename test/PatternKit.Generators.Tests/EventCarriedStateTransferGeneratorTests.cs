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
    [Theory]
    [InlineData("public static class TransferHost;", "PKECST001")]
    [InlineData("public static partial class TransferHost;", "PKECST002")]
    [InlineData("public static partial class TransferHost { [EventCarriedStateKey] private static string Key(ProductStockChanged evt) => evt.Sku; [EventCarriedStateKey] private static string Key2(ProductStockChanged evt) => evt.Sku; [EventCarriedStateVersion] private static long Version(ProductStockChanged evt) => evt.Sequence; [EventCarriedStateMapper] private static ProductInventoryState Map(ProductStockChanged evt) => new(evt.Sku, evt.QuantityOnHand); }", "PKECST002")]
    [InlineData("public static partial class TransferHost { [EventCarriedStateKey] private string Key(ProductStockChanged evt) => evt.Sku; [EventCarriedStateVersion] private static long Version(ProductStockChanged evt) => evt.Sequence; [EventCarriedStateMapper] private static ProductInventoryState Map(ProductStockChanged evt) => new(evt.Sku, evt.QuantityOnHand); }", "PKECST003")]
    [InlineData("public static partial class TransferHost { [EventCarriedStateKey] private static int Key(ProductStockChanged evt) => 1; [EventCarriedStateVersion] private static long Version(ProductStockChanged evt) => evt.Sequence; [EventCarriedStateMapper] private static ProductInventoryState Map(ProductStockChanged evt) => new(evt.Sku, evt.QuantityOnHand); }", "PKECST003")]
    [InlineData("public static partial class TransferHost { [EventCarriedStateKey] private static string Key(string evt) => evt; [EventCarriedStateVersion] private static long Version(ProductStockChanged evt) => evt.Sequence; [EventCarriedStateMapper] private static ProductInventoryState Map(ProductStockChanged evt) => new(evt.Sku, evt.QuantityOnHand); }", "PKECST003")]
    [InlineData("public static partial class TransferHost { [EventCarriedStateKey] private static string Key(ProductStockChanged evt) => evt.Sku; [EventCarriedStateVersion] private static int Version(ProductStockChanged evt) => 1; [EventCarriedStateMapper] private static ProductInventoryState Map(ProductStockChanged evt) => new(evt.Sku, evt.QuantityOnHand); }", "PKECST003")]
    [InlineData("public static partial class TransferHost { [EventCarriedStateKey] private static string Key(ProductStockChanged evt) => evt.Sku; [EventCarriedStateVersion] private static long Version(ProductStockChanged evt) => evt.Sequence; [EventCarriedStateMapper] private static string Map(ProductStockChanged evt) => evt.Sku; }", "PKECST003")]
    public Task Reports_Diagnostics_For_Invalid_Event_Carried_State_Transfer_Declarations(string declaration, string diagnosticId)
        => Given("an invalid event-carried state transfer declaration", () => Compile($$"""
            using PatternKit.Generators.EventCarriedStateTransfer;
            public sealed record ProductStockChanged(string Sku, int QuantityOnHand, long Sequence);
            public sealed record ProductInventoryState(string Sku, int QuantityOnHand);
            [GenerateEventCarriedStateTransfer(typeof(ProductStockChanged), typeof(string), typeof(ProductInventoryState))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generates event-carried state transfer defaults and host shapes")]
    [Fact]
    public Task Generates_Event_Carried_State_Transfer_Defaults_And_Host_Shapes()
        => Given("event-carried state transfer declarations with default names and host shapes", () => Compile("""
            using PatternKit.Generators.EventCarriedStateTransfer;
            namespace Demo;
            public sealed record ProductStockChanged(string Sku, int QuantityOnHand, long Sequence);
            public sealed record ProductInventoryState(string Sku, int QuantityOnHand);

            [GenerateEventCarriedStateTransfer(typeof(ProductStockChanged), typeof(string), typeof(ProductInventoryState))]
            internal abstract partial class AbstractStateTransfer
            {
                [EventCarriedStateKey]
                private static string Key(ProductStockChanged evt) => evt.Sku;
                [EventCarriedStateVersion]
                private static long Version(ProductStockChanged evt) => evt.Sequence;
                [EventCarriedStateMapper]
                private static ProductInventoryState Map(ProductStockChanged evt) => new(evt.Sku, evt.QuantityOnHand);
            }

            [GenerateEventCarriedStateTransfer(typeof(ProductStockChanged), typeof(string), typeof(ProductInventoryState), TransferName = "tenant\\\"state")]
            public sealed partial class SealedStateTransfer
            {
                [EventCarriedStateKey]
                private static string Key(ProductStockChanged evt) => evt.Sku;
                [EventCarriedStateVersion]
                private static long Version(ProductStockChanged evt) => evt.Sequence;
                [EventCarriedStateMapper]
                private static ProductInventoryState Map(ProductStockChanged evt) => new(evt.Sku, evt.QuantityOnHand);
            }

            [GenerateEventCarriedStateTransfer(typeof(ProductStockChanged), typeof(string), typeof(ProductInventoryState))]
            internal partial struct StructStateTransfer
            {
                [EventCarriedStateKey]
                private static string Key(ProductStockChanged evt) => evt.Sku;
                [EventCarriedStateVersion]
                private static long Version(ProductStockChanged evt) => evt.Sequence;
                [EventCarriedStateMapper]
                private static ProductInventoryState Map(ProductStockChanged evt) => new(evt.Sku, evt.QuantityOnHand);
            }
            """))
        .Then("generated sources preserve host shape and configured defaults", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractStateTransfer", combined);
            ScenarioExpect.Contains("public sealed partial class SealedStateTransfer", combined);
            ScenarioExpect.Contains("internal partial struct StructStateTransfer", combined);
            ScenarioExpect.Contains("Create(\"event-carried-state-transfer\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"state\")", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested event-carried state transfer host wrappers")]
    [Fact]
    public Task Generates_Nested_Event_Carried_State_Transfer_Host_Wrappers()
        => Given("nested event-carried state transfer declarations", () => Compile("""
            using PatternKit.Generators.EventCarriedStateTransfer;
            namespace Demo;
            public sealed record ProductStockChanged(string Sku, int QuantityOnHand, long Sequence);
            public sealed record ProductInventoryState(string Sku, int QuantityOnHand);

            public partial class StateTransferContainer
            {
                private partial class PrivateHost
                {
                    [GenerateEventCarriedStateTransfer(typeof(ProductStockChanged), typeof(string), typeof(ProductInventoryState))]
                    protected partial class ProtectedStateTransfer
                    {
                        [EventCarriedStateKey]
                        private static string Key(ProductStockChanged evt) => evt.Sku;
                        [EventCarriedStateVersion]
                        private static long Version(ProductStockChanged evt) => evt.Sequence;
                        [EventCarriedStateMapper]
                        private static ProductInventoryState Map(ProductStockChanged evt) => new(evt.Sku, evt.QuantityOnHand);
                    }

                    [GenerateEventCarriedStateTransfer(typeof(ProductStockChanged), typeof(string), typeof(ProductInventoryState))]
                    private protected partial class PrivateProtectedStateTransfer
                    {
                        [EventCarriedStateKey]
                        private static string Key(ProductStockChanged evt) => evt.Sku;
                        [EventCarriedStateVersion]
                        private static long Version(ProductStockChanged evt) => evt.Sequence;
                        [EventCarriedStateMapper]
                        private static ProductInventoryState Map(ProductStockChanged evt) => new(evt.Sku, evt.QuantityOnHand);
                    }

                    [GenerateEventCarriedStateTransfer(typeof(ProductStockChanged), typeof(string), typeof(ProductInventoryState))]
                    protected internal partial class ProtectedInternalStateTransfer
                    {
                        [EventCarriedStateKey]
                        private static string Key(ProductStockChanged evt) => evt.Sku;
                        [EventCarriedStateVersion]
                        private static long Version(ProductStockChanged evt) => evt.Sequence;
                        [EventCarriedStateMapper]
                        private static ProductInventoryState Map(ProductStockChanged evt) => new(evt.Sku, evt.QuantityOnHand);
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class StateTransferContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedStateTransfer", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedStateTransfer", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalStateTransfer", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed event-carried state transfer type arguments")]
    [Theory]
    [InlineData("null!", "typeof(string)", "typeof(ProductInventoryState)")]
    [InlineData("typeof(ProductStockChanged)", "null!", "typeof(ProductInventoryState)")]
    [InlineData("typeof(ProductStockChanged)", "typeof(string)", "null!")]
    public Task Skips_Malformed_Event_Carried_State_Transfer_Type_Arguments(string eventType, string keyType, string stateType)
        => Given("an event-carried state transfer declaration with a null type argument", () => Compile($$"""
            using PatternKit.Generators.EventCarriedStateTransfer;
            public sealed record ProductStockChanged(string Sku, int QuantityOnHand, long Sequence);
            public sealed record ProductInventoryState(string Sku, int QuantityOnHand);
            [GenerateEventCarriedStateTransfer({{eventType}}, {{keyType}}, {{stateType}})]
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
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
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
