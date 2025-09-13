using PatternKit.Creational.Builder;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Core.Creational.Builder;

[Feature("Creational - Composer<TState,TOut>")]
public sealed class ComposerTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ----------- Test state & DTO -----------
    private readonly record struct PersonState(string? Name, int Age);

    private sealed record PersonDto(string Name, int Age);

    // ----------- Seed factories -----------
    private static PersonState SeedDefault() => default; // (null, 0)
    private static PersonState SeedAda30() => new("Ada", 30);
    private static PersonState SeedSeeded() => new("Seed", 10);

    // ----------- Transformations (prefer functions over lambdas) -----------
    private static PersonState SetNameAda(PersonState s) => s with { Name = "Ada" };
    private static PersonState SetAge30(PersonState s) => s with { Age = 30 };
    private static PersonState SetAge(PersonState s, int age) => s with { Age = age };
    private static PersonState SetName(PersonState s, string? name) => s with { Name = name };

    // ----------- Validations -----------
    private static string? ValidateNameRequired(PersonState s)
        => string.IsNullOrWhiteSpace(s.Name) ? "Name is required." : null;

    private static string? ValidateAge0To130(PersonState s)
        => s.Age is < 0 or > 130 ? $"Age must be within [0, 130] but was {s.Age}." : null;

    private static string? AlwaysOk(PersonState _) => null;

    // ----------- Projection -----------
    private static PersonDto Project(PersonState s) => new(s.Name!, s.Age);

    // ---------- Helpers ----------
    private static Composer<PersonState, PersonDto> NewComposer(Func<PersonState> seed)
        => Composer<PersonState, PersonDto>.New(seed);

    // ============================================================
    // Tests
    // ============================================================

    [Scenario("Transformations are applied in order and projection produces the final DTO")]
    [Fact]
    public async Task Compose_Applies_Transforms_In_Order_And_Projects()
    {
        await Given("a composer seeded with default state", () => NewComposer(SeedDefault))
            .When("adding SetNameAda then SetAge30 and a pass validation", c =>
                c.With(SetNameAda)
                    .With(SetAge30)
                    .Require(AlwaysOk))
            .And("building to DTO", c => c.Build(Project))
            .Then("DTO has Name='Ada' and Age=30", dto => dto is { Name: "Ada", Age: 30 })
            .AssertPassed();
    }

    [Scenario("Validation failure throws with message")]
    [Fact]
    public async Task Require_Throws_On_Validation_Failure()
    {
        await Given("a composer seeded with default state", () => NewComposer(SeedDefault))
            .When("adding only age but missing name, and requiring a non-empty name", c =>
                c.With(SetAge30).Require(ValidateNameRequired))
            .And("building to DTO (should throw)", c => Record.Exception(() => c.Build(Project)))
            .Then("throws InvalidOperationException with 'Name is required.'",
                ex => ex is InvalidOperationException ioe && ioe.Message == "Name is required.")
            .AssertPassed();
    }

    [Scenario("Multiple validations - first failure message is thrown")]
    [Fact]
    public async Task Multiple_Validations_First_Failure_Message()
    {
        static string? FirstFailure(PersonState s) => "boom 1";
        static string? SecondFailure(PersonState s) => "boom 2";

        await Given("a composer seeded with Ada/30", () => NewComposer(SeedAda30))
            .When("adding two validators that both fail", c =>
                c.Require(FirstFailure).Require(SecondFailure))
            .And("building to DTO", c => Record.Exception(() => c.Build(Project)))
            .Then("the first validator's message is thrown",
                ex => ex is InvalidOperationException ioe && ioe.Message == "boom 1")
            .AssertPassed();
    }

    [Scenario("No transformations uses the seed state")]
    [Fact]
    public async Task No_Transforms_Uses_Seed()
    {
        await Given("a composer seeded with Name='Seed', Age=10", () => NewComposer(SeedSeeded))
            .When("adding a pass validation only", c => c.Require(AlwaysOk))
            .And("building to DTO", c => c.Build(Project))
            .Then("DTO reflects the seed values", dto => dto is { Name: "Seed", Age: 10 })
            .AssertPassed();
    }

    [Scenario("Composer can be reused; subsequent builds reflect additional With steps")]
    [Fact]
    public async Task Composer_Can_Be_Reused()
    {
        await Given("a composer seeded with default", () => NewComposer(SeedDefault))
            .When("adding SetNameAda", c => c.With(SetNameAda))
            .And("building first DTO", c => (composer: c, first: c.Build(Project)))
            .And("adding SetAge30", t =>
            {
                t.composer.With(SetAge30);
                return t;
            })
            .And("building second DTO", t => (t.first, second: t.composer.Build(Project)))
            .Then("first DTO only has the first transform applied",
                t => t.first is { Name: "Ada", Age: 0 })
            .And("second DTO has both transforms applied",
                t => t.second is { Name: "Ada", Age: 30 })
            .AssertPassed();
    }

    [Scenario("Validation of age range with transformation in pipeline")]
    [Fact]
    public async Task Age_Range_Validation_Works()
    {
        await Given("a composer seeded with default", () => NewComposer(SeedDefault))
            .When("setting name and invalid age, then requiring valid age range", c =>
                c.With(static s => SetName(s, "Bob"))
                    .With(static s => SetAge(s, -5))
                    .Require(ValidateAge0To130))
            .And("building to DTO", c => Record.Exception(() => c.Build(Project)))
            .Then("should throw with range message",
                ex => ex is InvalidOperationException ioe &&
                      ioe.Message == "Age must be within [0, 130] but was -5.")
            .AssertPassed();
    }

    [Scenario("Transformation composition is left-to-right (b(a(seed)))")]
    [Fact]
    public async Task Composition_Is_Left_To_Right()
    {
        // a: set age to 10, b: set age to 20. b should win if composed as b(a(seed)).
        static PersonState A(PersonState s) => SetAge(s, 10);
        static PersonState B(PersonState s) => SetAge(s, 20);

        await Given("a composer seeded with default", () => NewComposer(SeedDefault))
            .When("adding A then B with pass validation", c => c.With(A).With(B).Require(AlwaysOk))
            .And("building", c => c.Build(Project))
            .Then("age should be 20 (B after A)", dto => dto.Age == 20)
            .AssertPassed();
    }
}