# Enterprise Feature Slices with .NET DI

This demo shows PatternKit artifacts registered through the standard
`Microsoft.Extensions.DependencyInjection` container and consumed by a
task-focused application facade.

Source: `src/PatternKit.Examples/EnterpriseFeatureSlices/EnterpriseFeatureSlicesDemo.cs`

Tests: `test/PatternKit.Examples.Tests/EnterpriseFeatureSlices/EnterpriseFeatureSlicesDemoTests.cs`

## Scenario

A checkout feature slice owns the business workflow for placing and estimating
orders. The composition root registers small immutable PatternKit artifacts as
singletons, then exposes `IEnterpriseCheckout` through a typed facade.

```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.EnterpriseFeatureSlices;

using var provider = EnterpriseFeatureSlicesDemo.BuildServiceProvider();

var checkout = provider.GetRequiredService<EnterpriseFeatureSlicesDemo.IEnterpriseCheckout>();
var result = checkout.Place(EnterpriseFeatureSlicesDemo.CreateRetailRequest());
```

## Composition Root

`AddEnterpriseFeatureSlices` is the important part of the demo. It wires the
feature through the normal .NET container:

```csharp
services.AddSingleton(CreateCatalog());
services.AddSingleton(CreateFulfillmentFactory());
services.AddSingleton(CreateCheckoutPrototype());
services.AddSingleton(CreateDiscountStrategy());
services.AddSingleton(CreateValidationChain());
services.AddSingleton(CreateFulfillmentRenderer());
services.AddSingleton(CreateCheckoutStateMachine());
services.AddSingleton(CreateCheckoutHistory());
services.AddSingleton<Observer<CheckoutEvent>>(sp =>
    CreateCheckoutObserver(sp.GetRequiredService<AuditLog>()));
services.AddSingleton<AbstractFactory<Region>>(sp =>
    CreateRegionalFactory(sp.GetRequiredService<AuditLog>()));
services.AddSingleton<Proxy<PaymentCharge, PaymentReceipt>>(sp =>
    CreatePaymentProxy(
        sp.GetRequiredService<AbstractFactory<Region>>(),
        sp.GetRequiredService<AuditLog>()));
```

The final registration maps the internal service behind a typed facade:

```csharp
services.AddSingleton<IEnterpriseCheckout>(sp =>
{
    var service = new EnterpriseCheckoutService(...);

    return TypedFacade<IEnterpriseCheckout>.Create()
        .Map<CheckoutRequest, CheckoutResult>(x => x.Place, request => service.Place(request))
        .Map<CheckoutRequest, CheckoutResult>(x => x.Estimate, request => service.Estimate(request))
        .Build();
});
```

## Pattern Map

| Pattern | Role |
| --- | --- |
| Flyweight | Catalog metadata is shared by SKU and lazily creates special-order items. |
| Factory | Fulfillment instructions are created from product kind and line context. |
| Prototype | Checkout contexts are cloned from a configured template. |
| ResultChain | Validation rules short-circuit invalid orders. |
| Strategy | Customer tier and order size select discount policy. |
| Decorator | Pricing layers subtotal, shipping, regional tax, and discount. |
| Abstract Factory | Each region supplies compatible tax, payment, and risk services. |
| Proxy | Payment calls are audited and risk-gated before reaching the gateway. |
| TypeDispatcher | Fulfillment instructions render by concrete work-item type. |
| State Machine | Checkout lifecycle moves through received, priced, paid, queued, or rejected. |
| Memento | Checkout snapshots capture received, priced, queued, and rejected milestones. |
| Observer | Domain events feed audit and fulfillment notification subscribers. |
| Iterator/Flow | Fulfillment work is filtered and rendered lazily before queueing. |
| Typed Facade | Application consumers see a narrow `IEnterpriseCheckout` contract. |

## Why This Shape

This is intentionally close to a production feature slice:

* The feature has one public contract: `IEnterpriseCheckout`.
* PatternKit objects are immutable after build and safe to share as singletons.
* Regional policies are grouped with `AbstractFactory`, so tax, payment, and risk
  stay compatible.
* Cross-cutting concerns stay at the edge: the payment `Proxy` records audit data
  and handles manual-review gating.
* Tests resolve the feature through `ServiceProvider`, not by manually newing each
  dependency.

## Covered Scenarios

The TinyBDD tests validate:

* the standard DI container can resolve the facade and PatternKit artifacts;
* a mixed physical, digital, and subscription order reaches fulfillment;
* estimate mode prices without charging or queueing work;
* electronic fulfillment rejects physical goods before payment;
* high-value payments require manager override through the payment proxy.

## See Also

* [Enterprise Order Processing Demo](enterprise-order.md)
* [Configuration-Driven Transaction Pipeline](config-driven-transaction-pipeline.md)
* [Source Generator Application Suite](source-generator-application-suite.md)
* [Messaging Backplane Facade](messaging-backplane-facade.md)
