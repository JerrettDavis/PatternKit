using PatternKit.Application.ValueObjects;
using TinyBDD;

namespace PatternKit.Tests.Application.ValueObjects;

public sealed class ValueObjectTests
{
    private sealed class Money(decimal amount, string currency) : ValueObject<Money>
    {
        public decimal Amount { get; } = amount;
        public string Currency { get; } = currency;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    [Scenario("Value object compares instances by components")]
    [Fact]
    public void Value_Object_Compares_Instances_By_Components()
    {
        var usd = new Money(12.50m, "USD");
        var sameUsd = new Money(12.50m, "USD");
        var eur = new Money(12.50m, "EUR");

        ScenarioExpect.True(usd.Equals(sameUsd));
        ScenarioExpect.True(usd == sameUsd);
        ScenarioExpect.False(usd.Equals(eur));
        ScenarioExpect.True(usd != eur);
        ScenarioExpect.Equal(usd.GetHashCode(), sameUsd.GetHashCode());
    }

    [Scenario("Value object factory returns validation failures")]
    [Fact]
    public void Value_Object_Factory_Returns_Validation_Failures()
    {
        var result = ValueObjectFactory<Money>
            .Create(static () => new Money(-4m, ""))
            .Ensure("positive-amount", static money => money.Amount > 0m, "Amount must be positive.")
            .Ensure("currency-required", static money => !string.IsNullOrWhiteSpace(money.Currency), "Currency is required.")
            .Build();

        ScenarioExpect.False(result.IsValid);
        ScenarioExpect.Equal(["currency-required", "positive-amount"], result.Failures.Select(static failure => failure.Rule).OrderBy(static rule => rule).ToArray());
    }

    [Scenario("Value object factory rejects invalid rules")]
    [Fact]
    public void Value_Object_Factory_Rejects_Invalid_Rules()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() => ValueObjectFactory<Money>.Create(null!));
        ScenarioExpect.Throws<ArgumentException>(() => ValueObjectFactory<Money>.Create(static () => new Money(1m, "USD")).Ensure("", static _ => true, "valid"));
        ScenarioExpect.Throws<ArgumentNullException>(() => ValueObjectFactory<Money>.Create(static () => new Money(1m, "USD")).Ensure("rule", null!, "valid"));
        ScenarioExpect.Throws<ArgumentException>(() => ValueObjectFactory<Money>.Create(static () => new Money(1m, "USD")).Ensure("rule", static _ => true, ""));
    }
}
