using PatternKit.Creational.Builder;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Core.Creational.Builder;

[Feature("Creational - MutableBuilder<T>")]
public sealed class MutableBuilderTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ---------- Test model ----------
    private sealed class Person
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public List<string> Steps { get; } = [];
    }

    // ---------- Factories ----------
    private static Person NewPerson() => new();
    private static MutableBuilder<Person> NewBuilder() => MutableBuilder<Person>.New(static () => NewPerson());

    // ---------- Mutations ----------
    private static void SetNameAda(Person p) => p.Name = "Ada";
    private static void SetNameEmpty(Person p) => p.Name = "";
    private static void SetAge30(Person p) => p.Age = 30;
    private static void SetAgeNeg1(Person p) => p.Age = -1;
    private static void AppendStepA(Person p) => p.Steps.Add("A");
    private static void AppendStepB(Person p) => p.Steps.Add("B");

    // ---------- Validations ----------
    private static string? ValidateOk(Person _) => null;

    // ---------- Helpers ----------
    private static Person BuildPerson(MutableBuilder<Person> b) => b.Build();

    [Scenario("Build applies mutations and passes validations")]
    [Fact]
    public async Task Build_Succeeds_WithMutations_AndValidations()
    {
        await Given("a fresh builder", NewBuilder)
            .When("configuring name, age, and a pass-through validator",
                b => b.With(SetNameAda)
                      .With(SetAge30)
                      .Require(ValidateOk))
            .And("building the person", BuildPerson)
            .Then("the result should contain applied values",
                p => p is { Name: "Ada", Age: 30 })
            .AssertPassed();
    }

    [Scenario("First failing validation throws with its message")]
    [Fact]
    public async Task Build_Throws_OnFirstValidationFailure()
    {
        await Given("a builder with an empty name and a non-empty requirement", NewBuilder)
            .When("configuring invalid name and RequireNotEmpty(Name)",
                b => b.With(SetNameEmpty)
                      .RequireNotEmpty(static p => p.Name, nameof(Person.Name)))
            .And("attempting to build", b => Record.Exception(() => b.Build()))
            .Then("should throw InvalidOperationException with expected message",
                ex => ex is InvalidOperationException { Message: "Name must be non-empty." })
            .AssertPassed();
    }

    [Scenario("Range validation using stateful Require overload (no captures)")]
    [Fact]
    public async Task Build_Throws_OnStatefulRangeValidation()
    {
        await Given("a builder with age 30 and a stateful range requirement [40, 120]", NewBuilder)
            .When("configuring age and adding stateful validator",
                b => b.With(SetAge30)
                      .Require((min: 40, max: 120, name: nameof(Person.Age)),
                               static (x, s) =>
                               {
                                   var v = x.Age;
                                   return (v < s.min || v > s.max)
                                       ? $"{s.name} must be within [{s.min}, {s.max}] but was {v}."
                                       : null;
                               }))
            .And("attempting to build", b => Record.Exception(() => b.Build()))
            .Then("should throw with range message",
                ex => ex is InvalidOperationException { Message: "Age must be within [40, 120] but was 30." })
            .AssertPassed();
    }

    [Scenario("RequireRange extension validates inclusive bounds")]
    [Fact]
    public async Task RequireRange_Extension_Works()
    {
        await Given("a builder with age -1 and RequireRange [0,130]", NewBuilder)
            .When("configuring age and adding RequireRange",
                b => b.With(SetAgeNeg1)
                      .RequireRange(static p => p.Age, 0, 130, nameof(Person.Age)))
            .And("attempting to build", b => Record.Exception(() => b.Build()))
            .Then("should throw with inclusive bounds message",
                ex => ex is InvalidOperationException { Message: "Age must be within [0, 130] but was -1." })
            .AssertPassed();
    }

    [Scenario("Mutation order is preserved")]
    [Fact]
    public async Task Mutations_Are_Applied_In_Order()
    {
        await Given("a builder that records steps A then B", NewBuilder)
            .When("adding two mutations", b => b.With(AppendStepA).With(AppendStepB))
            .And("building the person", BuildPerson)
            .Then("steps should be applied in order", p => string.Join("", p.Steps) == "AB")
            .AssertPassed();
    }

    [Scenario("Builder can be reused; subsequent builds reflect added mutations")]
    [Fact]
    public async Task Builder_Can_Be_Reused()
    {
        await Given("a builder with Name='Ada'", NewBuilder)
            .When("adding initial mutation", b => b.With(SetNameAda))
            .And("building once", b => (builder: b, first: b.Build()))
            .And("adding a second mutation (Age=30)", t => { t.builder.With(SetAge30); return t; })
            .And("building again", t => (t.first, second: t.builder.Build()))
            .Then("first build has only first mutation applied",
                  t => t.first is { Name: "Ada", Age: 0 })
            .And("second build has both mutations applied",
                  t => t.second is { Name: "Ada", Age: 30 })
            .AssertPassed();
    }

    [Scenario("No mutations or validations returns factory instance")]
    [Fact]
    public async Task Build_Returns_Factory_Result_When_No_Config()
    {
        await Given("a fresh builder with default factory", NewBuilder)
            .When("building immediately", BuildPerson)
            .Then("result is a Person with default values",
                p => p is { Name: null, Age: 0, Steps.Count: 0 })
            .AssertPassed();
    }
}
