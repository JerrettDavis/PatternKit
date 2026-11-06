using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.ValueObjects;
using PatternKit.Generators.ValueObjects;

namespace PatternKit.Examples.ValueObjectDemo;

/// <summary>
/// Production-style value objects for order pricing and identifiers.
/// </summary>
public static class OrderValueObjectDemo
{
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
                .Create(() => new Money(amount, currency.Trim().ToUpperInvariant()))
                .Ensure("positive-amount", static money => money.Amount > 0m, "Amount must be positive.")
                .Ensure("supported-currency", static money => money.Currency is "USD" or "EUR", "Currency must be supported.")
                .Build();

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    public sealed record PricedOrder(string OrderId, Money Total, GeneratedOrderNumber GeneratedOrderNumber);

    public static ValueObjectResult<PricedOrder> PriceOrder(string orderId, decimal amount, string currency)
    {
        var total = Money.Create(amount, currency);
        var generated = GeneratedOrderNumber.Create(orderId.Trim().ToUpperInvariant(), "ONLINE");

        return total.IsValid
            ? ValueObjectResult<PricedOrder>.Success(new PricedOrder(orderId, total.Value, generated))
            : ValueObjectResult<PricedOrder>.Failure(new PricedOrder(orderId, total.Value, generated), total.Failures);
    }

    public static IServiceCollection AddOrderValueObjectDemo(this IServiceCollection services)
    {
        services.AddSingleton<OrderValueObjectService>();
        return services;
    }
}

public sealed class OrderValueObjectService
{
    public ValueObjectResult<OrderValueObjectDemo.PricedOrder> Price(string orderId, decimal amount, string currency)
        => OrderValueObjectDemo.PriceOrder(orderId, amount, currency);
}

[GenerateValueObject]
public sealed partial class GeneratedOrderNumber
{
    private GeneratedOrderNumber(string number, string channel)
    {
        Number = number;
        Channel = channel;
    }

    [ValueObjectComponent]
    public string Number { get; }

    [ValueObjectComponent]
    public string Channel { get; }
}
