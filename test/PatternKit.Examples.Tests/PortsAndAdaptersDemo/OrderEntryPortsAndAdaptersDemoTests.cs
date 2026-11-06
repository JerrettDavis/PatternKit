using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.PortsAndAdaptersDemo;
using TinyBDD;

namespace PatternKit.Examples.Tests.PortsAndAdaptersDemo;

public sealed class OrderEntryPortsAndAdaptersDemoTests
{
    [Scenario("Order entry ports and adapters supports fluent and generated paths")]
    [Fact]
    public async Task Order_Entry_Ports_And_Adapters_Supports_Fluent_And_Generated_Paths()
    {
        var fluentPort = new InMemoryOrderEntryApplicationPort();
        var fluent = OrderEntryPortsAndAdaptersPolicies.CreateFluent(fluentPort);
        var generatedPort = new InMemoryOrderEntryApplicationPort();
        GeneratedOrderEntryPortsAndAdapters.ApplicationPort = generatedPort;
        var generated = GeneratedOrderEntryPortsAndAdapters.CreateGenerated();

        var fluentResponse = await fluent.ExecuteAsync(new("order-100", "buyer@example.com", 42m));
        var generatedResponse = await generated.ExecuteAsync(new("order-200", "buyer2@example.com", 84m));

        ScenarioExpect.Equal(202, fluentResponse.StatusCode);
        ScenarioExpect.Equal("order-100", fluentResponse.OrderId);
        ScenarioExpect.Equal(1, fluentPort.Accepted.Count);
        ScenarioExpect.Equal(202, generatedResponse.StatusCode);
        ScenarioExpect.Equal("order-200", generatedResponse.OrderId);
        ScenarioExpect.Equal(1, generatedPort.Accepted.Count);
    }

    [Scenario("Order entry ports and adapters is importable through IServiceCollection")]
    [Fact]
    public async Task Order_Entry_Ports_And_Adapters_Is_Importable_Through_IServiceCollection()
    {
        using var provider = new ServiceCollection()
            .AddOrderEntryPortsAndAdaptersDemo()
            .BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<OrderEntryPortsAndAdaptersWorkflow>();

        var response = await workflow.PlaceOrderAsync(new("order-300", "buyer3@example.com", 126m));

        ScenarioExpect.Equal(202, response.StatusCode);
        ScenarioExpect.Equal("accepted", response.Message);
    }
}
