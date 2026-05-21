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
    [Fact]
    public Task Generator_Reports_Invalid_Materialized_View_Declarations()
        => Given("a non-partial materialized view declaration", () => Compile("""
            using PatternKit.Generators.MaterializedViews;
            public abstract record OrderEvent(string OrderId);
            public sealed record OrderState(string OrderId);
            [GenerateMaterializedView(typeof(OrderState), typeof(OrderEvent))]
            public static class OrderProjection;
            """))
        .Then("the partial diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKMV001"))
        .AssertPassed();

    [Scenario("Generator reports materialized view declarations without handlers")]
    [Fact]
    public Task Generator_Reports_Materialized_View_Declarations_Without_Handlers()
        => Given("a partial materialized view declaration without handlers", () => Compile("""
            using PatternKit.Generators.MaterializedViews;
            public abstract record OrderEvent(string OrderId);
            public sealed record OrderState(string OrderId);
            [GenerateMaterializedView(typeof(OrderState), typeof(OrderEvent))]
            public static partial class OrderProjection;
            """))
        .Then("the missing handlers diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKMV002"))
        .AssertPassed();

    [Scenario("Generator reports invalid materialized view handlers")]
    [Fact]
    public Task Generator_Reports_Invalid_Materialized_View_Handlers()
        => Given("a materialized view with invalid handler signature", () => Compile("""
            using PatternKit.Generators.MaterializedViews;
            public abstract record OrderEvent(string OrderId);
            public sealed record OrderPlaced(string OrderId) : OrderEvent(OrderId);
            public sealed record OrderState(string OrderId);
            [GenerateMaterializedView(typeof(OrderState), typeof(OrderEvent))]
            public static partial class OrderProjection
            {
                [MaterializedViewHandler(typeof(OrderPlaced))]
                private static string ApplyPlaced(OrderState state, OrderPlaced @event) => @event.OrderId;
            }
            """))
        .Then("the handler diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKMV003"))
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
