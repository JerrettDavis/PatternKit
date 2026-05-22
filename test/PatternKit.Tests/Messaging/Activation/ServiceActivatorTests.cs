using PatternKit.Messaging;
using PatternKit.Messaging.Activation;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Activation;

public sealed class ServiceActivatorTests
{
    [Scenario("Activate InvokesServiceHandler")]
    [Fact]
    public void Activate_InvokesServiceHandler()
    {
        var activator = ServiceActivator<Request, Response>.Create("inventory")
            .Handle((request, context) => Message<Response>.Create(new(request.Payload.Sku, request.Payload.Quantity <= 10)))
            .Build();

        var result = activator.Activate(Message<Request>.Create(new("SKU-100", 4)));

        ScenarioExpect.True(result.Completed);
        ScenarioExpect.Equal("inventory", result.ActivatorName);
        ScenarioExpect.Equal("SKU-100", result.Response.Payload.Sku);
        ScenarioExpect.True(result.Response.Payload.Reserved);
    }

    [Scenario("Activate RejectsInvalidInput")]
    [Fact]
    public void Activate_RejectsInvalidInput()
    {
        var activator = ServiceActivator<Request, Response>.Create()
            .Handle((_, _) => Message<Response>.Create(new("SKU-100", true)))
            .Build();

        ScenarioExpect.Throws<ArgumentNullException>(() => activator.Activate(null!));
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => ServiceActivator<Request, Response>.Create(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => ServiceActivator<Request, Response>.Create().Handle(null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => ServiceActivator<Request, Response>.Create().Build());
    }

    public sealed record Request(string Sku, int Quantity);

    public sealed record Response(string Sku, bool Reserved);
}
