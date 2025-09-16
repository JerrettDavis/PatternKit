using PatternKit.Creational.Prototype;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Creational.Prototype;

[Feature("Creational - Prototype<T> & Prototype<TKey,T>")]
public sealed class PrototypeTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private enum Kind { A, B }
    private sealed class Thing
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public Thing(string name, int value) { Name = name; Value = value; }
    }

    private static Thing Clone(in Thing s) => new(s.Name, s.Value); // deep-enough for test

    // ---------------- Single prototype ----------------

    [Scenario("Single prototype: default and per-call mutations are applied")]
    [Fact]
    public Task Single_Default_And_PerCall_Mutations()
        => Given("a prototype with Value++ default mutation", () =>
                Prototype<Thing>.Create(new Thing("x", 1), Clone)
                    .With(t => t.Value++)
                    .Build())
            .When("Create()", p => p.Create())
            .Then("Value is 2", t => t is { Name: "x", Value: 2 })
            .When("Create with per-call +10", _ =>
            {
                var p = Prototype<Thing>.Create(new Thing("y", 5), Clone)
                    .With(t => t.Value++)
                    .Build();
                return p.Create(t => t.Value += 10);
            })
            .Then("Value is 16 (1 + 10)", t => t is { Name: "y", Value: 16 })
            .AssertPassed();

    [Scenario("Single prototype: null per-call mutation is ignored")]
    [Fact]
    public Task Single_Null_PerCall_Mutation()
        => Given("a prototype with default mutation Value++", () =>
                Prototype<Thing>.Create(new Thing("n", 3), Clone).With(t => t.Value++).Build())
            .When("Create(null mutate)", p => p.Create(null!))
            .Then("Value is 4", t => t.Value == 4)
            .AssertPassed();

    [Scenario("Single prototype: multiple default mutations preserve order")]
    [Fact]
    public Task Single_Default_Mutations_Order()
        => Given("a prototype with two default mutations (+1, *2)", () =>
                Prototype<Thing>.Create(new Thing("o", 2), Clone)
                    .With(t => t.Value += 1)
                    .With(t => t.Value *= 2)
                    .Build())
            .When("Create()", p => p.Create())
            .Then("(2+1)*2 = 6", t => t.Value == 6)
            .AssertPassed();

    // ---------------- Registry prototype ----------------

    [Scenario("Registry: Map + Default + TryCreate semantics")]
    [Fact]
    public Task Registry_Map_Default_TryCreate()
        => Given("a registry with A mapped and a default", () =>
                Prototype<Kind, Thing>.Create()
                    .Map(Kind.A, new Thing("A", 1), Clone)
                    .Default(new Thing("D", 0), Clone)
                    .Build())
            .When("Create(A) and Create(B)", r => (Reg: r, A: r.Create(Kind.A), B: r.Create(Kind.B)))
            .Then("A uses A source", t => t.A is { Name: "A", Value: 1 })
            .And("B uses default", t => t.B is { Name: "D", Value: 0 })
            .When("TryCreate unknown", t => t.Reg.TryCreate((Kind)999, out var v) ? v : null!)
            .Then("falls back to default (non-null)", v => v is not null)
            .AssertPassed();

    [Scenario("Registry: TryCreate false when missing and no default")]
    [Fact]
    public Task Registry_TryCreate_NoDefault_ReturnsFalse()
        => Given("a registry with only A mapped", () =>
                Prototype<Kind, Thing>.Create().Map(Kind.A, new Thing("A", 1), Clone).Build())
            .When("TryCreate B", r => r.TryCreate(Kind.B, out var _))
            .Then("false", ok => ok == false)
            .AssertPassed();

    [Scenario("Registry: Create throws when missing and no default (no mutate)")]
    [Fact]
    public Task Registry_Create_NoDefault_Throws()
        => Given("a registry with only A mapped", () =>
                Prototype<Kind, Thing>.Create().Map(Kind.A, new Thing("A", 1), Clone).Build())
            .When("Create B", r => Record.Exception(() => r.Create(Kind.B)))
            .Then("InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Registry: Create with per-call mutate throws when missing and no default")]
    [Fact]
    public Task Registry_Create_WithMutate_NoDefault_Throws()
        => Given("a registry with only A mapped", () =>
                Prototype<Kind, Thing>.Create().Map(Kind.A, new Thing("A", 1), Clone).Build())
            .When("Create B with per-call", r => Record.Exception(() => r.Create(Kind.B, t => t.Value++)))
            .Then("InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Registry: per-call mutate applies on default path and after default mutations")]
    [Fact]
    public Task Registry_PerCall_On_Default_Path_After_DefaultMutations()
        => Given("default has Value+=2; no mapping for B", () =>
                Prototype<Kind, Thing>.Create()
                    .Default(new Thing("D", 1), Clone)
                    .DefaultMutate(t => t.Value += 2)
                    .Build())
            .When("Create B with per-call +5", r => r.Create(Kind.B, t => t.Value += 5))
            .Then("1+2 then +5 = 8", t => t.Value == 8)
            .AssertPassed();

    [Scenario("Registry: per-call mutate null is ignored on mapped and default paths")]
    [Fact]
    public Task Registry_Null_PerCall_Ignored()
        => Given("mapped A and a default", () =>
                Prototype<Kind, Thing>.Create()
                    .Map(Kind.A, new Thing("A", 1), Clone)
                    .Default(new Thing("D", 2), Clone)
                    .Build())
            .When("Create A(null) and B(null)", r => (A: r.Create(Kind.A, null!), B: r.Create(Kind.B, null!)))
            .Then("A remains 1", t => t.A.Value == 1)
            .And("default remains 2", t => t.B.Value == 2)
            .AssertPassed();

    [Scenario("Registry: DefaultMutate before Default -> Create missing throws (no default)")]
    [Fact]
    public Task Registry_DefaultMutate_Without_Default_Builds_NoDefault()
        => Given("builder with only DefaultMutate", () =>
                Prototype<Kind, Thing>.Create().DefaultMutate(t => t.Value++).Build())
            .When("Create B", r => Record.Exception(() => r.Create(Kind.B)))
            .Then("InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Registry: Mutate before Map then Map clears half-configured family (no throw, no mutation)")]
    [Fact]
    public Task Registry_Mutate_Before_Map_Then_Map_Clears()
        => Given("builder Mutate(A) then Map(A)", () =>
            {
                var b = Prototype<Kind, Thing>.Create()
                    .Mutate(Kind.A, t => t.Value += 100) // creates half-configured fam
                    .Map(Kind.A, new Thing("A", 1), Clone); // replaces with proper fam (no mutations)
                return b.Build();
            })
            .When("Create A (should not have +100)", r => r.Create(Kind.A))
            .Then("Value == 1", t => t.Value == 1)
            .AssertPassed();

    [Scenario("Registry: Map then Mutate then Map resets mutations (last Map wins)")]
    [Fact]
    public Task Registry_Map_Then_Mutate_Then_Map_Resets()
        => Given("Map(A) with mutations added then replaced by Map(A)", () =>
            {
                var b = Prototype<Kind, Thing>.Create()
                    .Map(Kind.A, new Thing("A", 1), Clone)
                    .Mutate(Kind.A, t => t.Value += 10)
                    .Map(Kind.A, new Thing("A2", 2), Clone); // resets mutations
                return b.Default(new Thing("D", 0), Clone).Build();
            })
            .When("Create A", r => r.Create(Kind.A))
            .Then("uses last Map with no mutation", t => t is { Name: "A2", Value: 2 })
            .AssertPassed();

    [Scenario("Registry: multiple Mutate for same key preserve order")] 
    [Fact]
    public Task Registry_Multiple_Mutate_Order()
        => Given("Map(A) then Mutate twice (+1 then *3)", () =>
                Prototype<Kind, Thing>.Create()
                    .Map(Kind.A, new Thing("A", 2), Clone)
                    .Mutate(Kind.A, t => t.Value += 1)
                    .Mutate(Kind.A, t => t.Value *= 3)
                    .Build())
            .When("Create A", r => r.Create(Kind.A))
            .Then("(2+1)*3 = 9", t => t.Value == 9)
            .AssertPassed();

    [Scenario("String keys comparer: case-insensitive mapping still works for Prototype registry")]
    [Fact]
    public Task Registry_Comparer_StringIgnoreCase()
        => Given("string-key registry with OrdinalIgnoreCase", () =>
                Prototype<string, Thing>.Create(StringComparer.OrdinalIgnoreCase)
                    .Map("json", new Thing("js", 1), Clone)
                    .Default(new Thing("def", 0), Clone)
                    .Build())
            .When("Create('JSON')", r => r.Create("JSON"))
            .Then("matches 'json' mapping", t => t is { Name: "js", Value: 1 })
            .AssertPassed();

    [Scenario("Mutate before Map for a key throws on Build")]
    [Fact]
    public Task Registry_Mutate_Before_Map_Throws()
        => Given("a builder where Mutate is called before Map", () =>
                Prototype<string, Thing>.Create()
                    .Mutate("x", t => t.Value++) )
            .When("building (should throw)", b => Record.Exception(() => b.Build()))
            .Then("InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Null key throws ArgumentNullException (string keys)")]
    [Fact]
    public Task Registry_Null_Key_Throws()
        => Given("a registry with default", () =>
                Prototype<string, Thing>.Create()
                    .Default(new Thing("def", 0), Clone)
                    .Build())
            .When("Create(null)", r => Record.Exception(() => r.Create(null!)))
            .Then("ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();
}
