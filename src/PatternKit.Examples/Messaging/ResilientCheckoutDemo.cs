using PatternKit.Behavioral.Command;
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Production-shaped checkout orchestration demo with route selection, rollback, and fallback routes.
/// </summary>
public static class ResilientCheckoutDemo
{
    /// <summary>Runs checkout using the supplied request and simulated services.</summary>
    public static CheckoutResult Run(CheckoutRequest request, CheckoutServices services)
    {
        var router = BuildRouteRouter();
        var attempts = new List<CheckoutAttemptResult>();
        var context = new CheckoutContext(request, services);
        var route = router.Route(Message<CheckoutAttempt>.Create(new CheckoutAttempt(request, null)));

        while (true)
        {
            context.BeginAttempt(route);
            var result = ExecuteRoute(context, route);
            attempts.Add(result);

            if (result.Succeeded || route.Kind == CheckoutRouteKind.ManualReview)
                return context.ToResult(result.Succeeded, route.Kind == CheckoutRouteKind.ManualReview, attempts);

            var nextRoute = router.Route(Message<CheckoutAttempt>.Create(new CheckoutAttempt(request, result.FailureKind)));
            if (nextRoute == route || nextRoute.Kind == CheckoutRouteKind.ManualReview)
            {
                context.BeginAttempt(CheckoutRoute.ManualReview);
                attempts.Add(new CheckoutAttemptResult(
                    CheckoutRoute.ManualReview.Name,
                    Succeeded: false,
                    result.FailureKind,
                    "manual-review"));
                return context.ToResult(Succeeded: false, ManualReview: true, attempts);
            }

            route = nextRoute;
        }
    }

    private static ContentRouter<CheckoutAttempt, CheckoutRoute> BuildRouteRouter()
        => ContentRouter<CheckoutAttempt, CheckoutRoute>.Create()
            .When(static (message, _) => message.Payload.Request.FraudHold)
            .Then(static (_, _) => CheckoutRoute.ManualReview)
            .When(static (message, _) => message.Payload.PreviousFailure == CheckoutFailureKind.InventoryUnavailable
                                         && message.Payload.Request.AllowDropshipFallback)
            .Then(static (_, _) => CheckoutRoute.DropshipCard)
            .When(static (message, _) => message.Payload.PreviousFailure == CheckoutFailureKind.PaymentDeclined
                                         && message.Payload.Request.GiftCardBalance >= message.Payload.Request.Total)
            .Then(static (_, _) => CheckoutRoute.PrimaryGiftCard)
            .When(static (message, _) => message.Payload.Request.GiftCardBalance >= message.Payload.Request.Total
                                         && message.Payload.Request.PreferGiftCard)
            .Then(static (_, _) => CheckoutRoute.PrimaryGiftCard)
            .Default(static (_, _) => CheckoutRoute.PrimaryCard)
            .Build();

    private static CheckoutAttemptResult ExecuteRoute(CheckoutContext context, CheckoutRoute route)
    {
        if (route.Kind == CheckoutRouteKind.ManualReview)
        {
            context.Audit.Add("manual-review:queued");
            return new CheckoutAttemptResult(route.Name, Succeeded: false, null, "manual-review");
        }

        var slip = RoutingSlip<CheckoutContext>.Create()
            .Step("validate", Execute(ValidateCommand()))
            .Step("reserve-inventory", Execute(ReserveInventoryCommand(route.Inventory)))
            .Step("charge-payment", Execute(ChargePaymentCommand(route.Payment)))
            .Step("schedule-shipment", Execute(ScheduleShipmentCommand(route.Inventory)))
            .Build();

        try
        {
            slip.Execute(Message<CheckoutContext>.Create(context).WithCorrelationId(context.Request.OrderId));
            return new CheckoutAttemptResult(route.Name, Succeeded: true, null, "completed");
        }
        catch (CheckoutStepException ex)
        {
            context.Audit.Add($"route:{route.Name}:failed:{ex.FailureKind}");
            context.Compensate();
            return new CheckoutAttemptResult(route.Name, Succeeded: false, ex.FailureKind, ex.Message);
        }
    }

    private static RoutingSlip<CheckoutContext>.StepHandler Execute(Command<CheckoutContext> command)
        => (message, _) =>
        {
            command.Execute(message.Payload).GetAwaiter().GetResult();
            message.Payload.Track(command);
            return message;
        };

    private static Command<CheckoutContext> ValidateCommand()
        => Command<CheckoutContext>.Create()
            .Do(static context =>
            {
                if (context.Request.Lines.Count == 0)
                    throw new CheckoutStepException(CheckoutFailureKind.ValidationFailed, "cart-empty");

                context.Audit.Add("validate:ok");
            })
            .Build();

    private static Command<CheckoutContext> ReserveInventoryCommand(InventoryRoute inventory)
        => Command<CheckoutContext>.Create()
            .Do(context =>
            {
                if (!context.Services.Inventory.TryReserve(context.Request, inventory, out var reservationId))
                    throw new CheckoutStepException(CheckoutFailureKind.InventoryUnavailable, $"inventory-unavailable:{inventory}");

                context.ReservationId = reservationId;
                context.Audit.Add($"inventory:reserved:{inventory}:{reservationId}");
            })
            .Undo(context =>
            {
                if (context.ReservationId is null)
                    return;

                context.Services.Inventory.Release(context.ReservationId);
                context.Audit.Add($"inventory:released:{context.ReservationId}");
                context.ReservationId = null;
            })
            .Build();

    private static Command<CheckoutContext> ChargePaymentCommand(PaymentRoute payment)
        => Command<CheckoutContext>.Create()
            .Do(context =>
            {
                if (!context.Services.Payments.TryCharge(context.Request, payment, out var paymentId))
                    throw new CheckoutStepException(CheckoutFailureKind.PaymentDeclined, $"payment-declined:{payment}");

                context.PaymentId = paymentId;
                context.Audit.Add($"payment:charged:{payment}:{paymentId}");
            })
            .Undo(context =>
            {
                if (context.PaymentId is null)
                    return;

                context.Services.Payments.Refund(context.PaymentId);
                context.Audit.Add($"payment:refunded:{context.PaymentId}");
                context.PaymentId = null;
            })
            .Build();

    private static Command<CheckoutContext> ScheduleShipmentCommand(InventoryRoute inventory)
        => Command<CheckoutContext>.Create()
            .Do(context =>
            {
                var shipmentId = context.Services.Shipping.Schedule(context.Request, inventory);
                context.ShipmentId = shipmentId;
                context.Audit.Add($"shipping:scheduled:{shipmentId}");
            })
            .Undo(context =>
            {
                if (context.ShipmentId is null)
                    return;

                context.Services.Shipping.Cancel(context.ShipmentId);
                context.Audit.Add($"shipping:cancelled:{context.ShipmentId}");
                context.ShipmentId = null;
            })
            .Build();

    private sealed class CheckoutContext
    {
        private readonly Stack<Command<CheckoutContext>> _executed = new();

        internal CheckoutContext(CheckoutRequest request, CheckoutServices services)
        {
            Request = request;
            Services = services;
        }

        internal CheckoutRequest Request { get; }

        internal CheckoutServices Services { get; }

        internal List<string> Audit { get; } = new();

        internal string? ReservationId { get; set; }

        internal string? PaymentId { get; set; }

        internal string? ShipmentId { get; set; }

        internal string? FinalRoute { get; private set; }

        internal void BeginAttempt(CheckoutRoute route)
        {
            _executed.Clear();
            FinalRoute = route.Name;
            Audit.Add($"route:{route.Name}:started");
        }

        internal void Track(Command<CheckoutContext> command)
        {
            if (command.HasUndo)
                _executed.Push(command);
        }

        internal void Compensate()
        {
            while (_executed.Count > 0)
            {
                var command = _executed.Pop();
                if (command.TryUndo(this, out var undo))
                    undo.GetAwaiter().GetResult();
            }
        }

        internal CheckoutResult ToResult(bool Succeeded, bool ManualReview, IReadOnlyList<CheckoutAttemptResult> attempts)
            => new(
                Request.OrderId,
                Succeeded,
                ManualReview,
                FinalRoute ?? "none",
                ReservationId,
                PaymentId,
                ShipmentId,
                attempts.ToArray(),
                Audit.ToArray());
    }
}

/// <summary>Checkout request used by the resilient checkout demo.</summary>
public sealed record CheckoutRequest(
    string OrderId,
    string CustomerId,
    IReadOnlyList<CheckoutLine> Lines,
    bool FraudHold = false,
    bool PreferGiftCard = false,
    bool AllowDropshipFallback = true,
    decimal GiftCardBalance = 0m)
{
    /// <summary>Total amount for the request.</summary>
    public decimal Total => Lines.Sum(static line => line.UnitPrice * line.Quantity);
}

/// <summary>Checkout line item.</summary>
public sealed record CheckoutLine(string Sku, int Quantity, decimal UnitPrice);

/// <summary>Simulated service dependencies used by the checkout demo.</summary>
public sealed class CheckoutServices
{
    /// <summary>Inventory service.</summary>
    public InventoryGateway Inventory { get; init; } = new();

    /// <summary>Payment service.</summary>
    public PaymentGateway Payments { get; init; } = new();

    /// <summary>Shipping service.</summary>
    public ShippingGateway Shipping { get; init; } = new();
}

/// <summary>Simulated inventory adapter.</summary>
public sealed class InventoryGateway
{
    private int _nextReservation = 100;

    /// <summary>Whether primary warehouse reservation should succeed.</summary>
    public bool PrimaryAvailable { get; set; } = true;

    /// <summary>Whether dropship reservation should succeed.</summary>
    public bool DropshipAvailable { get; set; } = true;

    /// <summary>Released reservation identifiers.</summary>
    public List<string> ReleasedReservations { get; } = new();

    internal bool TryReserve(CheckoutRequest request, InventoryRoute route, out string reservationId)
    {
        var available = route switch
        {
            InventoryRoute.PrimaryWarehouse => PrimaryAvailable,
            InventoryRoute.Dropship => DropshipAvailable,
            _ => false
        };

        if (!available)
        {
            reservationId = "";
            return false;
        }

        reservationId = $"{route.ToString().ToLowerInvariant()}-{request.OrderId}-{++_nextReservation}";
        return true;
    }

    internal void Release(string reservationId) => ReleasedReservations.Add(reservationId);
}

/// <summary>Simulated payment adapter.</summary>
public sealed class PaymentGateway
{
    private int _nextPayment = 200;

    /// <summary>Whether card authorization should succeed.</summary>
    public bool CardApproved { get; set; } = true;

    /// <summary>Refunded payment identifiers.</summary>
    public List<string> RefundedPayments { get; } = new();

    internal bool TryCharge(CheckoutRequest request, PaymentRoute route, out string paymentId)
    {
        var approved = route switch
        {
            PaymentRoute.Card => CardApproved,
            PaymentRoute.GiftCard => request.GiftCardBalance >= request.Total,
            _ => false
        };

        if (!approved)
        {
            paymentId = "";
            return false;
        }

        paymentId = $"{route.ToString().ToLowerInvariant()}-{request.OrderId}-{++_nextPayment}";
        return true;
    }

    internal void Refund(string paymentId) => RefundedPayments.Add(paymentId);
}

/// <summary>Simulated shipping adapter.</summary>
public sealed class ShippingGateway
{
    private int _nextShipment = 300;

    /// <summary>Cancelled shipment identifiers.</summary>
    public List<string> CancelledShipments { get; } = new();

    internal string Schedule(CheckoutRequest request, InventoryRoute route)
        => $"{route.ToString().ToLowerInvariant()}-ship-{request.OrderId}-{++_nextShipment}";

    internal void Cancel(string shipmentId) => CancelledShipments.Add(shipmentId);
}

/// <summary>Checkout route attempt input.</summary>
public sealed record CheckoutAttempt(CheckoutRequest Request, CheckoutFailureKind? PreviousFailure);

/// <summary>Checkout route definition.</summary>
public sealed record CheckoutRoute(string Name, CheckoutRouteKind Kind, InventoryRoute Inventory, PaymentRoute Payment)
{
    /// <summary>Primary warehouse with card payment.</summary>
    public static CheckoutRoute PrimaryCard { get; } = new(
        "primary-card",
        CheckoutRouteKind.Fulfillment,
        InventoryRoute.PrimaryWarehouse,
        PaymentRoute.Card);

    /// <summary>Primary warehouse with gift-card payment.</summary>
    public static CheckoutRoute PrimaryGiftCard { get; } = new(
        "primary-gift-card",
        CheckoutRouteKind.Fulfillment,
        InventoryRoute.PrimaryWarehouse,
        PaymentRoute.GiftCard);

    /// <summary>Dropship fulfillment with card payment.</summary>
    public static CheckoutRoute DropshipCard { get; } = new(
        "dropship-card",
        CheckoutRouteKind.Fulfillment,
        InventoryRoute.Dropship,
        PaymentRoute.Card);

    /// <summary>Manual review route for fraud or unrecoverable failures.</summary>
    public static CheckoutRoute ManualReview { get; } = new(
        "manual-review",
        CheckoutRouteKind.ManualReview,
        InventoryRoute.None,
        PaymentRoute.None);
}

/// <summary>Route kind.</summary>
public enum CheckoutRouteKind { Fulfillment, ManualReview }

/// <summary>Inventory route.</summary>
public enum InventoryRoute { None, PrimaryWarehouse, Dropship }

/// <summary>Payment route.</summary>
public enum PaymentRoute { None, Card, GiftCard }

/// <summary>Checkout failure kind used to choose fallback routes.</summary>
public enum CheckoutFailureKind { ValidationFailed, InventoryUnavailable, PaymentDeclined }

/// <summary>Single checkout attempt result.</summary>
public sealed record CheckoutAttemptResult(
    string Route,
    bool Succeeded,
    CheckoutFailureKind? FailureKind,
    string Message);

/// <summary>Final checkout result.</summary>
public sealed record CheckoutResult(
    string OrderId,
    bool Succeeded,
    bool ManualReview,
    string FinalRoute,
    string? ReservationId,
    string? PaymentId,
    string? ShipmentId,
    IReadOnlyList<CheckoutAttemptResult> Attempts,
    IReadOnlyList<string> Audit);

internal sealed class CheckoutStepException : Exception
{
    internal CheckoutStepException(CheckoutFailureKind failureKind, string message)
        : base(message) => FailureKind = failureKind;

    internal CheckoutFailureKind FailureKind { get; }
}
