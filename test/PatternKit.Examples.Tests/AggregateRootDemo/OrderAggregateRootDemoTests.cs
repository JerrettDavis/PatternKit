using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.AggregateRootDemo;
using PatternKit.Examples.DependencyInjection;
using TinyBDD;
using static PatternKit.Examples.AggregateRootDemo.OrderAggregateRootDemo;

namespace PatternKit.Examples.Tests.AggregateRootDemo;

public sealed class OrderAggregateRootDemoTests
{
    [Scenario("Fluent and generated aggregate handlers produce the same order state")]
    [Fact]
    public void Fluent_And_Generated_Aggregate_Handlers_Produce_The_Same_Order_State()
    {
        var fluent = ExecuteOrder(CreateFluentHandler());
        var generated = ExecuteOrder(CreateGeneratedHandler());

        ScenarioExpect.True(fluent.IsPlaced);
        ScenarioExpect.True(fluent.IsPaid);
        ScenarioExpect.Equal(fluent.OrderId, generated.OrderId);
        ScenarioExpect.Equal(fluent.Total, generated.Total);
        ScenarioExpect.Equal(fluent.Version, generated.Version);
        ScenarioExpect.Equal(fluent.Changes.Select(static change => change.GetType()).ToArray(), generated.Changes.Select(static change => change.GetType()).ToArray());
    }

    [Scenario("Aggregate root demo integrates with IServiceCollection")]
    [Fact]
    public void Aggregate_Root_Demo_Integrates_With_IServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddOrderAggregateRootDemo();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var service = provider.GetRequiredService<OrderAggregateRootService>();
        var summary = service.Run();

        ScenarioExpect.True(summary.IsPaid);
        ScenarioExpect.Equal(2L, summary.Version);
    }

    [Scenario("Aggregate root demo is importable through AddPatternKitExamples")]
    [Fact]
    public void Aggregate_Root_Demo_Is_Importable_Through_AddPatternKitExamples()
    {
        using var provider = new ServiceCollection()
            .AddPatternKitExamples()
            .BuildServiceProvider(validateScopes: true);

        var example = provider.GetRequiredService<OrderAggregateRootPatternExample>();
        var summary = example.Service.Run();

        ScenarioExpect.True(summary.IsPlaced);
        ScenarioExpect.True(summary.IsPaid);
    }
}
