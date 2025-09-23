using PatternKit.Behavioral.Iterator;
using TinyBDD;
using TinyBDD.Assertions;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Iterator;

[Feature("WindowSequence (sliding/striding windows)")]
public sealed class WindowSequenceTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Sliding size=3 stride=1 over 1..5 yields 3 overlapping windows")]
    [Fact]
    public Task BasicSliding()
        => Given("range 1..5", () => Enumerable.Range(1, 5))
            .When("windowed size3 stride1", src => src.Windows(size: 3, stride: 1).Select(w => string.Join('-', w.ToArray())).ToList())
            .Then("windows are 1-2-3 | 2-3-4 | 3-4-5", list => Expect.For(string.Join(" | ", list)).ToBe("1-2-3 | 2-3-4 | 3-4-5"))
            .AssertPassed();

    [Scenario("Stride > 1 drops elements between windows")]
    [Fact]
    public Task StrideTwo()
        => Given("range 1..6", () => Enumerable.Range(1, 6))
            .When("size3 stride2", src => src.Windows(size: 3, stride: 2).Select(w => w.ToArray()).ToList())
            .Then("two windows produced", ws => Expect.For(ws.Count).ToBe(2))
            .And("first is 1,2,3", ws => ws[0][0] == 1 && ws[0][2] == 3)
            .And("second is 3,4,5", ws => ws[1][0] == 3 && ws[1][2] == 5)
            .AssertPassed();

    [Scenario("Partial enabled yields trailing partial window")]
    [Fact]
    public Task PartialTrailing()
        => Given("range 1..4", () => Enumerable.Range(1, 4))
            .When("size3 stride2 include partial",
                src => src.Windows(size: 3, stride: 2, includePartial: true).Select(w => (Vals: w.ToArray(), w.IsPartial)).ToList())
            .Then("two windows", list => list.Count == 2)
            .And("first full, second partial", list => !list[0].IsPartial && list[1].IsPartial)
            .And("partial contains 3,4", list => string.Join('-', list[1].Vals) == "3-4")
            .AssertPassed();

    [Scenario("ReuseBuffer reuses underlying array for full windows")]
    [Fact]
    public Task ReuseBufferBehavior()
        => Given("range 1..5", () => Enumerable.Range(1, 5))
            .When("size3 stride1 reuse buffer", src => src.Windows(3, 1, reuseBuffer: true).Take(3).ToList())
            .Then("all report reused buffer", ws => ws.All(w => w.IsBufferReused))
            .And("ToArray snapshots are distinct", ws =>
            {
                var snaps = ws.Select(w => w.ToArray()).ToList();
                return snaps[0] != snaps[1] && snaps[1] != snaps[2];
            })
            .AssertPassed();

    [Scenario("Argument validation: size<=0 throws")]
    [Fact]
    public Task SizeValidation()
        => Given("invalid size 0", () => 0)
            .When("invoking Windows", i =>
            {
                try
                {
                    _ = Enumerable.Range(1, 3).Windows(i).First();
                    return false;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return true;
                }
            })
            .Then("throws", ok => ok)
            .AssertPassed();
}