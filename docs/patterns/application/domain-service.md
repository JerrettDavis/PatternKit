# Domain Service

Domain Service models stateless domain behavior that does not naturally belong to a single entity or value object.

Use it when a decision spans aggregates, value objects, policies, or external facts while still representing domain logic rather than application orchestration.

## Fluent Path

```csharp
using PatternKit.Application.DomainServices;

var registry = DomainServiceRegistry<ShippingRequest, ShippingDecision>.Create()
    .Add("ground", request => new ShippingDecision(request.OrderId, "ground", request.Weight * 1.25m))
    .Add("insured-air", request => new ShippingDecision(request.OrderId, "air", request.Weight * 3m))
    .Build();

var decision = registry.Execute("insured-air", request);
```

`DomainServiceOperation<TRequest,TResponse>` keeps the operation named and stateless. `DomainServiceRegistry<TRequest,TResponse>` gives application services a stable injected surface.

## Generated Path

```csharp
using PatternKit.Generators.DomainServices;

[GenerateDomainServiceRegistry(typeof(ShippingRequest), typeof(ShippingDecision))]
public static partial class ShippingServices
{
    [DomainServiceOperation("ground")]
    private static ShippingDecision Ground(ShippingRequest request)
        => new(request.OrderId, "ground", request.Weight * 1.25m);
}
```

The generator emits a registry factory so domain service operation names and signatures are compile-time checked.

## IoC Usage

```csharp
services.AddShippingDomainServiceDemo();
services.AddSingleton<CheckoutWorkflow>();
```

The example in `docs/examples/shipping-domain-service-pattern.md` shows fluent and generated domain services registered through `IServiceCollection`.
