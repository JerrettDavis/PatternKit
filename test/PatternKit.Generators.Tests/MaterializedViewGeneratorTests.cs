using Microsoft.CodeAnalysis;
using PatternKit.Application.MaterializedViews;
using PatternKit.Generators.MaterializedViews;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Materialized View generator")]
public sealed partial class MaterializedViewGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits materialized view factory")]
    [Fact]
    public Task Generator_Emits_Materialized_View_Factory()
        => Given("a valid materialized view declaration", () => Compile("""
            using PatternKit.Generators.MaterializedViews;
            namespace Demo;
            public abstract record OrderEvent(string OrderId);
            public sealed record OrderPlaced(string OrderId) : OrderEvent(OrderId);
            public sealed record OrderState(string OrderId);
            [GenerateMaterializedView(typeof(OrderState), typeof(OrderEvent), FactoryName = "Build", ViewName = "order-read-model")]
            public static partial class OrderReadModelProjection
            {
                [MaterializedViewHandler(typeof(OrderPlaced), Order = 10)]
                private static OrderState ApplyPlaced(OrderState state, OrderPlaced @event) => new(@event.OrderId);
            }
            """))
        .Then("generated source creates the view with handlers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("MaterializedView<global::Demo.OrderState, global::Demo.OrderEvent>.Create(\"order-read-model\")", source);
            ScenarioExpect.Contains(".WithHandler<global::Demo.OrderPlaced>(ApplyPlaced, 10)", source);
        })
        .And("generated source compiles", result =>
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics)))
        .AssertPassed();

    [Scenario("Generator emits async materialized view handlers")]
    [Fact]
    public Task Generator_Emits_Async_Materialized_View_Handlers()
        => Given("a valid materialized view declaration with async handler", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.MaterializedViews;
            public abstract record OrderEvent(string OrderId);
            public sealed record OrderPaid(string OrderId) : OrderEvent(OrderId);
            public sealed record OrderState(string OrderId);
            [GenerateMaterializedView(typeof(OrderState), typeof(OrderEvent), FactoryName = "CreateProjection")]
            internal partial struct OrderProjection
            {
                [MaterializedViewHandler(typeof(OrderPaid), Order = 20)]
                private static ValueTask<OrderState> ApplyPaid(OrderState state, OrderPaid @event, CancellationToken cancellationToken)
                    => new(new OrderState(@event.OrderId));
            }
            """))
        .Then("generated source uses async handler registration", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("internal partial struct OrderProjection", source);
            ScenarioExpect.Contains(".WithAsyncHandler<global::OrderPaid>(ApplyPaid, 20)", source);
            ScenarioExpect.Contains("CreateProjection()", source);
        })
        .And("generated source compiles", result =>
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics)))
        .AssertPassed();

    [Scenario("Generator reports invalid materialized view declarations")]
    [Theory]
    [InlineData("public static class OrderProjection { [MaterializedViewHandler(typeof(OrderPlaced))] private static OrderState ApplyPlaced(OrderState state, OrderPlaced @event) => state; }", "PKMV001")]
    [InlineData("public static partial class OrderProjection;", "PKMV002")]
    [InlineData("public partial class OrderProjection { [MaterializedViewHandler(typeof(OrderPlaced))] private OrderState ApplyPlaced(OrderState state, OrderPlaced @event) => state; }", "PKMV003")]
    [InlineData("public static partial class OrderProjection { [MaterializedViewHandler(typeof(OtherEvent))] private static OrderState ApplyOther(OrderState state, OtherEvent @event) => state; }", "PKMV003")]
    [InlineData("public static partial class OrderProjection { [MaterializedViewHandler(typeof(OrderPlaced))] private static OrderState ApplyPlaced() => new(string.Empty); }", "PKMV003")]
    [InlineData("public static partial class OrderProjection { [MaterializedViewHandler(typeof(OrderPlaced))] private static OrderState ApplyPlaced(OrderState state) => state; }", "PKMV003")]
    [InlineData("public static partial class OrderProjection { [MaterializedViewHandler(typeof(OrderPlaced))] private static OrderState ApplyPlaced(string state, OrderPlaced @event) => new(@event.OrderId); }", "PKMV003")]
    [InlineData("public static partial class OrderProjection { [MaterializedViewHandler(typeof(OrderPlaced))] private static OrderState ApplyPlaced(OrderState state, OrderEvent @event) => state; }", "PKMV003")]
    [InlineData("public static partial class OrderProjection { [MaterializedViewHandler(typeof(OrderPlaced))] private static string ApplyPlaced(OrderState state, OrderPlaced @event) => @event.OrderId; }", "PKMV003")]
    [InlineData("public static partial class OrderProjection { [MaterializedViewHandler(typeof(OrderPlaced))] private static ValueTask<OrderState> ApplyPlaced(OrderState state, OrderPlaced @event, string cancellationToken) => new(state); }", "PKMV003")]
    [InlineData("public static partial class OrderProjection { [MaterializedViewHandler(typeof(OrderPlaced))] private static ValueTask<string> ApplyPlaced(OrderState state, OrderPlaced @event, CancellationToken cancellationToken) => new(@event.OrderId); }", "PKMV003")]
    public Task Generator_Reports_Invalid_Materialized_View_Declarations(string declaration, string diagnosticId)
        => Given("an invalid materialized view declaration", () => Compile($$"""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.MaterializedViews;
            public abstract record OrderEvent(string OrderId);
            public sealed record OrderPlaced(string OrderId) : OrderEvent(OrderId);
            public sealed record OtherEvent(string OrderId);
            public sealed record OrderState(string OrderId);
            [GenerateMaterializedView(typeof(OrderState), typeof(OrderEvent))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generator emits materialized view defaults and host shapes")]
    [Fact]
    public Task Generator_Emits_Materialized_View_Defaults_And_Host_Shapes()
        => Given("materialized view declarations with default names and different host shapes", () => Compile("""
            using PatternKit.Generators.MaterializedViews;
            namespace Demo;
            public abstract record OrderEvent(string OrderId);
            public sealed record OrderPlaced(string OrderId) : OrderEvent(OrderId);
            public sealed record OrderState(string OrderId);

            [GenerateMaterializedView(typeof(OrderState), typeof(OrderEvent))]
            internal abstract partial class AbstractProjection
            {
                [MaterializedViewHandler(typeof(OrderPlaced))]
                private static OrderState ApplyPlaced(OrderState state, OrderPlaced @event) => new(@event.OrderId);
            }

            [GenerateMaterializedView(typeof(OrderState), typeof(OrderEvent), ViewName = "tenant\\\"projection")]
            public sealed partial class SealedProjection
            {
                [MaterializedViewHandler(typeof(OrderPlaced))]
                private static OrderState ApplyPlaced(OrderState state, OrderPlaced @event) => new(@event.OrderId);
            }

            [GenerateMaterializedView(typeof(OrderState), typeof(OrderEvent))]
            internal partial struct StructProjection
            {
                [MaterializedViewHandler(typeof(OrderPlaced))]
                private static OrderState ApplyPlaced(OrderState state, OrderPlaced @event) => new(@event.OrderId);
            }
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractProjection", combined);
            ScenarioExpect.Contains("public sealed partial class SealedProjection", combined);
            ScenarioExpect.Contains("internal partial struct StructProjection", combined);
            ScenarioExpect.Contains("Create(\"AbstractProjection\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"projection\")", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator emits nested materialized view host wrappers")]
    [Fact]
    public Task Generator_Emits_Nested_Materialized_View_Host_Wrappers()
        => Given("nested materialized view declarations", () => Compile("""
            using PatternKit.Generators.MaterializedViews;
            namespace Demo;
            public abstract record OrderEvent(string OrderId);
            public sealed record OrderPlaced(string OrderId) : OrderEvent(OrderId);
            public sealed record OrderState(string OrderId);

            public partial class ProjectionContainer
            {
                private partial class PrivateHost
                {
                    [GenerateMaterializedView(typeof(OrderState), typeof(OrderEvent))]
                    protected partial class ProtectedProjection
                    {
                        [MaterializedViewHandler(typeof(OrderPlaced))]
                        private static OrderState ApplyPlaced(OrderState state, OrderPlaced @event) => new(@event.OrderId);
                    }

                    [GenerateMaterializedView(typeof(OrderState), typeof(OrderEvent))]
                    private protected partial class PrivateProtectedProjection
                    {
                        [MaterializedViewHandler(typeof(OrderPlaced))]
                        private static OrderState ApplyPlaced(OrderState state, OrderPlaced @event) => new(@event.OrderId);
                    }

                    [GenerateMaterializedView(typeof(OrderState), typeof(OrderEvent))]
                    protected internal partial class ProtectedInternalProjection
                    {
                        [MaterializedViewHandler(typeof(OrderPlaced))]
                        private static OrderState ApplyPlaced(OrderState state, OrderPlaced @event) => new(@event.OrderId);
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class ProjectionContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedProjection", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedProjection", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalProjection", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator skips malformed materialized view type arguments")]
    [Theory]
    [InlineData("null!", "typeof(OrderEvent)")]
    [InlineData("typeof(OrderState)", "null!")]
    public Task Generator_Skips_Malformed_Materialized_View_Type_Arguments(string stateType, string eventType)
        => Given("a materialized view declaration with a null type argument", () => Compile($$"""
            using PatternKit.Generators.MaterializedViews;
            public abstract record OrderEvent(string OrderId);
            public sealed record OrderPlaced(string OrderId) : OrderEvent(OrderId);
            public sealed record OrderState(string OrderId);
            [GenerateMaterializedView({{stateType}}, {{eventType}})]
            public static partial class OrderProjection
            {
                [MaterializedViewHandler(typeof(OrderPlaced))]
                private static OrderState ApplyPlaced(OrderState state, OrderPlaced @event) => state;
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "MaterializedViewGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(MaterializedView<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new MaterializedViewGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);
}
