using PatternKit.Behavioral.Command;
using PatternKit.Behavioral.Mediator;
using PatternKit.Behavioral.Observer;
using PatternKit.Behavioral.Strategy;
using PatternKit.Behavioral.Template;
using PatternKit.Behavioral.Visitor;
using PatternKit.Creational.Factory;
using PatternKit.Structural.Adapter;
using PatternKit.Structural.Facade;

namespace PatternKit.Examples.PatternShowcase;

/// <summary>
/// End-to-end demonstration that composes many patterns to process an order.
/// Shows how Strategy, Factory, Adapter, Template Method, Command (with undo),
/// Visitor, Mediator, Observer, Memento (via Command undo), and Facade can work together.
/// </summary>
public static class PatternShowcase
{
    // -------- Domain DTOs (external) and Models (internal) --------

    /// <summary>External API DTO for an incoming order.</summary>
    public sealed record OrderDto(string OrderId, string CustomerId, string PaymentKind, OrderItemDto[] Items);

    /// <summary>External API DTO for a line item.</summary>
    public sealed record OrderItemDto(string Sku, string Name, decimal Price, int Qty, string? Category = null);

    /// <summary>Internal order aggregate.</summary>
    public sealed class Order
    {
        public required string OrderId { get; set; }
        public required string CustomerId { get; set; }
        public required Payment Payment { get; set; }
        public required List<OrderItem> Items { get; set; }
    }

    /// <summary>Internal line item.</summary>
    public sealed record OrderItem(string Sku, string Name, decimal UnitPrice, int Quantity, string? Category = null);

    /// <summary>Payment base type and variants for visitor-based dispatch.</summary>
    public abstract record Payment;
    public sealed record Cash(decimal Amount) : Payment;
    public sealed record Card(string Brand, string Last4, decimal Amount) : Payment;

    /// <summary>Runtime order processing context used by commands and template.</summary>
    public sealed class OrderContext
    {
        public required Order Order { get; init; }
        public required IPaymentGateway Gateway { get; init; }
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal Fees { get; set; }
        public decimal Total => Subtotal - Discount + Fees;
        public List<string> Audit { get; } = new();
    }

    // -------- Facade Contract --------

    /// <summary>
    /// Typed facade exposing a minimal, task-focused API for clients to place orders.
    /// Implemented using <see cref="TypedFacade{TFacadeInterface}"/> for strong typing.
    /// </summary>
    public interface IOrderProcessingFacade
    {
        (bool ok, string message, decimal total) Place(OrderDto dto);
    }

    /// <summary>
    /// Entry point to build the facade instance. Consumers can register this in DI as a singleton.
    /// </summary>
    public static IOrderProcessingFacade Build()
    {
        // 1) Adapter: external DTO → internal model
        var dtoToOrder = BuildOrderAdapter();

        // 2) Factory: pick payment gateway by key
        var gatewayFactory = BuildPaymentFactory();

        // 3) Strategy: compute discounts by customer/category
        var discount = BuildDiscountStrategy();

        // 4) Visitor: compute payment fees by runtime payment type
        var feeCalc = BuildFeeVisitor();

        // 5) Mediator + Observer: orchestration/advisory signals
        var (mediator, events) = BuildMediatorAndEvents();

        // 6) Template + Command: algorithm skeleton and reversible steps
        var pipeline = BuildTemplatePipeline(discount, feeCalc, mediator, events);

        // 7) Facade: adapt → compose gateway → run template
        return TypedFacade<IOrderProcessingFacade>.Create()
            .Map(x => x.Place, (OrderDto dto) =>
            {
                // Adapt
                if (!dtoToOrder.TryAdapt(dto, out var order, out var err))
                    return (false, err ?? "Adapt failed", 0m);

                // Factory
                var gateway = gatewayFactory.Create(KeyFrom(dto.PaymentKind));

                // Compose context
                var ctx = new OrderContext { Order = order, Gateway = gateway };

                // Template execution
                var ok = pipeline.TryExecute(ctx, out _, out var error);
                var msg = ok ? "Order processed" : error ?? "Error";
                return (ok, msg, ctx.Total);
            })
            .Build();
    }

    private static string KeyFrom(string kind)
        => string.IsNullOrWhiteSpace(kind) ? "default" : kind.Trim().ToLowerInvariant();

    // -------- Pattern Builders --------

    /// <summary>Adapter that maps <see cref="OrderDto"/> to an <see cref="Order"/> with payment subtypes.</summary>
    private static Adapter<OrderDto, Order> BuildOrderAdapter()
        => Adapter<OrderDto, Order>.Create(seed: () => new Order { OrderId = "", CustomerId = "", Payment = new Cash(0m), Items = new() })
            .Map(static (in src, dest) => dest.OrderId = src.OrderId)
            .Map(static (in src, dest) => dest.CustomerId = src.CustomerId)
            .Map(static (in src, dest) => dest.Payment = src.PaymentKind.ToLowerInvariant() switch
            {
                "cash" => new Cash(src.Items.Sum(i => i.Price * i.Qty)),
                "card" => new Card("VISA", "4242", src.Items.Sum(i => i.Price * i.Qty)),
                _ => new Cash(src.Items.Sum(i => i.Price * i.Qty))
            })
            .Map(static (in src, dest) => dest.Items = src.Items.Select(i => new OrderItem(i.Sku, i.Name, i.Price, i.Qty, i.Category)).ToList())
            .Require(static (in src, _) => string.IsNullOrWhiteSpace(src.OrderId) ? "OrderId required" : null)
            .Require(static (in _, dest) => dest.Items.Count == 0 ? "At least one item is required" : null)
            .Build();

    /// <summary>Factory that returns a payment gateway implementation by string key.</summary>
    private static Factory<string, IPaymentGateway> BuildPaymentFactory()
        => Factory<string, IPaymentGateway>.Create(StringComparer.OrdinalIgnoreCase)
            .Map("sandbox", () => new SandboxGateway())
            .Map("stripe", () => new StripeGateway())
            .Default(() => new SandboxGateway())
            .Build();

    /// <summary>Strategy that computes order discount based on simple rules.</summary>
    private static Strategy<OrderContext, decimal> BuildDiscountStrategy()
        => Strategy<OrderContext, decimal>.Create()
            .When(static (in inCtx) => inCtx.Order.Items.Any(i => string.Equals(i.Category, "Promo", StringComparison.OrdinalIgnoreCase)))
                .Then(static (in inCtx) => inCtx.Subtotal * 0.10m) // 10% off for promo category presence
            .When(static (in inCtx) => inCtx.Order.CustomerId.StartsWith("VIP", StringComparison.OrdinalIgnoreCase))
                .Then(static (in _) => 15m) // VIP flat discount
            .Default(static (in _) => 0m)
            .Build();

    /// <summary>Visitor that calculates fees for different payment types.</summary>
    private static Visitor<Payment, decimal> BuildFeeVisitor()
        => Visitor<Payment, decimal>.Create()
            .On<Cash>(static _ => 0m)
            .On<Card>(static c => Math.Max(0.30m, Math.Round(c.Amount * 0.029m, 2)))
            .Default(static _ => 0m)
            .Build();

    /// <summary>Set up mediator and observer used for orchestration and audit.</summary>
    private static (Mediator mediator, Observer<string> events) BuildMediatorAndEvents()
    {
        var events = Observer<string>.Create().ThrowAggregate().Build();

        var mediator = Mediator.Create()
            .Pre(static (in _, _) => { /* global validation/logging */ return default; })
            .Command<GenerateReceipt, string>(static (in r, _) => new ValueTask<string>($"Receipt for {r.OrderId}: ${r.Total:F2}"))
            .Notification<OrderPlaced>((in n, _) => { events.Publish($"AUDIT: order placed {n.OrderId}"); return default; })
            .Build();

        return (mediator, events);
    }

    /// <summary>Template pipeline that computes totals and executes reversible operations via commands.</summary>
    private static Template<OrderContext, string> BuildTemplatePipeline(
        Strategy<OrderContext, decimal> discount,
        Visitor<Payment, decimal> feeCalc,
        Mediator mediator,
        Observer<string> events)
    {
        // Commands (each with optional undo)
        var reserveInventory = Command<OrderContext>.Create()
            .Do(static ctx => { ctx.Audit.Add("Reserve OK"); })
            .Undo(static ctx => { ctx.Audit.Add("Reserve UNDO"); })
            .Build();

        var chargePayment = Command<OrderContext>.Create()
            .Do(static (in ctx, ct) => Charge(ctx, ct))
            .Undo(static (in ctx, ct) => Refund(ctx, ct))
            .Build();

        var scheduleShipment = Command<OrderContext>.Create()
            .Do(static ctx => { ctx.Audit.Add("Shipment scheduled"); })
            .Undo(static ctx => { ctx.Audit.Add("Shipment cancelled"); })
            .Build();

        var macro = Command<OrderContext>.Macro()
            .Add(reserveInventory)
            .Add(chargePayment)
            .Add(scheduleShipment)
            .Build();

        // Template step computes totals, executes macro, and returns receipt text via mediator
        return Template<OrderContext, string>.Create(ctx =>
            {
                ctx.Subtotal = ctx.Order.Items.Sum(i => i.UnitPrice * i.Quantity);
                ctx.Discount = discount.Execute(in ctx);
                ctx.Fees = feeCalc.Visit(ctx.Order.Payment);

                // Run macro (throws propagate)
                macro.Execute(in ctx);

                // Publish event and generate receipt via mediator
                events.Publish($"METRIC: total={ctx.Total:F2}");
                var receipt = mediator.Send<GenerateReceipt, string>(new GenerateReceipt(ctx.Order.OrderId, ctx.Total)).GetAwaiter().GetResult();
                return receipt ?? string.Empty;
            })
            .Before(static ctx => ctx.Audit.Add("BEGIN"))
            .After(static (ctx, _) => ctx.Audit.Add("END"))
            .OnError(static (ctx, err) => ctx.Audit.Add($"ERROR: {err}"))
            .Build();
    }

    // Async helpers to avoid 'async in' lambdas
    private static async ValueTask Charge(OrderContext ctx, CancellationToken ct)
    {
        await ctx.Gateway.ChargeAsync(ctx.Total, ct);
        ctx.Audit.Add("Charged");
    }

    private static async ValueTask Refund(OrderContext ctx, CancellationToken ct)
    {
        await ctx.Gateway.RefundAsync(ctx.Total, ct);
        ctx.Audit.Add("Refunded");
    }

    // -------- Support types used by Mediator --------

    /// <summary>Notification emitted when an order is placed.</summary>
    public readonly record struct OrderPlaced(string OrderId);

    /// <summary>Command request to generate a simple receipt line.</summary>
    public readonly record struct GenerateReceipt(string OrderId, decimal Total);

    // -------- Payment gateway abstractions & impls --------

    /// <summary>Payment gateway abstraction used by the example commands.</summary>
    public interface IPaymentGateway
    {
        ValueTask ChargeAsync(decimal amount, CancellationToken ct);
        ValueTask RefundAsync(decimal amount, CancellationToken ct);
    }

    /// <summary>Sandbox gateway that only logs actions.</summary>
    public sealed class SandboxGateway : IPaymentGateway
    {
        public ValueTask ChargeAsync(decimal amount, CancellationToken ct) { return default; }
        public ValueTask RefundAsync(decimal amount, CancellationToken ct) { return default; }
    }

    /// <summary>Pretend Stripe gateway with the same interface.</summary>
    public sealed class StripeGateway : IPaymentGateway
    {
        public ValueTask ChargeAsync(decimal amount, CancellationToken ct) { return default; }
        public ValueTask RefundAsync(decimal amount, CancellationToken ct) { return default; }
    }
}
