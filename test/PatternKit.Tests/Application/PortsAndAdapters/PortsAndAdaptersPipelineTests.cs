using PatternKit.Application.PortsAndAdapters;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Application.PortsAndAdapters;

[Feature("Ports and Adapters")]
public sealed class PortsAndAdaptersPipelineTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Ports and Adapters isolates inbound and outbound adapters from the application port")]
    [Fact]
    public Task Ports_And_Adapters_Isolates_Inbound_And_Outbound_Adapters_From_The_Application_Port()
        => Given("a ports and adapters pipeline", () => PortsAndAdaptersPipeline<HttpCreateOrderRequest, CreateOrderCommand, CreateOrderResult, HttpCreateOrderResponse>
            .Create("order-entry")
            .AdaptInboundWith(static inbound => new CreateOrderCommand(inbound.OrderId, inbound.CustomerEmail))
            .HandleWith(static (command, _) => new ValueTask<CreateOrderResult>(new CreateOrderResult(command.OrderId, command.Email, "accepted")))
            .AdaptOutboundWith(static result => new HttpCreateOrderResponse(202, result.OrderId, result.Status))
            .Build())
        .When("an inbound delivery DTO is executed", (Func<IPortsAndAdaptersPipeline<HttpCreateOrderRequest, CreateOrderCommand, CreateOrderResult, HttpCreateOrderResponse>, ValueTask<HttpCreateOrderResponse>>)(pipeline =>
            pipeline.ExecuteAsync(new HttpCreateOrderRequest("order-100", "buyer@example.com"))))
        .Then("the outbound DTO is returned without leaking the application command", response =>
        {
            ScenarioExpect.Equal(202, response.StatusCode);
            ScenarioExpect.Equal("order-100", response.OrderId);
            ScenarioExpect.Equal("accepted", response.Status);
        })
        .AssertPassed();

    [Scenario("Ports and Adapters validates configuration and execution arguments")]
    [Fact]
    public Task Ports_And_Adapters_Validates_Configuration_And_Execution_Arguments()
        => Given("ports and adapters builders", () => true)
        .Then("invalid configuration is rejected", (Func<bool, Task>)(async _ =>
        {
            ScenarioExpect.Throws<ArgumentNullException>(() => PortsAndAdaptersPipeline<HttpCreateOrderRequest, CreateOrderCommand, CreateOrderResult, HttpCreateOrderResponse>.Create().AdaptInboundWith(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => PortsAndAdaptersPipeline<HttpCreateOrderRequest, CreateOrderCommand, CreateOrderResult, HttpCreateOrderResponse>.Create().HandleWith(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => PortsAndAdaptersPipeline<HttpCreateOrderRequest, CreateOrderCommand, CreateOrderResult, HttpCreateOrderResponse>.Create().AdaptOutboundWith(null!));
            ScenarioExpect.Throws<InvalidOperationException>(() => PortsAndAdaptersPipeline<HttpCreateOrderRequest, CreateOrderCommand, CreateOrderResult, HttpCreateOrderResponse>.Create().Build());
            ScenarioExpect.Throws<InvalidOperationException>(() => PortsAndAdaptersPipeline<HttpCreateOrderRequest, CreateOrderCommand, CreateOrderResult, HttpCreateOrderResponse>.Create().AdaptInboundWith(Inbound).Build());
            ScenarioExpect.Throws<InvalidOperationException>(() => PortsAndAdaptersPipeline<HttpCreateOrderRequest, CreateOrderCommand, CreateOrderResult, HttpCreateOrderResponse>.Create().AdaptInboundWith(Inbound).HandleWith(Handle).Build());
            ScenarioExpect.Throws<ArgumentException>(() => PortsAndAdaptersPipeline<HttpCreateOrderRequest, CreateOrderCommand, CreateOrderResult, HttpCreateOrderResponse>.Create("").AdaptInboundWith(Inbound).HandleWith(Handle).AdaptOutboundWith(Outbound).Build());

            var pipeline = PortsAndAdaptersPipeline<HttpCreateOrderRequest, CreateOrderCommand, CreateOrderResult, HttpCreateOrderResponse>.Create("order-entry")
                .AdaptInboundWith(Inbound)
                .HandleWith(Handle)
                .AdaptOutboundWith(Outbound)
                .Build();

            ScenarioExpect.Equal("order-entry", pipeline.Name);
            await ScenarioExpect.ThrowsAsync<ArgumentNullException>(() => pipeline.ExecuteAsync(null!).AsTask());
            await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(new("order", "email"), new CancellationToken(true)).AsTask());
        }))
        .AssertPassed();

    private static CreateOrderCommand Inbound(HttpCreateOrderRequest request) => new(request.OrderId, request.CustomerEmail);

    private static ValueTask<CreateOrderResult> Handle(CreateOrderCommand command, CancellationToken cancellationToken) => new(new CreateOrderResult(command.OrderId, command.Email, "accepted"));

    private static HttpCreateOrderResponse Outbound(CreateOrderResult result) => new(202, result.OrderId, result.Status);

    private sealed record HttpCreateOrderRequest(string OrderId, string CustomerEmail);
    private sealed record CreateOrderCommand(string OrderId, string Email);
    private sealed record CreateOrderResult(string OrderId, string Email, string Status);
    private sealed record HttpCreateOrderResponse(int StatusCode, string OrderId, string Status);
}
