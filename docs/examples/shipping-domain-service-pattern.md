# Shipping Domain Service Pattern

This example demonstrates a production-style Domain Service for shipping decisions. It includes a fluent operation registry, a source-generated operation registry, TinyBDD tests, and an `IServiceCollection` extension.

## Import

```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DomainServiceDemo;

var services = new ServiceCollection();
services.AddShippingDomainServiceDemo();

using var provider = services.BuildServiceProvider(validateScopes: true);
var service = provider.GetRequiredService<ShippingDomainService>();
```

## Use

```csharp
var decision = service.SelectBest(ShippingDomainServiceDemo.CreateHighValueRequest());
```

The service uses a generated `DomainServiceRegistry<ShippingRequest, ShippingDecision>`. The fluent and generated routes share the same carrier and insurance rules so teams can compare runtime and generated composition without changing domain behavior.

## Production Notes

- Keep domain services stateless.
- Keep operation names stable because callers and tests use them as domain vocabulary.
- Register the example with `AddShippingDomainServiceDemo()` or import all examples with `AddPatternKitExamples()`.
