using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.WorkflowOrchestration;
using PatternKit.Generators.WorkflowOrchestration;

namespace PatternKit.Examples.WorkflowOrchestrationDemo;

public sealed record FulfillmentRequest(string OrderId, bool RequiresFraudReview, bool PaymentShouldFail);

public sealed record FulfillmentSummary(
    WorkflowExecutionStatus Status,
    IReadOnlyList<string> Events,
    IReadOnlyList<WorkflowExecutionRecordKind> History);

public sealed class FulfillmentWorkflowContext(FulfillmentRequest request)
{
    public FulfillmentRequest Request { get; } = request ?? throw new ArgumentNullException(nameof(request));

    public List<string> Events { get; } = [];

    public int PaymentAttempts { get; set; }
}

public static partial class FulfillmentWorkflowOrchestrations
{
    public static WorkflowOrchestrator<FulfillmentWorkflowContext> CreateFluent()
        => WorkflowOrchestrator<FulfillmentWorkflowContext>
            .Create("fulfillment-orchestration")
            .AddStep("reserve-inventory", static (context, _) =>
            {
                context.Events.Add("inventory:reserved");
                return ValueTask.CompletedTask;
            }, static step => step.At(1).Compensate(static (context, _) =>
            {
                context.Events.Add("inventory:released");
                return ValueTask.CompletedTask;
            }))
            .AddStep("review-fraud", static (context, _) =>
            {
                context.Events.Add("fraud:reviewed");
                return ValueTask.CompletedTask;
            }, static step => step.At(2).When(static context => context.Request.RequiresFraudReview))
            .AddStep("capture-payment", static (context, _) =>
            {
                context.PaymentAttempts++;
                if (context.Request.PaymentShouldFail)
                    throw new InvalidOperationException("payment authorization declined");

                context.Events.Add("payment:captured");
                return ValueTask.CompletedTask;
            }, static step => step.At(3).WithMaxAttempts(2))
            .AddStep("release-to-warehouse", static (context, _) =>
            {
                context.Events.Add("warehouse:released");
                return ValueTask.CompletedTask;
            }, static step => step.At(4))
            .Build();
}

[WorkflowOrchestration(FactoryMethodName = "CreateGenerated", WorkflowName = "fulfillment-orchestration")]
public static partial class GeneratedFulfillmentWorkflowOrchestration
{
    [WorkflowStep("reserve-inventory", 1, Compensation = nameof(ReleaseInventory))]
    private static ValueTask ReserveInventory(FulfillmentWorkflowContext context, CancellationToken cancellationToken)
    {
        context.Events.Add("inventory:reserved");
        return ValueTask.CompletedTask;
    }

    [WorkflowStep("review-fraud", 2, Condition = nameof(RequiresFraudReview))]
    private static ValueTask ReviewFraud(FulfillmentWorkflowContext context, CancellationToken cancellationToken)
    {
        context.Events.Add("fraud:reviewed");
        return ValueTask.CompletedTask;
    }

    [WorkflowStep("capture-payment", 3, MaxAttempts = 2)]
    private static ValueTask CapturePayment(FulfillmentWorkflowContext context, CancellationToken cancellationToken)
    {
        context.PaymentAttempts++;
        if (context.Request.PaymentShouldFail)
            throw new InvalidOperationException("payment authorization declined");

        context.Events.Add("payment:captured");
        return ValueTask.CompletedTask;
    }

    [WorkflowStep("release-to-warehouse", 4)]
    private static ValueTask ReleaseToWarehouse(FulfillmentWorkflowContext context, CancellationToken cancellationToken)
    {
        context.Events.Add("warehouse:released");
        return ValueTask.CompletedTask;
    }

    private static bool RequiresFraudReview(FulfillmentWorkflowContext context) => context.Request.RequiresFraudReview;

    private static ValueTask ReleaseInventory(FulfillmentWorkflowContext context, CancellationToken cancellationToken)
    {
        context.Events.Add("inventory:released");
        return ValueTask.CompletedTask;
    }
}

public sealed class FulfillmentWorkflowOrchestrationService(WorkflowOrchestrator<FulfillmentWorkflowContext> workflow)
{
    public async ValueTask<FulfillmentSummary> FulfillAsync(FulfillmentRequest request, CancellationToken cancellationToken = default)
    {
        var context = new FulfillmentWorkflowContext(request);
        var execution = await workflow.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        return new(
            execution.Status,
            context.Events.ToArray(),
            execution.History.Select(static record => record.Kind).ToArray());
    }
}

public sealed class FulfillmentWorkflowOrchestrationDemoRunner(FulfillmentWorkflowOrchestrationService service)
{
    public ValueTask<FulfillmentSummary> RunGeneratedAsync(FulfillmentRequest request, CancellationToken cancellationToken = default)
        => service.FulfillAsync(request, cancellationToken);

    public static ValueTask<FulfillmentSummary> RunFluentAsync(FulfillmentRequest request, CancellationToken cancellationToken = default)
    {
        var service = new FulfillmentWorkflowOrchestrationService(FulfillmentWorkflowOrchestrations.CreateFluent());
        return service.FulfillAsync(request, cancellationToken);
    }

    public static ValueTask<FulfillmentSummary> RunGeneratedStaticAsync(FulfillmentRequest request, CancellationToken cancellationToken = default)
    {
        var service = new FulfillmentWorkflowOrchestrationService(GeneratedFulfillmentWorkflowOrchestration.CreateGenerated());
        return service.FulfillAsync(request, cancellationToken);
    }
}

public static class FulfillmentWorkflowOrchestrationDemoServiceCollectionExtensions
{
    public static IServiceCollection AddFulfillmentWorkflowOrchestrationDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedFulfillmentWorkflowOrchestration.CreateGenerated());
        services.AddSingleton<FulfillmentWorkflowOrchestrationService>();
        services.AddSingleton<FulfillmentWorkflowOrchestrationDemoRunner>();
        return services;
    }
}
