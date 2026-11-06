using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.ManualTaskGates;
using PatternKit.Generators.ManualTaskGates;

namespace PatternKit.Examples.ManualTaskGateDemo;

public sealed record OrderApprovalRequest(Guid OrderId, string RequestId, decimal Total, string SubmittedBy);

public sealed record OrderApprovalSummary(bool IsBlocked, int PendingApprovals, ManualTaskStatus? Decision);

public static partial class OrderApprovalManualTaskGates
{
    public static ManualTaskGate<Guid> CreateFluent()
        => ManualTaskGate<Guid>.Create("order-approval-gate").Build();
}

[GenerateManualTaskGate(typeof(Guid), FactoryMethodName = "CreateGenerated", GateName = "order-approval-gate")]
public static partial class GeneratedOrderApprovalManualTaskGate;

public sealed class OrderApprovalManualTaskService(ManualTaskGate<Guid> gate)
{
    public ManualTaskRecord<Guid> Submit(OrderApprovalRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return gate.Open(
            request.OrderId,
            $"Approve order total {request.Total:C}",
            "order-approvals",
            request.RequestId);
    }

    public OrderApprovalSummary Approve(Guid orderId, string actor)
    {
        var decision = gate.Approve(orderId, actor, "Approved for fulfillment.");
        return new(gate.IsBlocked, gate.PendingCount, decision?.Status);
    }

    public OrderApprovalSummary Reject(Guid orderId, string actor)
    {
        var decision = gate.Reject(orderId, actor, "Rejected by manual review.");
        return new(gate.IsBlocked, gate.PendingCount, decision?.Status);
    }
}

public sealed class OrderApprovalManualTaskGateDemoRunner(OrderApprovalManualTaskService service)
{
    public OrderApprovalSummary RunGenerated(OrderApprovalRequest request)
    {
        service.Submit(request);
        return service.Approve(request.OrderId, "case-manager");
    }

    public static OrderApprovalSummary RunFluent(OrderApprovalRequest request)
    {
        var service = new OrderApprovalManualTaskService(OrderApprovalManualTaskGates.CreateFluent());
        service.Submit(request);
        return service.Approve(request.OrderId, "case-manager");
    }

    public static OrderApprovalSummary RunGeneratedStatic(OrderApprovalRequest request)
    {
        var service = new OrderApprovalManualTaskService(GeneratedOrderApprovalManualTaskGate.CreateGenerated());
        service.Submit(request);
        return service.Approve(request.OrderId, "case-manager");
    }
}

public static class OrderApprovalManualTaskGateServiceCollectionExtensions
{
    public static IServiceCollection AddOrderApprovalManualTaskGateDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedOrderApprovalManualTaskGate.CreateGenerated());
        services.AddSingleton<OrderApprovalManualTaskService>();
        services.AddSingleton<OrderApprovalManualTaskGateDemoRunner>();
        return services;
    }
}
