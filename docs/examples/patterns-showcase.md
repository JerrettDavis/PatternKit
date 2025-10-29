# Patterns Showcase — Integrated Order Processing

This example composes many PatternKit patterns in a single, realistic flow: adapting an external order, selecting behavior, executing reversible operations, and coordinating notifications — all behind a simple facade.

Source: `src/PatternKit.Examples/PatternShowcase/PatternShowcase.cs:1`

---

## Scenario

- Adapt external `OrderDto` to an internal `Order` model.
- Select discounts based on rules and apply payment fees by type.
- Execute a reversible command pipeline (reserve → charge → schedule) under a template.
- Publish audit/metric events and generate a receipt via mediator.
- Expose everything through a typed facade that’s easy to call from a controller/service.

---

## Quick Start

```csharp
// Build once at startup
var facade = PatternKit.Examples.PatternShowcase.PatternShowcase.Build();

// Incoming request → DTO
var dto = new PatternKit.Examples.PatternShowcase.PatternShowcase.OrderDto(
    OrderId: "ORD-1001",
    CustomerId: "VIP-42",
    PaymentKind: "card",
    Items: new [] {
        new PatternKit.Examples.PatternShowcase.PatternShowcase.OrderItemDto("SKU-1","Widget", 49.99m, 2, "Promo")
    });

var (ok, message, total) = facade.Place(dto);
// ok == true, message == "Order processed", total reflects discount + fees
```

---

## Pattern Map (Who Does What)

- Adapter — `OrderDto` → `Order`: field mapping + validation.
- Factory — `IPaymentGateway` selection by key: `sandbox`, `stripe`, default.
- Strategy — Discount rules over `OrderContext` (first match wins).
- Visitor — Payment fee by runtime type (`Cash`, `Card`).
- Template — Algorithm skeleton: compute totals → execute commands → emit events/receipt.
- Command — Reversible steps: reserve inventory, charge payment, schedule shipment, composed as a macro.
- Mediator — Generate receipt and emit notifications (decoupled handlers/pre/post behaviors).
- Observer — Audit/metric event hub for subscribers.
- Facade — Typed API that hides the composition and exposes `Place(OrderDto)`.

---

## Similarities And Differences

- Visitor vs Strategy
  - Similar: both route to behavior based on a condition.
  - Different: Visitor dispatches by runtime type; Strategy dispatches by boolean predicates. Use Visitor when the type tells you the behavior; Strategy when rules are data/condition‑driven.

- Adapter vs Facade
  - Similar: both present a friendlier surface area.
  - Different: Adapter transforms data shape; Facade aggregates operations behind a typed contract. Use Adapter at system boundaries, Facade to simplify internal orchestration.

- Command vs Template
  - Similar: both structure execution.
  - Different: Command encapsulates a unit of work (with optional undo); Template defines a skeleton with hooks. Use Command for reversible steps and composition; Template for consistent flow with before/after/error hooks.

- Mediator vs Observer
  - Similar: decouple senders from receivers.
  - Different: Mediator is request/response + pipeline behaviors; Observer is pub/sub broadcast. Use Mediator for commands/queries, Observer for fan‑out notifications.

- Factory vs Visitor
  - Similar: both pick a concrete implementation.
  - Different: Factory chooses a product by key; Visitor chooses a handler by input type at runtime. Use Factory for creation, Visitor for behavior.

---

## Best‑Fit Guidance

- Type‑based behavior → Visitor (`Payment` handling).
- Rule‑based selection → Strategy (`OrderContext` discounts).
- Boundary transformation → Adapter (`OrderDto` to `Order`).
- Consistent flow with hooks → Template (compute totals, run commands, report).
- Reversible operations or macro steps → Command (with undo, macro composition).
- Creation by key/config → Factory (`IPaymentGateway`).
- Cross‑cutting orchestration → Mediator (receipt generation, behaviors).
- Broadcast events → Observer (AUDIT/METRIC).
- Simplified client API → Facade (`IOrderProcessingFacade`).

---

## Code Pointers

- Adapter: `BuildOrderAdapter` — validates `OrderId` and requires ≥1 item.
- Factory: `BuildPaymentFactory` — string key to `IPaymentGateway` mapping.
- Strategy: `BuildDiscountStrategy` — promo category or VIP id.
- Visitor: `BuildFeeVisitor` — 2.9% card fee with min $0.30, $0 for cash.
- Template + Commands: `BuildTemplatePipeline` — reserve → charge → schedule; error hook appends to audit.
- Mediator + Observer: `BuildMediatorAndEvents` — receipt command + AUDIT publish.
- Facade: `Build()` — wires everything together.

---

## Extending The Showcase

- Add fraud checks as a `Strategy<OrderContext, bool>` to gate the command pipeline.
- Make shipping asynchronous via `AsyncActionVisitor<Tender>` or mediator streams.
- Snapshot/restore the context with `Memento<T>` when you need full state time‑travel beyond command undo.

