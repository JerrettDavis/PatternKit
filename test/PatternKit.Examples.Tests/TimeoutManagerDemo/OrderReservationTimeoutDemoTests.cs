using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.Timeouts;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.TimeoutManagerDemo;
using TinyBDD;

namespace PatternKit.Examples.Tests.TimeoutManagerDemo;

public sealed class OrderReservationTimeoutDemoTests
{
    [Scenario("Fluent timeout manager expires order reservations")]
    [Fact]
    public void Fluent_Timeout_Manager_Expires_Order_Reservations()
    {
        var request = CreateRequest();
        var summary = OrderReservationTimeoutDemoRunner.RunFluent(request, DateTimeOffset.UtcNow.AddMinutes(20));

        ScenarioExpect.Equal(0, summary.PendingReservations);
        ScenarioExpect.Equal([request.OrderId], summary.ExpiredOrders);
    }

    [Scenario("Generated timeout manager matches fluent behavior")]
    [Fact]
    public void Generated_Timeout_Manager_Matches_Fluent_Behavior()
    {
        var request = CreateRequest();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(20);

        var fluent = OrderReservationTimeoutDemoRunner.RunFluent(request, expiresAt);
        var generated = OrderReservationTimeoutDemoRunner.RunGeneratedStatic(request, expiresAt);

        ScenarioExpect.Equal(fluent.PendingReservations, generated.PendingReservations);
        ScenarioExpect.Equal(fluent.ExpiredOrders, generated.ExpiredOrders);
    }

    [Scenario("ServiceCollection imports timeout manager example")]
    [Fact]
    public void ServiceCollection_Imports_Timeout_Manager_Example()
    {
        var services = new ServiceCollection();
        services.AddOrderReservationTimeoutDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var runner = provider.GetRequiredService<OrderReservationTimeoutDemoRunner>();
        var request = CreateRequest();
        var summary = runner.RunGenerated(request, DateTimeOffset.UtcNow.AddMinutes(20));

        ScenarioExpect.Equal([request.OrderId], summary.ExpiredOrders);
        ScenarioExpect.NotNull(provider.GetRequiredService<TimeoutManager<Guid>>());
    }

    [Scenario("Order reservation timeout service validates and completes reservations")]
    [Fact]
    public void Order_Reservation_Timeout_Service_Validates_And_Completes_Reservations()
    {
        var service = new OrderReservationTimeoutService(OrderReservationTimeoutManagers.CreateFluent());
        var request = CreateRequest();

        service.Reserve(request);

        ScenarioExpect.True(service.Complete(request.OrderId));
        ScenarioExpect.False(service.Complete(request.OrderId));
        ScenarioExpect.Throws<ArgumentNullException>(() => service.Reserve(null!));
    }

    [Scenario("Aggregate examples import timeout manager example")]
    [Fact]
    public void Aggregate_Examples_Import_Timeout_Manager_Example()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<OrderReservationTimeoutPatternExample>();
        var request = CreateRequest();
        var summary = example.Runner.RunGenerated(request, DateTimeOffset.UtcNow.AddMinutes(20));

        ScenarioExpect.Equal([request.OrderId], summary.ExpiredOrders);
        ScenarioExpect.NotNull(example.Manager);
    }

    private static OrderReservationRequest CreateRequest()
        => new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "REQ-100", TimeSpan.FromMinutes(15));
}
