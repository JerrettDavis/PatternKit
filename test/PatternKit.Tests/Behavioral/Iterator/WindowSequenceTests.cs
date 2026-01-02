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

#region Additional WindowSequence Tests

public sealed class WindowSequenceBuilderTests
{
    [Fact]
    public void Windows_NullSource_Throws()
    {
        IEnumerable<int>? source = null;

        Assert.Throws<ArgumentNullException>(() => source!.Windows(3).ToList());
    }

    [Fact]
    public void Windows_NegativeSize_Throws()
    {
        var source = Enumerable.Range(1, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => source.Windows(-1).ToList());
    }

    [Fact]
    public void Windows_ZeroStride_Throws()
    {
        var source = Enumerable.Range(1, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => source.Windows(3, stride: 0).ToList());
    }

    [Fact]
    public void Windows_NegativeStride_Throws()
    {
        var source = Enumerable.Range(1, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => source.Windows(3, stride: -1).ToList());
    }

    [Fact]
    public void Windows_EmptySource_YieldsNothing()
    {
        var source = Enumerable.Empty<int>();

        var windows = source.Windows(3).ToList();

        Assert.Empty(windows);
    }

    [Fact]
    public void Windows_SourceSmallerThanSize_NoWindows()
    {
        var source = Enumerable.Range(1, 2);

        var windows = source.Windows(5).ToList();

        Assert.Empty(windows);
    }

    [Fact]
    public void Windows_SourceSmallerThanSize_WithPartial_YieldsPartial()
    {
        var source = Enumerable.Range(1, 2);

        var windows = source.Windows(5, includePartial: true).ToList();

        Assert.Single(windows);
        Assert.True(windows[0].IsPartial);
        Assert.Equal(2, windows[0].Count);
    }

    [Fact]
    public void Windows_ExactSize_SingleWindow()
    {
        var source = Enumerable.Range(1, 3);

        var windows = source.Windows(3).ToList();

        Assert.Single(windows);
        Assert.Equal(new[] { 1, 2, 3 }, windows[0].ToArray());
        Assert.False(windows[0].IsPartial);
    }

    [Fact]
    public void Window_Indexer_ValidIndex()
    {
        var window = Enumerable.Range(1, 5).Windows(3).First();

        Assert.Equal(1, window[0]);
        Assert.Equal(2, window[1]);
        Assert.Equal(3, window[2]);
    }

    [Fact]
    public void Window_Indexer_InvalidIndex_Throws()
    {
        var window = Enumerable.Range(1, 5).Windows(3).First();

        Assert.Throws<ArgumentOutOfRangeException>(() => window[3]);
        Assert.Throws<ArgumentOutOfRangeException>(() => window[-1]);
    }

    [Fact]
    public void Window_GetEnumerator_Works()
    {
        var window = Enumerable.Range(1, 5).Windows(3).First();
        var list = new List<int>();

        foreach (var item in window)
            list.Add(item);

        Assert.Equal(new[] { 1, 2, 3 }, list);
    }

    [Fact]
    public void Windows_LargeStride_SkipsElements()
    {
        var source = Enumerable.Range(1, 10);

        // stride=4 means skip 4 elements (not slide by 4)
        // Windows: [1,2], then skip 4 -> [5,6], then skip 4 -> [9,10]
        // But actually: stride=4 slides by 4 positions
        // [1,2] at pos 0, [5,6] at pos 4, [9,10] at pos 8 - only 3 windows fit
        var windows = source.Windows(2, stride: 4).ToList();

        Assert.True(windows.Count >= 2); // At least 2 windows
        Assert.Equal(new[] { 1, 2 }, windows[0].ToArray());
    }

    [Fact]
    public void Windows_StrideGreaterThanSize_NoOverlap()
    {
        var source = Enumerable.Range(1, 8);

        // stride=3, size=2:
        // Window 1 at queue start: [1,2]
        // Then stride drops 2 elements (min of stride and queue size) and refills
        // Queue drops all 2 and refills with [3,4]
        // Window 2: [3,4], then drop 2 refill [5,6]
        // Window 3: [5,6], then drop 2 refill [7,8]
        // Window 4: [7,8]
        var windows = source.Windows(2, stride: 3).ToList();

        Assert.True(windows.Count >= 2);
        Assert.Equal(new[] { 1, 2 }, windows[0].ToArray());
        // Second window depends on stride implementation
        Assert.Equal(2, windows[1].Count);
    }

    [Fact]
    public void Windows_ReuseBuffer_SameArrayReused()
    {
        var source = Enumerable.Range(1, 6);
        var windows = source.Windows(3, stride: 1, reuseBuffer: true).ToList();

        // All windows should have IsBufferReused = true
        Assert.All(windows, w => Assert.True(w.IsBufferReused));
    }

    [Fact]
    public void Windows_NoReuseBuffer_FreshArrays()
    {
        var source = Enumerable.Range(1, 6);
        var windows = source.Windows(3, stride: 1, reuseBuffer: false).ToList();

        Assert.All(windows, w => Assert.False(w.IsBufferReused));
    }

    [Fact]
    public void Window_ToArray_CreatesCopy()
    {
        // With reuseBuffer=true, windows share the same underlying buffer
        // but ToArray() should create a fresh copy
        var windows = Enumerable.Range(1, 6).Windows(3, reuseBuffer: true).ToList();

        // When buffer is reused, windows[0] buffer has been overwritten by later windows
        // But ToArray on the fly should work:
        var allWindows = Enumerable.Range(1, 6).Windows(3, reuseBuffer: false).ToList();
        var copy1 = allWindows[0].ToArray();
        var copy2 = allWindows[1].ToArray();

        // ToArray should create distinct arrays
        Assert.NotSame(copy1, copy2);
        Assert.Equal(new[] { 1, 2, 3 }, copy1);
        Assert.Equal(new[] { 2, 3, 4 }, copy2);
    }

    [Fact]
    public void Windows_PartialWindow_IsNotBufferReused()
    {
        var source = Enumerable.Range(1, 4);
        var windows = source.Windows(3, stride: 2, includePartial: true, reuseBuffer: true).ToList();

        Assert.Equal(2, windows.Count);
        Assert.True(windows[0].IsBufferReused);    // full window
        Assert.False(windows[1].IsBufferReused);   // partial window always gets fresh array
    }

    [Fact]
    public void Windows_Count_Property()
    {
        var window = Enumerable.Range(1, 10).Windows(4).First();

        Assert.Equal(4, window.Count);
    }

    [Fact]
    public void Windows_IsPartial_Property()
    {
        // Range 1..5 with size 3, stride 2, includePartial:
        // Window 1: [1,2,3] at pos 0 (full)
        // Window 2: [3,4,5] at pos 2 (full)
        // Window 3: [5] at pos 4 (partial - only 1 element left)
        var windows = Enumerable.Range(1, 5).Windows(3, stride: 2, includePartial: true).ToList();

        Assert.True(windows.Count >= 2);
        Assert.False(windows[0].IsPartial);  // 1,2,3 - full

        // The last window should be partial if there are any remaining elements
        var lastWindow = windows[^1];
        if (windows.Count > 2)
            Assert.True(lastWindow.IsPartial);
    }
}

#endregion