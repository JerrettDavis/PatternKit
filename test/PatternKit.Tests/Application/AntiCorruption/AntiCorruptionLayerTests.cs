using PatternKit.Application.AntiCorruption;
using TinyBDD;

namespace PatternKit.Tests.Application.AntiCorruption;

public sealed class AntiCorruptionLayerTests
{
    private sealed record LegacyOrder(string Id, decimal Amount, string Currency);
    private sealed record Order(string OrderId, decimal TotalUsd);

    [Scenario("Anti-corruption layer translates accepted external models")]
    [Fact]
    public void AntiCorruptionLayer_Translates_Accepted_External_Models()
    {
        var layer = CreateLayer();

        var result = layer.Translate(new LegacyOrder("ORD-100", 25m, "USD"));

        ScenarioExpect.True(result.Accepted);
        ScenarioExpect.Equal("legacy-erp", result.SourceSystem);
        ScenarioExpect.Equal(new Order("ORD-100", 25m), result.Value);
    }

    [Scenario("Anti-corruption layer rejects invalid external models before translation")]
    [Fact]
    public void AntiCorruptionLayer_Rejects_Invalid_External_Models_Before_Translation()
    {
        var calls = 0;
        var layer = AntiCorruptionLayer<LegacyOrder, Order>
            .Create("orders")
            .FromSource("legacy-erp")
            .RequireExternal(static order => order.Currency == "USD", "Only USD orders are accepted.")
            .TranslateWith(order =>
            {
                calls++;
                return new Order(order.Id, order.Amount);
            })
            .Build();

        var result = layer.Translate(new LegacyOrder("ORD-100", 25m, "EUR"));

        ScenarioExpect.True(result.Rejected);
        ScenarioExpect.Equal("Only USD orders are accepted.", result.RejectionReason);
        ScenarioExpect.Equal(0, calls);
    }

    [Scenario("Anti-corruption layer rejects invalid domain models after translation")]
    [Fact]
    public void AntiCorruptionLayer_Rejects_Invalid_Domain_Models_After_Translation()
    {
        var layer = CreateLayer();

        var result = layer.Translate(new LegacyOrder("ORD-100", -5m, "USD"));

        ScenarioExpect.True(result.Rejected);
        ScenarioExpect.Equal("Domain orders must have positive totals.", result.RejectionReason);
    }

    [Scenario("Async anti-corruption layer preserves cancellation")]
    [Fact]
    public async Task AsyncAntiCorruptionLayer_Preserves_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var layer = CreateLayer();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() =>
            layer.TranslateAsync(new LegacyOrder("ORD-100", 25m, "USD"), cts.Token).AsTask());
    }

    [Scenario("Anti-corruption layer rejects invalid configuration")]
    [Fact]
    public void AntiCorruptionLayer_Rejects_Invalid_Configuration()
    {
        var layer = CreateLayer();

        ScenarioExpect.Throws<ArgumentException>(() => AntiCorruptionLayer<LegacyOrder, Order>.Create("").TranslateWith(static order => new Order(order.Id, order.Amount)).Build());
        ScenarioExpect.Throws<ArgumentException>(() => AntiCorruptionLayer<LegacyOrder, Order>.Create().FromSource("").TranslateWith(static order => new Order(order.Id, order.Amount)).Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => AntiCorruptionLayer<LegacyOrder, Order>.Create().TranslateWith(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => AntiCorruptionLayer<LegacyOrder, Order>.Create().RequireExternal(null!, "reason"));
        ScenarioExpect.Throws<ArgumentException>(() => AntiCorruptionLayer<LegacyOrder, Order>.Create().RequireExternal(static _ => true, ""));
        ScenarioExpect.Throws<InvalidOperationException>(() => AntiCorruptionLayer<LegacyOrder, Order>.Create().Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => layer.Translate(null!));
    }

    private static AntiCorruptionLayer<LegacyOrder, Order> CreateLayer()
        => AntiCorruptionLayer<LegacyOrder, Order>
            .Create("orders")
            .FromSource("legacy-erp")
            .RequireExternal(static order => !string.IsNullOrWhiteSpace(order.Id), "Legacy order id is required.")
            .RequireExternal(static order => order.Currency == "USD", "Only USD orders are accepted.")
            .TranslateWith(static order => new Order(order.Id, order.Amount))
            .RequireDomain(static order => order.TotalUsd > 0m, "Domain orders must have positive totals.")
            .Build();
}
