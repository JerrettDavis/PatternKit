using PatternKit.Creational.Factory;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Creational.Factory;

[Feature("Creational - Factory<TKey,TOut> & Factory<TKey,TIn,TOut>")]
public sealed class FactoryTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // -------------------- Helpers --------------------
    private static Factory<string, string>.Builder NewOutOnly(StringComparer? cmp = null)
        => Factory<string, string>.Create(cmp);

    private static Factory<string, int, int>.Builder NewWithInput(StringComparer? cmp = null)
        => Factory<string, int, int>.Create(cmp);

    // -------------------- Scenarios --------------------

    [Scenario("Mapping and default are used as expected (out-only)")]
    [Fact]
    public Task Mapping_And_Default_OutOnly()
        => Given("a builder with 'json' and default 'other'", () =>
            NewOutOnly()
                .Map("json", static () => "application/json")
                .Default(static () => "other")
                .Build())
            .When("creating for known and unknown keys", f => (Known: f.Create("json"), Fallback: f.Create("xml")))
            .Then("known uses mapping", r => r.Known == "application/json")
            .And("unknown uses default", r => r.Fallback == "other")
            .AssertPassed();

    [Scenario("TryCreate returns false only when missing and no default (out-only)")]
    [Fact]
    public Task TryCreate_NoDefault_ReturnsFalse()
        => Given("a factory without default and one mapping", () =>
                NewOutOnly().Map("a", static () => "A").Build())
            .When("TryCreate on missing key", f => f.TryCreate("b", out var _))
            .Then("should be false", ok => !ok)
            .AssertPassed();

    [Scenario("Create throws when missing and no default (out-only)")]
    [Fact]
    public Task Create_NoDefault_Throws_OutOnly()
        => Given("a factory without default and one mapping", () =>
                NewOutOnly().Map("a", static () => "A").Build())
            .When("calling Create on missing key", f => Record.Exception(() => f.Create("b")))
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Last mapping wins when the same key is mapped twice")]
    [Fact]
    public Task Last_Mapping_Wins()
        => Given("a builder mapping 'a' twice", () =>
                NewOutOnly()
                    .Map("a", static () => "first")
                    .Map("a", static () => "second")
                    .Default(static () => "-")
                    .Build())
            .When("creating 'a'", f => f.Create("a"))
            .Then("returns the second mapping", s => s == "second")
            .AssertPassed();

    [Scenario("Case-insensitive comparer maps 'json' and resolves 'JSON'")]
    [Fact]
    public Task Comparer_OrdinalIgnoreCase()
        => Given("a factory with OrdinalIgnoreCase comparer", () =>
                NewOutOnly(StringComparer.OrdinalIgnoreCase)
                    .Map("json", static () => "application/json")
                    .Default(static () => "other")
                    .Build())
            .When("creating with different casing", f => f.Create("JSON"))
            .Then("mapping is found", v => v == "application/json")
            .AssertPassed();

    [Scenario("Build snapshots are immutable w.r.t. later builder changes")]
    [Fact]
    public Task Build_Snapshot_Immutability()
        => Given("build F1 then change mapping and build F2", () =>
            {
                var b = NewOutOnly()
                    .Map("a", static () => "1");
                var f1 = b.Default(static () => "-").Build();
                b.Map("a", static () => "2");
                var f2 = b.Build();
                return (f1, f2);
            })
            .When("creating via both", t => (v1: t.f1.Create("a"), v2: t.f2.Create("a")))
            .Then("F1 uses old mapping", t => t.v1 == "1")
            .And("F2 uses new mapping", t => t.v2 == "2")
            .AssertPassed();

    [Scenario("Parameterized creators: mapping uses input, default echoes input")]
    [Fact]
    public Task With_Input_Mapping_And_Default()
        => Given("a factory where 'double' maps to x*2; default returns x", () =>
                NewWithInput()
                    .Map("double", static (in int x) => x * 2)
                    .Default(static (in int x) => x)
                    .Build())
            .When("creating for 'double' and 'other'", f => (d: f.Create("double", 5), o: f.Create("other", 7)))
            .Then("double maps to 10", r => r.d == 10)
            .And("default echoes 7", r => r.o == 7)
            .AssertPassed();

    [Scenario("TryCreate with input: false when missing and no default")]
    [Fact]
    public Task With_Input_TryCreate_NoDefault()
        => Given("a factory without default and only 'triple'", () =>
                NewWithInput().Map("triple", static (in int x) => x * 3).Build())
            .When("TryCreate on missing key", f => f.TryCreate("quad", 4, out var _))
            .Then("returns false", ok => ok == false)
            .AssertPassed();

    [Scenario("Create with input throws when missing and no default")]
    [Fact]
    public Task With_Input_Create_NoDefault_Throws()
        => Given("a factory without default and only 'double'", () =>
                NewWithInput().Map("double", static (in int x) => x * 2).Build())
            .When("calling Create on missing key", f => Record.Exception(() => f.Create("noop", 1)))
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Null key throws ArgumentNullException (out-only)")]
    [Fact]
    public Task Null_Key_Throws_ArgumentNull_OutOnly()
        => Given("a factory with a default", () =>
                NewOutOnly().Default(static () => "-").Build())
            .When("calling Create(null)", f => Record.Exception(() => f.Create(null!)))
            .Then("throws ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();

    [Scenario("Null key throws ArgumentNullException (with input)")]
    [Fact]
    public Task Null_Key_Throws_ArgumentNull_WithInput()
        => Given("a factory with a default", () =>
                NewWithInput().Default(static (in int x) => x).Build())
            .When("calling Create(null, 1)", f => Record.Exception(() => f.Create(null!, 1)))
            .Then("throws ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();
}
