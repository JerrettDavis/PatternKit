using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.CompensatingTransactions;
using PatternKit.Generators.CompensatingTransactions;

namespace PatternKit.Examples.CompensatingTransactionDemo;

public sealed record CheckoutCompensatingTransactionRequest(string OrderId, bool ShipmentAvailable);

public sealed record CheckoutCompensatingTransactionSummary(
    CompensatingTransactionStatus Status,
    IReadOnlyList<string> Log,
    IReadOnlyList<CompensatingTransactionRecordKind> History);

public static class CheckoutCompensatingTransactionDemo
{
    public static async ValueTask<CheckoutCompensatingTransactionSummary> RunFluentAsync(bool shipmentAvailable = false)
    {
        var context = new CheckoutCompensatingTransactionContext("order-1001", shipmentAvailable);
        var transaction = CreateFluent();
        var execution = await transaction.ExecuteAsync(context);
        return CreateSummary(execution);
    }

    public static async ValueTask<CheckoutCompensatingTransactionSummary> RunGeneratedAsync(bool shipmentAvailable = false)
    {
        var context = new CheckoutCompensatingTransactionContext("order-1001", shipmentAvailable);
        var execution = await GeneratedCheckoutCompensatingTransaction.Create().ExecuteAsync(context);
        return CreateSummary(execution);
    }

    public static CompensatingTransaction<CheckoutCompensatingTransactionContext> CreateFluent()
        => CompensatingTransaction<CheckoutCompensatingTransactionContext>
            .Create("checkout-compensation")
            .AddStep("reserve-inventory", ReserveInventory, ReleaseInventory, static step => step.At(10))
            .AddStep("authorize-payment", AuthorizePayment, VoidPayment, static step => step.At(20))
            .AddStep("create-shipment", CreateShipment, CancelShipment, static step => step.At(30))
            .Build();

    private static CheckoutCompensatingTransactionSummary CreateSummary(
        CompensatingTransactionExecution<CheckoutCompensatingTransactionContext> execution)
        => new(
            execution.Status,
            execution.Context.Log.ToArray(),
            execution.History.Select(static record => record.Kind).ToArray());

    internal static ValueTask ReserveInventory(CheckoutCompensatingTransactionContext context, CancellationToken cancellationToken)
    {
        context.Log.Add("inventory-reserved");
        return default;
    }

    internal static ValueTask ReleaseInventory(CheckoutCompensatingTransactionContext context, CancellationToken cancellationToken)
    {
        context.Log.Add("inventory-released");
        return default;
    }

    internal static ValueTask AuthorizePayment(CheckoutCompensatingTransactionContext context, CancellationToken cancellationToken)
    {
        context.Log.Add("payment-authorized");
        return default;
    }

    internal static ValueTask VoidPayment(CheckoutCompensatingTransactionContext context, CancellationToken cancellationToken)
    {
        context.Log.Add("payment-voided");
        return default;
    }

    internal static ValueTask CreateShipment(CheckoutCompensatingTransactionContext context, CancellationToken cancellationToken)
    {
        if (!context.ShipmentAvailable)
            throw new InvalidOperationException("shipment carrier unavailable");

        context.Log.Add("shipment-created");
        return default;
    }

    internal static ValueTask CancelShipment(CheckoutCompensatingTransactionContext context, CancellationToken cancellationToken)
    {
        context.Log.Add("shipment-canceled");
        return default;
    }
}

public sealed class CheckoutCompensatingTransactionContext(string orderId, bool shipmentAvailable)
{
    public string OrderId { get; } = orderId;

    public bool ShipmentAvailable { get; } = shipmentAvailable;

    public List<string> Log { get; } = [];
}

public sealed class CheckoutCompensatingTransactionWorkflow
{
    public ValueTask<CheckoutCompensatingTransactionSummary> RunAsync(CheckoutCompensatingTransactionRequest request)
        => request is null
            ? throw new ArgumentNullException(nameof(request))
            : CheckoutCompensatingTransactionDemo.RunFluentAsync(request.ShipmentAvailable);
}

public sealed record CheckoutCompensatingTransactionDemoRunner(
    Func<bool, ValueTask<CheckoutCompensatingTransactionSummary>> RunFluentAsync,
    Func<bool, ValueTask<CheckoutCompensatingTransactionSummary>> RunGeneratedAsync);

public static class CheckoutCompensatingTransactionServiceCollectionExtensions
{
    public static IServiceCollection AddCheckoutCompensatingTransactionDemo(this IServiceCollection services)
    {
        services.AddSingleton(CheckoutCompensatingTransactionDemo.CreateFluent());
        services.AddSingleton<CheckoutCompensatingTransactionWorkflow>();
        services.AddSingleton(new CheckoutCompensatingTransactionDemoRunner(
            CheckoutCompensatingTransactionDemo.RunFluentAsync,
            CheckoutCompensatingTransactionDemo.RunGeneratedAsync));
        return services;
    }
}

[GenerateCompensatingTransaction(TransactionName = "checkout-compensation")]
public static partial class GeneratedCheckoutCompensatingTransaction
{
    [CompensatingTransactionStep("reserve-inventory", 10, Compensation = nameof(ReleaseInventory))]
    private static ValueTask ReserveInventory(CheckoutCompensatingTransactionContext context, CancellationToken cancellationToken)
        => CheckoutCompensatingTransactionDemo.ReserveInventory(context, cancellationToken);

    private static ValueTask ReleaseInventory(CheckoutCompensatingTransactionContext context, CancellationToken cancellationToken)
        => CheckoutCompensatingTransactionDemo.ReleaseInventory(context, cancellationToken);

    [CompensatingTransactionStep("authorize-payment", 20, Compensation = nameof(VoidPayment))]
    private static ValueTask AuthorizePayment(CheckoutCompensatingTransactionContext context, CancellationToken cancellationToken)
        => CheckoutCompensatingTransactionDemo.AuthorizePayment(context, cancellationToken);

    private static ValueTask VoidPayment(CheckoutCompensatingTransactionContext context, CancellationToken cancellationToken)
        => CheckoutCompensatingTransactionDemo.VoidPayment(context, cancellationToken);

    [CompensatingTransactionStep("create-shipment", 30, Compensation = nameof(CancelShipment))]
    private static ValueTask CreateShipment(CheckoutCompensatingTransactionContext context, CancellationToken cancellationToken)
        => CheckoutCompensatingTransactionDemo.CreateShipment(context, cancellationToken);

    private static ValueTask CancelShipment(CheckoutCompensatingTransactionContext context, CancellationToken cancellationToken)
        => CheckoutCompensatingTransactionDemo.CancelShipment(context, cancellationToken);
}
