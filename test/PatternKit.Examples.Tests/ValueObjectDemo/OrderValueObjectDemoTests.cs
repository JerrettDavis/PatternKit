using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.ValueObjectDemo;
using TinyBDD;
using static PatternKit.Examples.ValueObjectDemo.OrderValueObjectDemo;

namespace PatternKit.Examples.Tests.ValueObjectDemo;

public sealed class OrderValueObjectDemoTests
{
    [Scenario("Fluent value object validates money and compares by value")]
    [Fact]
    public void Fluent_Value_Object_Validates_Money_And_Compares_By_Value()
    {
        var first = Money.Create(25m, "usd");
        var second = Money.Create(25m, "USD");
        var invalid = Money.Create(-1m, "BTC");

        ScenarioExpect.True(first.IsValid);
        ScenarioExpect.Equal(first.Value, second.Value);
        ScenarioExpect.False(invalid.IsValid);
        ScenarioExpect.Equal(["positive-amount", "supported-currency"], invalid.Failures.Select(static failure => failure.Rule).OrderBy(static rule => rule).ToArray());
    }

    [Scenario("Generated value object factory and equality are used in orders")]
    [Fact]
    public void Generated_Value_Object_Factory_And_Equality_Are_Used_In_Orders()
    {
        var first = GeneratedOrderNumber.Create("ORD-100", "ONLINE");
        var second = GeneratedOrderNumber.Create("ORD-100", "ONLINE");
        var different = GeneratedOrderNumber.Create("ORD-100", "STORE");
        var order = PriceOrder("ord-100", 25m, "usd");

        ScenarioExpect.Equal(first, second);
        ScenarioExpect.True(first == second);
        ScenarioExpect.True(first != different);
        ScenarioExpect.True(order.IsValid);
        ScenarioExpect.Equal(first, order.Value.GeneratedOrderNumber);
    }

    [Scenario("Order value object demo integrates with IServiceCollection")]
    [Fact]
    public void Order_Value_Object_Demo_Integrates_With_IServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddOrderValueObjectDemo();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var service = provider.GetRequiredService<OrderValueObjectService>();
        var order = service.Price("ord-200", 40m, "eur");

        ScenarioExpect.True(order.IsValid);
        ScenarioExpect.Equal("EUR", order.Value.Total.Currency);
    }

    [Scenario("Order value object demo is importable through AddPatternKitExamples")]
    [Fact]
    public void Order_Value_Object_Demo_Is_Importable_Through_AddPatternKitExamples()
    {
        using var provider = new ServiceCollection()
            .AddPatternKitExamples()
            .BuildServiceProvider(validateScopes: true);

        var example = provider.GetRequiredService<OrderValueObjectPatternExample>();
        var order = example.Service.Price("ord-300", 75m, "usd");

        ScenarioExpect.True(order.IsValid);
        ScenarioExpect.Equal("ORD-300", order.Value.GeneratedOrderNumber.Number);
    }
}
