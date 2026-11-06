# Order Value Object Pattern

This example demonstrates production-style value objects for order pricing. It includes a fluent `Money` value object, a source-generated `GeneratedOrderNumber`, TinyBDD tests, and an `IServiceCollection` extension.

## Import

```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.ValueObjectDemo;

var services = new ServiceCollection();
services.AddOrderValueObjectDemo();

using var provider = services.BuildServiceProvider(validateScopes: true);
var service = provider.GetRequiredService<OrderValueObjectService>();
```

## Use

```csharp
var order = service.Price("ord-100", 25m, "usd");
```

The fluent path validates money with named rules. The generated path provides the order-number factory, equality, hash code, and operators from `[ValueObjectComponent]` properties.

## Production Notes

- Normalize primitives before constructing value objects.
- Keep validation failures named so APIs, jobs, and tests can assert exact domain rules.
- Register the example with `AddOrderValueObjectDemo()` or import all examples with `AddPatternKitExamples()`.
