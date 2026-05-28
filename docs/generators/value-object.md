# Value Object Generator

The Value Object generator turns annotated partial classes into component-based value objects.

## Usage

```csharp
using PatternKit.Generators.ValueObjects;

[GenerateValueObject(FactoryMethodName = "From")]
public sealed partial class OrderNumber
{
    private OrderNumber(string number, string channel)
    {
        Number = number;
        Channel = channel;
    }

    [ValueObjectComponent]
    public string Number { get; }

    [ValueObjectComponent]
    public string Channel { get; }
}
```

Generated output includes:

```csharp
var first = OrderNumber.From("ORD-100", "ONLINE");
var second = OrderNumber.From("ORD-100", "ONLINE");
var same = first == second;
```

## Component Shape

Components are instance properties annotated with `[ValueObjectComponent]`. The generator preserves declaration order for the generated factory parameters and hash code composition.

## Diagnostics

| ID | Meaning |
|---|---|
| `PKVO001` | Host type must be `partial`. |
| `PKVO002` | Host type must be a class. |
| `PKVO003` | Host type has no `[ValueObjectComponent]` properties. |

## Dependency Injection

Generated value objects are allocation-light domain primitives. Register services that consume them through `IServiceCollection`, and keep creation inside application services or domain factories.
