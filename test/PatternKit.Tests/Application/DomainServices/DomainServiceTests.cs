using PatternKit.Application.DomainServices;
using TinyBDD;

namespace PatternKit.Tests.Application.DomainServices;

public sealed class DomainServiceTests
{
    private sealed record ShipmentQuote(string OrderId, decimal Weight, decimal Value);

    private sealed record ShipmentDecision(string OrderId, string Carrier, decimal Cost);

    [Scenario("Domain service operation executes stateless domain behavior")]
    [Fact]
    public void Domain_Service_Operation_Executes_Stateless_Domain_Behavior()
    {
        var operation = DomainServiceOperation<ShipmentQuote, ShipmentDecision>.Create(
            "quote-ground",
            static request => new ShipmentDecision(request.OrderId, "ground", request.Weight * 1.25m));

        var decision = operation.Execute(new ShipmentQuote("ORD-100", 10m, 250m));

        ScenarioExpect.Equal("quote-ground", operation.Name);
        ScenarioExpect.Equal(new ShipmentDecision("ORD-100", "ground", 12.50m), decision);
    }

    [Scenario("Domain service registry resolves named operations")]
    [Fact]
    public void Domain_Service_Registry_Resolves_Named_Operations()
    {
        var registry = DomainServiceRegistry<ShipmentQuote, ShipmentDecision>.Create()
            .Add("ground", static request => new ShipmentDecision(request.OrderId, "ground", request.Weight * 1.25m))
            .Add(DomainServiceOperation<ShipmentQuote, ShipmentDecision>.Create(
                "insured-air",
                static request => new ShipmentDecision(request.OrderId, "air", request.Weight * 3m + request.Value * 0.01m)))
            .Build();

        var decision = registry.Execute("insured-air", new ShipmentQuote("ORD-100", 5m, 500m));

        ScenarioExpect.Equal(["ground", "insured-air"], registry.Names.ToArray());
        ScenarioExpect.Equal(new ShipmentDecision("ORD-100", "air", 20m), decision);
        ScenarioExpect.Equal("ground", registry.Get("ground").Name);
    }

    [Scenario("Domain service rejects invalid usage")]
    [Fact]
    public void Domain_Service_Rejects_Invalid_Usage()
    {
        var operation = DomainServiceOperation<ShipmentQuote, ShipmentDecision>.Create(
            "ground",
            static request => new ShipmentDecision(request.OrderId, "ground", request.Weight));
        var builder = DomainServiceRegistry<ShipmentQuote, ShipmentDecision>.Create();

        ScenarioExpect.Throws<ArgumentException>(() => DomainServiceOperation<ShipmentQuote, ShipmentDecision>.Create("", static request => new ShipmentDecision(request.OrderId, "ground", request.Weight)));
        ScenarioExpect.Throws<ArgumentNullException>(() => DomainServiceOperation<ShipmentQuote, ShipmentDecision>.Create("ground", null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => operation.Execute(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.Add(null!));
        builder.Add(operation);
        ScenarioExpect.Throws<InvalidOperationException>(() => builder.Add(operation));
        ScenarioExpect.Throws<KeyNotFoundException>(() => builder.Build().Get("missing"));
    }
}
