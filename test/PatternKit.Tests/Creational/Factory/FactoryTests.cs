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
                    .Map("double", static (in x) => x * 2)
                    .Default(static (in x) => x)
                    .Build())
            .When("creating for 'double' and 'other'", f => (d: f.Create("double", 5), o: f.Create("other", 7)))
            .Then("double maps to 10", r => r.d == 10)
            .And("default echoes 7", r => r.o == 7)
            .AssertPassed();

    [Scenario("TryCreate with input: false when missing and no default")]
    [Fact]
    public Task With_Input_TryCreate_NoDefault()
        => Given("a factory without default and only 'triple'", () =>
                NewWithInput().Map("triple", static (in x) => x * 3).Build())
            .When("TryCreate on missing key", f => f.TryCreate("quad", 4, out var _))
            .Then("returns false", ok => ok == false)
            .AssertPassed();

    [Scenario("Create with input throws when missing and no default")]
    [Fact]
    public Task With_Input_Create_NoDefault_Throws()
        => Given("a factory without default and only 'double'", () =>
                NewWithInput().Map("double", static (in x) => x * 2).Build())
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
                NewWithInput().Default(static (in x) => x).Build())
            .When("calling Create(null, 1)", f => Record.Exception(() => f.Create(null!, 1)))
            .Then("throws ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();
}

#region Additional Factory Tests

public sealed class FactoryBuilderTests
{
    [Fact]
    public void TryCreate_ValidKey_ReturnsTrue()
    {
        var factory = Factory<string, int>.Create()
            .Map("one", () => 1)
            .Map("two", () => 2)
            .Build();

        var success = factory.TryCreate("one", out var value);

        Assert.True(success);
        Assert.Equal(1, value);
    }

    [Fact]
    public void TryCreate_MissingKey_WithDefault_ReturnsTrue()
    {
        var factory = Factory<string, int>.Create()
            .Map("one", () => 1)
            .Default(() => -1)
            .Build();

        var success = factory.TryCreate("unknown", out var value);

        Assert.True(success);
        Assert.Equal(-1, value);
    }

    [Fact]
    public void TryCreate_MissingKey_NoDefault_ReturnsFalse()
    {
        var factory = Factory<string, int>.Create()
            .Map("one", () => 1)
            .Build();

        var success = factory.TryCreate("unknown", out var value);

        Assert.False(success);
        Assert.Equal(default, value);
    }

    [Fact]
    public void Create_MultipleKeys_AllWork()
    {
        var factory = Factory<int, string>.Create()
            .Map(1, () => "one")
            .Map(2, () => "two")
            .Map(3, () => "three")
            .Build();

        Assert.Equal("one", factory.Create(1));
        Assert.Equal("two", factory.Create(2));
        Assert.Equal("three", factory.Create(3));
    }

    [Fact]
    public void Create_EnumKey_Works()
    {
        var factory = Factory<DayOfWeek, string>.Create()
            .Map(DayOfWeek.Monday, () => "Start of week")
            .Map(DayOfWeek.Friday, () => "End of week")
            .Default(() => "Mid week")
            .Build();

        Assert.Equal("Start of week", factory.Create(DayOfWeek.Monday));
        Assert.Equal("End of week", factory.Create(DayOfWeek.Friday));
        Assert.Equal("Mid week", factory.Create(DayOfWeek.Wednesday));
    }

    [Fact]
    public void Default_CalledMultipleTimes_LastWins()
    {
        var factory = Factory<string, string>.Create()
            .Default(() => "first")
            .Default(() => "second")
            .Default(() => "third")
            .Build();

        Assert.Equal("third", factory.Create("unknown"));
    }

    [Fact]
    public void Factory_WithInput_ValidKey_UsesMapping()
    {
        var factory = Factory<string, int, string>.Create()
            .Map("repeat", (in int n) => new string('x', n))
            .Build();

        var result = factory.Create("repeat", 5);

        Assert.Equal("xxxxx", result);
    }

    [Fact]
    public void Factory_WithInput_TryCreate_ValidKey()
    {
        var factory = Factory<string, int, int>.Create()
            .Map("double", (in int x) => x * 2)
            .Map("triple", (in int x) => x * 3)
            .Build();

        var success = factory.TryCreate("triple", 10, out var value);

        Assert.True(success);
        Assert.Equal(30, value);
    }

    [Fact]
    public void Factory_WithInput_TryCreate_MissingKey_WithDefault()
    {
        var factory = Factory<string, int, int>.Create()
            .Map("double", (in int x) => x * 2)
            .Default((in int x) => x)
            .Build();

        var success = factory.TryCreate("noop", 42, out var value);

        Assert.True(success);
        Assert.Equal(42, value);
    }

    [Fact]
    public void Factory_WithInput_Comparer_Works()
    {
        var factory = Factory<string, int, int>.Create(StringComparer.OrdinalIgnoreCase)
            .Map("multiply", (in int x) => x * 10)
            .Build();

        var result = factory.Create("MULTIPLY", 5);

        Assert.Equal(50, result);
    }

    [Fact]
    public void Factory_WithInput_MultipleBuilds_Independent()
    {
        var builder = Factory<string, int, int>.Create()
            .Map("add", (in int x) => x + 1);

        var f1 = builder.Build();
        builder.Map("add", (in int x) => x + 10);
        var f2 = builder.Build();

        Assert.Equal(6, f1.Create("add", 5));
        Assert.Equal(15, f2.Create("add", 5));
    }

    [Fact]
    public void Factory_CreatorReturnsNull_Works()
    {
        var factory = Factory<string, string?>.Create()
            .Map("null", () => null)
            .Build();

        var result = factory.Create("null");

        Assert.Null(result);
    }

    [Fact]
    public void Factory_WithComplexOutput_Works()
    {
        var factory = Factory<string, TestProduct>.Create()
            .Map("widget", () => new TestProduct("Widget", 10.0m))
            .Map("gadget", () => new TestProduct("Gadget", 25.0m))
            .Build();

        var widget = factory.Create("widget");
        var gadget = factory.Create("gadget");

        Assert.Equal("Widget", widget.Name);
        Assert.Equal(10.0m, widget.Price);
        Assert.Equal("Gadget", gadget.Name);
        Assert.Equal(25.0m, gadget.Price);
    }

    private record TestProduct(string Name, decimal Price);

    [Fact]
    public void Factory_EmptyFactory_Create_Throws()
    {
        var factory = Factory<string, string>.Create().Build();

        Assert.Throws<InvalidOperationException>(() => factory.Create("any"));
    }

    [Fact]
    public void Factory_EmptyFactory_TryCreate_ReturnsFalse()
    {
        var factory = Factory<string, string>.Create().Build();

        var success = factory.TryCreate("any", out var value);

        Assert.False(success);
        Assert.Null(value);
    }

    [Fact]
    public void Factory_WithInput_EmptyFactory_Create_Throws()
    {
        var factory = Factory<string, int, int>.Create().Build();

        Assert.Throws<InvalidOperationException>(() => factory.Create("any", 1));
    }

    [Fact]
    public void Factory_WithInput_EmptyFactory_TryCreate_ReturnsFalse()
    {
        var factory = Factory<string, int, int>.Create().Build();

        var success = factory.TryCreate("any", 1, out var value);

        Assert.False(success);
        Assert.Equal(default, value);
    }

    [Fact]
    public void Factory_IntKey_Works()
    {
        var factory = Factory<int, string>.Create()
            .Map(1, () => "one")
            .Map(2, () => "two")
            .Default(() => "unknown")
            .Build();

        Assert.Equal("one", factory.Create(1));
        Assert.Equal("two", factory.Create(2));
        Assert.Equal("unknown", factory.Create(999));
    }

    [Fact]
    public void Factory_NullComparer_UsesDefault()
    {
        var factory = Factory<string, string>.Create(null)
            .Map("test", () => "value")
            .Build();

        Assert.Equal("value", factory.Create("test"));
    }

    [Fact]
    public void Factory_WithInput_NullComparer_UsesDefault()
    {
        var factory = Factory<string, int, int>.Create(null)
            .Map("double", (in int x) => x * 2)
            .Build();

        Assert.Equal(20, factory.Create("double", 10));
    }
}

#endregion
