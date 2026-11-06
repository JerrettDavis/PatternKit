using PatternKit.Cloud.Sidecar;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Cloud.Sidecar;

[Feature("Sidecar")]
public sealed class SidecarTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Sidecar invokes companion steps around primary handler")]
    [Fact]
    public Task Sidecar_Invokes_Companion_Steps_Around_Primary_Handler()
        => Given("an order sidecar", CreateSidecar)
        .When("an order request is invoked", sidecar => sidecar.Invoke(new OrderRequest("O-100")))
        .Then("the sidecar enriches and observes the primary operation", result =>
        {
            ScenarioExpect.True(result.Succeeded);
            ScenarioExpect.Equal("order-sidecar", result.SidecarName);
            ScenarioExpect.Equal("accepted:O-100:trace-1", result.Response!.Confirmation);
            ScenarioExpect.Equal(["trace", "metrics"], result.Events);
        })
        .AssertPassed();

    [Scenario("Sidecar reports companion or handler failures")]
    [Fact]
    public Task Sidecar_Reports_Companion_Or_Handler_Failures()
        => Given("a sidecar with a failing companion", () => Sidecar<OrderRequest, OrderResponse>.Create("order-sidecar")
            .Before("trace", static ctx => ctx.Items["trace-id"] = "trace-1")
            .After("metrics", static (_, _) => throw new InvalidOperationException("metrics unavailable"))
            .Handle(static ctx => new OrderResponse($"accepted:{ctx.Request.OrderId}"))
            .Build())
        .When("the operation is invoked", sidecar => sidecar.Invoke(new OrderRequest("O-100")))
        .Then("the failed result preserves completed companion events", result =>
        {
            ScenarioExpect.True(result.Failed);
            ScenarioExpect.Equal(["trace"], result.Events);
            ScenarioExpect.Contains("metrics unavailable", result.Exception!.Message);
        })
        .AssertPassed();

    [Scenario("Sidecar validates configuration")]
    [Fact]
    public Task Sidecar_Validates_Configuration()
        => Given("invalid sidecar inputs", () => true)
        .Then("invalid names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => Sidecar<OrderRequest, OrderResponse>.Create("")
                .Before("trace", AddTrace)
                .Handle(Handle)
                .Build()))
        .And("missing companion steps are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => Sidecar<OrderRequest, OrderResponse>.Create().Handle(Handle).Build()))
        .And("missing handlers are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => Sidecar<OrderRequest, OrderResponse>.Create().Before("trace", AddTrace).Build()))
        .And("duplicate companion names are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => Sidecar<OrderRequest, OrderResponse>.Create()
                .Before("trace", AddTrace)
                .Before("TRACE", AddTrace)))
        .And("null requests are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => CreateSidecar().Invoke(null!)))
        .AssertPassed();

    private static Sidecar<OrderRequest, OrderResponse> CreateSidecar()
        => Sidecar<OrderRequest, OrderResponse>.Create("order-sidecar")
            .Before("trace", AddTrace)
            .After("metrics", static (ctx, response) => ctx.Items["confirmation"] = response.Confirmation)
            .Handle(Handle)
            .Build();

    private static void AddTrace(SidecarContext<OrderRequest> ctx) => ctx.Items["trace-id"] = "trace-1";

    private static OrderResponse Handle(SidecarContext<OrderRequest> ctx)
        => new($"accepted:{ctx.Request.OrderId}:{ctx.Items["trace-id"]}");

    private sealed record OrderRequest(string OrderId);

    private sealed record OrderResponse(string Confirmation);
}
