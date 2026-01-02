using PatternKit.Behavioral.TypeDispatcher;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - AsyncTypeDispatcher<TBase,TResult> and AsyncActionTypeDispatcher<TBase>")]
public sealed class AsyncTypeDispatcherTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private abstract record Node;
    private sealed record Number(int Value) : Node;
    private sealed record Add(Node Left, Node Right) : Node;
    private sealed record Neg(Node Inner) : Node;

    [Scenario("Async result dispatcher dispatches by runtime type")]
    [Fact]
    public async Task AsyncTypeDispatcher_Dispatch_And_Default()
    {
        // Arrange
        var d = AsyncTypeDispatcher<Node, string>
            .Create()
            .On<Add>(_ => "+")
            .On<Number>(n => $"#{n.Value}")
            .Default(_ => "?")
            .Build();

        // Act
        var a = await d.DispatchAsync(new Add(new Number(1), new Number(2)));
        var b = await d.DispatchAsync(new Number(7));
        var c = await d.DispatchAsync(new Neg(new Number(1))); // no match, hits default

        // Assert
        Assert.Equal("+", a);
        Assert.Equal("#7", b);
        Assert.Equal("?", c);
    }

    [Scenario("TryDispatchAsync returns false when no handler and no default")]
    [Fact]
    public async Task AsyncTypeDispatcher_TryDispatch_NoDefault()
    {
        // Arrange
        var d = AsyncTypeDispatcher<Node, string>.Create()
            .On<Number>(n => n.Value.ToString())
            .Build();

        // Act
        var (ok, result) = await d.TryDispatchAsync(new Neg(new Number(3)));

        // Assert
        Assert.False(ok);
        Assert.Null(result);
    }

    [Scenario("Async action dispatcher executes side effects by runtime type")]
    [Fact]
    public async Task AsyncActionTypeDispatcher_Dispatch()
    {
        // Arrange
        var counters = new int[3]; // [0]=Add, [1]=Number, [2]=Default
        var d = AsyncActionTypeDispatcher<Node>
            .Create()
            .On<Add>(_ => Interlocked.Increment(ref counters[0]))
            .On<Number>(_ => Interlocked.Increment(ref counters[1]))
            .Default(_ => Interlocked.Increment(ref counters[2]))
            .Build();

        // Act
        await d.DispatchAsync(new Add(new Number(1), new Number(2)));
        await d.DispatchAsync(new Number(5));
        await d.DispatchAsync(new Neg(new Number(9))); // default

        // Assert
        Assert.Equal(1, counters[0]);
        Assert.Equal(1, counters[1]);
        Assert.Equal(1, counters[2]);
    }

    [Scenario("Async handlers with cancellation token")]
    [Fact]
    public async Task AsyncTypeDispatcher_WithCancellationToken()
    {
        // Arrange
        var d = AsyncTypeDispatcher<Node, string>
            .Create()
            .On<Number>(async (n, ct) =>
            {
                await Task.Delay(1, ct);
                return $"#{n.Value}";
            })
            .Build();

        // Act
        using var cts = new CancellationTokenSource();
        var result = await d.DispatchAsync(new Number(42), cts.Token);

        // Assert
        Assert.Equal("#42", result);
    }

    [Scenario("TryDispatchAsync action returns false when no handler and no default")]
    [Fact]
    public async Task AsyncActionTypeDispatcher_TryDispatch_NoDefault()
    {
        // Arrange
        var counter = 0;
        var d = AsyncActionTypeDispatcher<Node>
            .Create()
            .On<Number>(_ => counter++)
            .Build();

        // Act
        var ok = await d.TryDispatchAsync(new Neg(new Number(3)));

        // Assert
        Assert.False(ok);
        Assert.Equal(0, counter);
    }

    [Scenario("DispatchAsync throws when no handler and no default")]
    [Fact]
    public async Task AsyncTypeDispatcher_DispatchAsync_Throws_NoDefault()
    {
        // Arrange
        var d = AsyncTypeDispatcher<Node, string>.Create()
            .On<Number>(n => n.Value.ToString())
            .Build();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await d.DispatchAsync(new Neg(new Number(3))));
    }
}
