using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

[CollectionDefinition("Order wire tap examples", DisableParallelization = true)]
public sealed class OrderWireTapExampleCollection
{
    public const string Name = "Order wire tap examples";
}

[Collection(OrderWireTapExampleCollection.Name)]
public sealed class OrderWireTapExampleTests
{
    [Scenario("FluentWireTap RecordsAuditAndMetricsWithoutChangingMessage")]
    [Fact]
    public void FluentWireTap_RecordsAuditAndMetricsWithoutChangingMessage()
    {
        var summary = OrderWireTapExampleRunner.RunFluent(new("order-1", "tenant-a", 125m));

        ScenarioExpect.Equal("order-1", summary.OrderId);
        ScenarioExpect.Equal(["audit", "metrics"], summary.InvokedTaps);
        ScenarioExpect.Equal(["corr-order:tenant-a:order-1"], summary.AuditTrail);
        ScenarioExpect.Equal(["tenant-a:125.00"], summary.Metrics);
    }

    [Scenario("GeneratedWireTap MatchesFluentTapBehavior")]
    [Fact]
    public void GeneratedWireTap_MatchesFluentTapBehavior()
    {
        var audit = new OrderWireTapAuditSink();
        var metrics = new OrderWireTapMetricsSink();
        OrderWireTapSinkRegistry.Audit = audit;
        OrderWireTapSinkRegistry.Metrics = metrics;

        var result = GeneratedOrderWireTap.Create().Publish(
            Message<OrderWireTapEvent>.Create(new("order-1", "tenant-a", 125m)),
            new MessageContext(MessageHeaders.Empty.WithCorrelationId("corr-order")));

        ScenarioExpect.Equal(["audit", "metrics"], result.InvokedTaps);
        ScenarioExpect.Equal(["corr-order:tenant-a:order-1"], audit.Entries);
        ScenarioExpect.Equal(["tenant-a:125.00"], metrics.Measurements);
    }

    [Scenario("ServiceCollection ImportsWireTapExample")]
    [Fact]
    public void ServiceCollection_ImportsWireTapExample()
    {
        var services = new ServiceCollection();
        services.AddOrderWireTapDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var tap = provider.GetRequiredService<WireTap<OrderWireTapEvent>>();
        var runner = provider.GetRequiredService<OrderWireTapExampleRunner>();

        var direct = tap.Publish(Message<OrderWireTapEvent>.Create(new("order-1", "tenant-a", 125m)));
        var summary = runner.RunGenerated(new("order-2", "tenant-b", 42m));

        ScenarioExpect.Equal(["audit", "metrics"], direct.InvokedTaps);
        ScenarioExpect.Equal(["audit", "metrics"], summary.InvokedTaps);
        ScenarioExpect.True(summary.Metrics.Contains("tenant-b:42.00", StringComparer.Ordinal));
    }

    [Scenario("AggregateServiceCollection ImportsWireTapExample")]
    [Fact]
    public void AggregateServiceCollection_ImportsWireTapExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<OrderWireTapExampleService>();

        var summary = example.Service.Publish(new("order-1", "tenant-a", 125m));

        ScenarioExpect.Equal(["audit", "metrics"], summary.InvokedTaps);
        ScenarioExpect.True(summary.AuditTrail.Contains("corr-order:tenant-a:order-1", StringComparer.Ordinal));
    }
}
