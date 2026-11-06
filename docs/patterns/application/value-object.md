# Value Object

Value Object models domain concepts whose identity is their component values, such as money, order numbers, addresses, measurements, and identifiers.

Use it when equality, hashing, and validation should be explicit and shared across application services instead of repeated as ad hoc primitive checks.

## Fluent Path

```csharp
using PatternKit.Application.ValueObjects;

public sealed class Money : ValueObject<Money>
{
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }
    public string Currency { get; }

    public static ValueObjectResult<Money> Create(decimal amount, string currency)
        => ValueObjectFactory<Money>
            .Create(() => new Money(amount, currency))
            .Ensure("positive-amount", money => money.Amount > 0m, "Amount must be positive.")
            .Build();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
```

`ValueObject<TSelf>` centralizes component equality and hashing. `ValueObjectFactory<T>` gives teams a named validation surface that can be tested and documented alongside the domain model.

## Generated Path

```csharp
using PatternKit.Generators.ValueObjects;

[GenerateValueObject]
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

The generator emits a factory, equality implementation, hash code implementation, and equality operators from `[ValueObjectComponent]` properties.

## IoC Usage

```csharp
services.AddOrderValueObjectDemo();
services.AddSingleton<OrderPricingService>();
```

The example in `docs/examples/order-value-object-pattern.md` demonstrates fluent and generated value objects registered through standard `IServiceCollection`.
