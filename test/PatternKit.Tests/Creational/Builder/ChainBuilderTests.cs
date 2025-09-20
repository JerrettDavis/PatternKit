using PatternKit.Creational.Builder;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Creational.Builder;

[Feature("ChainBuilder<T>")]
public sealed class ChainBuilderTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record Ctx(ChainBuilder<int> B, object? Result = null);

    private static Ctx Build_Empty() => new(ChainBuilder<int>.Create());

    private static Ctx Build_With_TwoItems() => new(
        ChainBuilder<int>.Create()
            .Add(1)
            .Add(2)
    );

    private static Ctx Build_With_ThreeItems() => new(
        ChainBuilder<int>.Create()
            .Add(1)
            .Add(2)
            .Add(3)
    );

    private static Ctx Build_With_AddIf() => new(
        ChainBuilder<int>.Create()
            .AddIf(true, 42)
            .AddIf(false, 99)
    );

    [Scenario("Create returns an empty builder")]
    [Fact]
    public async Task Create_ShouldReturnEmptyBuilder()
    {
        await Given("an empty builder", Build_Empty)
            .When("building to get length", c => c with { Result = c.B.Build(arr => arr.Length) })
            .Then("result length is 0", c => (int)c.Result! == 0)
            .AssertPassed();
    }

    [Scenario("Add registers items in order")]
    [Fact]
    public async Task Add_ShouldAddItems()
    {
        await Given("a builder with two items", Build_With_TwoItems)
            .When("projecting to CSV", c => c with { Result = c.B.Build(arr => string.Join(",", arr)) })
            .Then("CSV matches \"1,2\"", c => (string)c.Result! == "1,2")
            .AssertPassed();
    }

    [Scenario("AddIf only adds when condition is true")]
    [Fact]
    public async Task AddIf_ShouldAddItem_WhenConditionIsTrue()
    {
        await Given("a builder using AddIf(true) and AddIf(false)", Build_With_AddIf)
            .When("building to array", c => c with { Result = c.B.Build(arr => arr) })
            .Then("only the true branch item exists", c =>
            {
                var arr = (int[])c.Result!;
                return arr is [42];
            })
            .AssertPassed();
    }

    [Scenario("Build can project items (aggregation)")]
    [Fact]
    public async Task Build_ShouldProjectItems()
    {
        await Given("a builder with 1,2,3", Build_With_ThreeItems)
            .When("building to sum", c => c with { Result = c.B.Build(arr => arr.Sum()) })
            .Then("sum equals 6", c => (int)c.Result! == 6)
            .AssertPassed();
    }
}
