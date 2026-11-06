using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.ManualTaskGates;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.ManualTaskGateDemo;
using TinyBDD;

namespace PatternKit.Examples.Tests.ManualTaskGateDemo;

public sealed class OrderApprovalManualTaskGateDemoTests
{
    [Scenario("Fluent manual task gate approves order reviews")]
    [Fact]
    public void Fluent_Manual_Task_Gate_Approves_Order_Reviews()
    {
        var request = CreateRequest();
        var summary = OrderApprovalManualTaskGateDemoRunner.RunFluent(request);

        ScenarioExpect.False(summary.IsBlocked);
        ScenarioExpect.Equal(0, summary.PendingApprovals);
        ScenarioExpect.Equal(ManualTaskStatus.Approved, summary.Decision);
    }

    [Scenario("Generated manual task gate matches fluent behavior")]
    [Fact]
    public void Generated_Manual_Task_Gate_Matches_Fluent_Behavior()
    {
        var request = CreateRequest();

        var fluent = OrderApprovalManualTaskGateDemoRunner.RunFluent(request);
        var generated = OrderApprovalManualTaskGateDemoRunner.RunGeneratedStatic(request);

        ScenarioExpect.Equal(fluent.IsBlocked, generated.IsBlocked);
        ScenarioExpect.Equal(fluent.PendingApprovals, generated.PendingApprovals);
        ScenarioExpect.Equal(fluent.Decision, generated.Decision);
    }

    [Scenario("Order approval manual task service rejects invalid submissions and rejected decisions")]
    [Fact]
    public void Order_Approval_Manual_Task_Service_Rejects_Invalid_Submissions_And_Rejected_Decisions()
    {
        var service = new OrderApprovalManualTaskService(OrderApprovalManualTaskGates.CreateFluent());
        var request = CreateRequest();

        service.Submit(request);
        var rejected = service.Reject(request.OrderId, "case-manager");

        ScenarioExpect.Equal(ManualTaskStatus.Rejected, rejected.Decision);
        ScenarioExpect.False(rejected.IsBlocked);
        ScenarioExpect.Throws<ArgumentNullException>(() => service.Submit(null!));
    }

    [Scenario("ServiceCollection imports manual task gate example")]
    [Fact]
    public void ServiceCollection_Imports_Manual_Task_Gate_Example()
    {
        var services = new ServiceCollection();
        services.AddOrderApprovalManualTaskGateDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var runner = provider.GetRequiredService<OrderApprovalManualTaskGateDemoRunner>();
        var summary = runner.RunGenerated(CreateRequest());

        ScenarioExpect.Equal(ManualTaskStatus.Approved, summary.Decision);
        ScenarioExpect.NotNull(provider.GetRequiredService<ManualTaskGate<Guid>>());
    }

    [Scenario("Aggregate examples import manual task gate example")]
    [Fact]
    public void Aggregate_Examples_Import_Manual_Task_Gate_Example()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<OrderApprovalManualTaskGatePatternExample>();
        var summary = example.Runner.RunGenerated(CreateRequest());

        ScenarioExpect.Equal(ManualTaskStatus.Approved, summary.Decision);
        ScenarioExpect.NotNull(example.Gate);
    }

    private static OrderApprovalRequest CreateRequest()
        => new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "REQ-200", 1250.00m, "checkout-api");
}
