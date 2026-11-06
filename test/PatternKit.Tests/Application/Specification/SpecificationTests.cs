using PatternKit.Application.Specification;
using TinyBDD;

namespace PatternKit.Tests.Application.Specification;

public sealed class SpecificationTests
{
    private sealed record Applicant(string Tier, decimal Income, int CreditScore, bool HasFraudHold);

    [Scenario("Specification Evaluates Predicate And Exposes Predicate Delegate")]
    [Fact]
    public void Specification_Evaluates_Predicate_And_Exposes_Predicate_Delegate()
    {
        var spec = Specification<Applicant>.Where("high-credit", static applicant => applicant.CreditScore >= 700);
        var applicant = new Applicant("standard", 80_000m, 720, false);

        ScenarioExpect.Equal("high-credit", spec.Name);
        ScenarioExpect.True(spec.IsSatisfiedBy(applicant));
        ScenarioExpect.True(spec.ToPredicate()(applicant));
    }

    [Scenario("Specification Composes And Or Not")]
    [Fact]
    public void Specification_Composes_And_Or_Not()
    {
        var highIncome = Specification<Applicant>.Where("high-income", static applicant => applicant.Income >= 100_000m);
        var goldTier = Specification<Applicant>.Where("gold-tier", static applicant => applicant.Tier == "Gold");
        var fraudHold = Specification<Applicant>.Where("fraud-hold", static applicant => applicant.HasFraudHold);
        var eligible = highIncome.Or(goldTier, "income-or-tier").And(fraudHold.Not("clear"), "eligible");

        ScenarioExpect.True(eligible.IsSatisfiedBy(new Applicant("Gold", 50_000m, 650, false)));
        ScenarioExpect.True(eligible.IsSatisfiedBy(new Applicant("standard", 120_000m, 650, false)));
        ScenarioExpect.False(eligible.IsSatisfiedBy(new Applicant("Gold", 120_000m, 650, true)));
        ScenarioExpect.False(eligible.IsSatisfiedBy(new Applicant("standard", 50_000m, 650, false)));
    }

    [Scenario("Specification Registry Resolves Named Rules")]
    [Fact]
    public void Specification_Registry_Resolves_Named_Rules()
    {
        var registry = SpecificationRegistry<Applicant>.Create()
            .Add("all", Specification<Applicant>.All())
            .Add("none", Specification<Applicant>.None())
            .Add("prime", static applicant => applicant.CreditScore >= 720 && applicant.Income >= 75_000m)
            .Build();
        var applicant = new Applicant("standard", 80_000m, 730, false);

        ScenarioExpect.Equal(["all", "none", "prime"], registry.Names.OrderBy(static name => name).ToArray());
        ScenarioExpect.True(registry.Get("prime").IsSatisfiedBy(applicant));
        ScenarioExpect.True(registry.IsSatisfiedBy("all", applicant));
        ScenarioExpect.False(registry.IsSatisfiedBy("none", applicant));
        ScenarioExpect.Throws<KeyNotFoundException>(() => registry.Get("missing"));
    }

    [Scenario("Specification Rejects Invalid Construction")]
    [Fact]
    public void Specification_Rejects_Invalid_Construction()
    {
        ScenarioExpect.Throws<ArgumentException>(() => Specification<Applicant>.Where("", static _ => true));
        ScenarioExpect.Throws<ArgumentNullException>(() => Specification<Applicant>.Where("rule", null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => Specification<Applicant>.All().And(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => Specification<Applicant>.All().Or(null!));
        ScenarioExpect.Throws<ArgumentException>(() => SpecificationRegistry<Applicant>.Create().Add("", static _ => true));
        ScenarioExpect.Throws<ArgumentNullException>(() => SpecificationRegistry<Applicant>.Create().Add("rule", (ISpecification<Applicant>)null!));
    }
}
