using PatternKit.Behavioral.TypeDispatcher;

namespace PatternKit.Tests.Behavioral.TypeDispatcher;

public sealed class ActionTypeDispatcherTests
{
    private abstract class Shape { }
    private sealed class Circle : Shape { public double Radius { get; init; } }
    private sealed class Rectangle : Shape { public double Width { get; init; } public double Height { get; init; } }
    private sealed class Triangle : Shape { }

    #region ActionTypeDispatcher<TBase> Tests

    [Fact]
    public void ActionTypeDispatcher_Dispatches_To_Correct_Handler()
    {
        var log = new List<string>();
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => log.Add($"circle-{c.Radius}"))
            .On<Rectangle>(r => log.Add($"rect-{r.Width}x{r.Height}"))
            .Build();

        dispatcher.Dispatch(new Circle { Radius = 5 });
        dispatcher.Dispatch(new Rectangle { Width = 3, Height = 4 });

        Assert.Equal("circle-5", log[0]);
        Assert.Equal("rect-3x4", log[1]);
    }

    [Fact]
    public void ActionTypeDispatcher_Default_Handler()
    {
        var log = new List<string>();
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => log.Add("circle"))
            .Default(s => log.Add($"default-{s.GetType().Name}"))
            .Build();

        dispatcher.Dispatch(new Triangle());

        Assert.Single(log);
        Assert.Equal("default-Triangle", log[0]);
    }

    [Fact]
    public void ActionTypeDispatcher_TryDispatch_Returns_True()
    {
        var log = new List<string>();
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => log.Add("circle"))
            .Build();

        var result = dispatcher.TryDispatch(new Circle { Radius = 5 });

        Assert.True(result);
        Assert.Single(log);
    }

    [Fact]
    public void ActionTypeDispatcher_TryDispatch_Returns_False_When_No_Handler()
    {
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => { })
            .Build();

        var result = dispatcher.TryDispatch(new Triangle());

        Assert.False(result);
    }

    [Fact]
    public void ActionTypeDispatcher_Throws_When_No_Handler()
    {
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => { })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            dispatcher.Dispatch(new Triangle()));
    }

    [Fact]
    public void ActionTypeDispatcher_Registration_Order_Matters()
    {
        var log = new List<string>();
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => log.Add("circle"))
            .On<Shape>(s => log.Add("shape")) // More general - should only match if Circle didn't
            .Build();

        dispatcher.Dispatch(new Circle { Radius = 5 });

        Assert.Single(log);
        Assert.Equal("circle", log[0]);
    }

    #endregion

    #region AsyncActionTypeDispatcher<TBase> Tests

    [Fact]
    public async Task AsyncActionTypeDispatcher_Dispatches()
    {
        var log = new List<string>();
        var dispatcher = AsyncActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => log.Add($"circle-{c.Radius}"))
            .On<Rectangle>(r => log.Add($"rect-{r.Width}x{r.Height}"))
            .Build();

        await dispatcher.DispatchAsync(new Circle { Radius = 5 });
        await dispatcher.DispatchAsync(new Rectangle { Width = 3, Height = 4 });

        Assert.Equal("circle-5", log[0]);
        Assert.Equal("rect-3x4", log[1]);
    }

    [Fact]
    public async Task AsyncActionTypeDispatcher_Async_Handler()
    {
        var log = new List<string>();
        var dispatcher = AsyncActionTypeDispatcher<Shape>.Create()
            .On<Circle>(async (c, ct) =>
            {
                await Task.Delay(1, ct);
                log.Add($"circle-{c.Radius}");
            })
            .Build();

        await dispatcher.DispatchAsync(new Circle { Radius = 5 });

        Assert.Single(log);
        Assert.Equal("circle-5", log[0]);
    }

    [Fact]
    public async Task AsyncActionTypeDispatcher_Default_Handler()
    {
        var log = new List<string>();
        var dispatcher = AsyncActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => log.Add("circle"))
            .Default(s => log.Add($"default-{s.GetType().Name}"))
            .Build();

        await dispatcher.DispatchAsync(new Triangle());

        Assert.Single(log);
        Assert.Equal("default-Triangle", log[0]);
    }

    [Fact]
    public async Task AsyncActionTypeDispatcher_TryDispatch_Returns_True()
    {
        var log = new List<string>();
        var dispatcher = AsyncActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => log.Add("circle"))
            .Build();

        var result = await dispatcher.TryDispatchAsync(new Circle { Radius = 5 });

        Assert.True(result);
        Assert.Single(log);
    }

    [Fact]
    public async Task AsyncActionTypeDispatcher_TryDispatch_Returns_False()
    {
        var dispatcher = AsyncActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => { })
            .Build();

        var result = await dispatcher.TryDispatchAsync(new Triangle());

        Assert.False(result);
    }

    [Fact]
    public async Task AsyncActionTypeDispatcher_Throws_When_No_Handler()
    {
        var dispatcher = AsyncActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => { })
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(new Triangle()).AsTask());
    }

    #endregion

    #region AsyncTypeDispatcher<TBase, TResult> Tests

    [Fact]
    public async Task AsyncTypeDispatcher_Dispatches_With_Result()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, double>.Create()
            .On<Circle>(c => Math.PI * c.Radius * c.Radius)
            .On<Rectangle>(r => r.Width * r.Height)
            .Default(_ => 0)
            .Build();

        var circleArea = await dispatcher.DispatchAsync(new Circle { Radius = 2 });
        var rectArea = await dispatcher.DispatchAsync(new Rectangle { Width = 3, Height = 4 });

        Assert.Equal(Math.PI * 4, circleArea);
        Assert.Equal(12, rectArea);
    }

    [Fact]
    public async Task AsyncTypeDispatcher_Async_Handler()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, double>.Create()
            .On<Circle>(async (c, ct) =>
            {
                await Task.Delay(1, ct);
                return c.Radius * 2;
            })
            .Build();

        var result = await dispatcher.DispatchAsync(new Circle { Radius = 5 });

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AsyncTypeDispatcher_Constant_Handler()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, double>.Create()
            .On<Circle>(42.0)
            .Build();

        var result = await dispatcher.DispatchAsync(new Circle { Radius = 5 });

        Assert.Equal(42.0, result);
    }

    [Fact]
    public async Task AsyncTypeDispatcher_TryDispatch_Returns_Result()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, double>.Create()
            .On<Circle>(c => c.Radius * 2)
            .Build();

        var (success, result) = await dispatcher.TryDispatchAsync(new Circle { Radius = 5 });

        Assert.True(success);
        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AsyncTypeDispatcher_TryDispatch_Returns_False_When_No_Handler()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, double>.Create()
            .On<Circle>(c => c.Radius)
            .Build();

        var (success, result) = await dispatcher.TryDispatchAsync(new Triangle());

        Assert.False(success);
        Assert.Equal(default, result);
    }

    [Fact]
    public async Task AsyncTypeDispatcher_Throws_When_No_Handler()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, double>.Create()
            .On<Circle>(c => c.Radius)
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(new Triangle()).AsTask());
    }

    [Fact]
    public async Task AsyncTypeDispatcher_Default_Handler()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, string>.Create()
            .On<Circle>(c => "circle")
            .Default(s => $"default-{s.GetType().Name}")
            .Build();

        var result = await dispatcher.DispatchAsync(new Triangle());

        Assert.Equal("default-Triangle", result);
    }

    #endregion

    #region Null Behavior Tests

    [Fact]
    public void ActionTypeDispatcher_Null_Handler_Throws_At_Dispatch()
    {
        // Null handlers are stored but cause NullReferenceException when dispatched
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .On<Circle>(null!)
            .Build();

        Assert.Throws<NullReferenceException>(() =>
            dispatcher.Dispatch(new Circle { Radius = 5 }));
    }

    [Fact]
    public void ActionTypeDispatcher_Null_Default_Throws_At_Dispatch()
    {
        // Null default is stored but causes NullReferenceException when dispatched
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .Default((Action<Shape>)null!)
            .Build();

        Assert.Throws<NullReferenceException>(() =>
            dispatcher.Dispatch(new Triangle()));
    }

    [Fact]
    public async Task AsyncActionTypeDispatcher_Null_Handler_Throws_At_Dispatch()
    {
        var dispatcher = AsyncActionTypeDispatcher<Shape>.Create()
            .On<Circle>((Action<Circle>)null!)
            .Build();

        await Assert.ThrowsAsync<NullReferenceException>(() =>
            dispatcher.DispatchAsync(new Circle { Radius = 5 }).AsTask());
    }

    [Fact]
    public async Task AsyncActionTypeDispatcher_Null_Default_Throws_At_Dispatch()
    {
        var dispatcher = AsyncActionTypeDispatcher<Shape>.Create()
            .Default((Action<Shape>)null!)
            .Build();

        await Assert.ThrowsAsync<NullReferenceException>(() =>
            dispatcher.DispatchAsync(new Triangle()).AsTask());
    }

    [Fact]
    public async Task AsyncTypeDispatcher_Null_Handler_Throws_At_Dispatch()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, double>.Create()
            .On<Circle>((Func<Circle, double>)null!)
            .Build();

        await Assert.ThrowsAsync<NullReferenceException>(() =>
            dispatcher.DispatchAsync(new Circle { Radius = 5 }).AsTask());
    }

    [Fact]
    public async Task AsyncTypeDispatcher_Null_Default_Throws_At_Dispatch()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, double>.Create()
            .Default((Func<Shape, double>)null!)
            .Build();

        await Assert.ThrowsAsync<NullReferenceException>(() =>
            dispatcher.DispatchAsync(new Triangle()).AsTask());
    }

    #endregion
}
