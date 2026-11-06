# Domain Service Generator

The Domain Service generator turns annotated static methods into a named `DomainServiceRegistry<TRequest,TResponse>` factory.

## Usage

```csharp
using PatternKit.Generators.DomainServices;

[GenerateDomainServiceRegistry(typeof(ShippingRequest), typeof(ShippingDecision), FactoryMethodName = "Build")]
public static partial class ShippingServices
{
    [DomainServiceOperation("insured-air")]
    private static ShippingDecision InsuredAir(ShippingRequest request)
        => new(request.OrderId, "air", request.Weight * 3m);
}
```

Generated output:

```csharp
var registry = ShippingServices.Build();
var decision = registry.Execute("insured-air", request);
```

## Operation Shape

Domain service operations must be static methods with this shape:

```csharp
static TResponse Operation(TRequest request)
```

Operation names must be unique within the generated registry.

## Diagnostics

| ID | Meaning |
|---|---|
| `PKDOM001` | Host type must be `partial`. |
| `PKDOM002` | Host type has no `[DomainServiceOperation]` methods. |
| `PKDOM003` | Operation method signature is invalid. |
| `PKDOM004` | Operation name is duplicated. |
