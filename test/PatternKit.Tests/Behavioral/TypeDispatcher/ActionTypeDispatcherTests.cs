using PatternKit.Behavioral.TypeDispatcher;
using TinyBDD;

namespace PatternKit.Tests.Behavioral.TypeDispatcher;

public sealed class ActionTypeDispatcherTests
{
    private abstract class Shape { }
    private sealed class Circle : Shape { public double Radius { get; init; } }
    private sealed class Rectangle : Shape { public double Width { get; init; } public double Height { get; init; } }
    private sealed class Triangle : Shape { }

    #region ActionTypeDispatcher<TBase> Tests

    [Scenario("ActionTypeDispatcher Dispatches To Correct Handler")]
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

        ScenarioExpect.Equal("circle-5", log[0]);
        ScenarioExpect.Equal("rect-3x4", log[1]);
    }

    [Scenario("ActionTypeDispatcher Default Handler")]
    [Fact]
    public void ActionTypeDispatcher_Default_Handler()
    {
        var log = new List<string>();
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => log.Add("circle"))
            .Default(s => log.Add($"default-{s.GetType().Name}"))
            .Build();

        dispatcher.Dispatch(new Triangle());

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("default-Triangle", log[0]);
    }

    [Scenario("ActionTypeDispatcher TryDispatch Returns True")]
    [Fact]
    public void ActionTypeDispatcher_TryDispatch_Returns_True()
    {
        var log = new List<string>();
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => log.Add("circle"))
            .Build();

        var result = dispatcher.TryDispatch(new Circle { Radius = 5 });

        ScenarioExpect.True(result);
        ScenarioExpect.Single(log);
    }

    [Scenario("ActionTypeDispatcher TryDispatch Returns False When No Handler")]
    [Fact]
    public void ActionTypeDispatcher_TryDispatch_Returns_False_When_No_Handler()
    {
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => { })
            .Build();

        var result = dispatcher.TryDispatch(new Triangle());

        ScenarioExpect.False(result);
    }

    [Scenario("ActionTypeDispatcher Throws When No Handler")]
    [Fact]
    public void ActionTypeDispatcher_Throws_When_No_Handler()
    {
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => { })
            .Build();

        ScenarioExpect.Throws<InvalidOperationException>(() =>
            dispatcher.Dispatch(new Triangle()));
    }

    [Scenario("ActionTypeDispatcher Registration Order Matters")]
    [Fact]
    public void ActionTypeDispatcher_Registration_Order_Matters()
    {
        var log = new List<string>();
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => log.Add("circle"))
            .On<Shape>(s => log.Add("shape")) // More general - should only match if Circle didn't
            .Build();

        dispatcher.Dispatch(new Circle { Radius = 5 });

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("circle", log[0]);
    }

    #endregion

    #region AsyncActionTypeDispatcher<TBase> Tests

    [Scenario("AsyncActionTypeDispatcher Dispatches")]
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

        ScenarioExpect.Equal("circle-5", log[0]);
        ScenarioExpect.Equal("rect-3x4", log[1]);
    }

    [Scenario("AsyncActionTypeDispatcher Async Handler")]
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

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("circle-5", log[0]);
    }

    [Scenario("AsyncActionTypeDispatcher Default Handler")]
    [Fact]
    public async Task AsyncActionTypeDispatcher_Default_Handler()
    {
        var log = new List<string>();
        var dispatcher = AsyncActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => log.Add("circle"))
            .Default(s => log.Add($"default-{s.GetType().Name}"))
            .Build();

        await dispatcher.DispatchAsync(new Triangle());

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("default-Triangle", log[0]);
    }

    [Scenario("AsyncActionTypeDispatcher TryDispatch Returns True")]
    [Fact]
    public async Task AsyncActionTypeDispatcher_TryDispatch_Returns_True()
    {
        var log = new List<string>();
        var dispatcher = AsyncActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => log.Add("circle"))
            .Build();

        var result = await dispatcher.TryDispatchAsync(new Circle { Radius = 5 });

        ScenarioExpect.True(result);
        ScenarioExpect.Single(log);
    }

    [Scenario("AsyncActionTypeDispatcher TryDispatch Returns False")]
    [Fact]
    public async Task AsyncActionTypeDispatcher_TryDispatch_Returns_False()
    {
        var dispatcher = AsyncActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => { })
            .Build();

        var result = await dispatcher.TryDispatchAsync(new Triangle());

        ScenarioExpect.False(result);
    }

    [Scenario("AsyncActionTypeDispatcher Throws When No Handler")]
    [Fact]
    public async Task AsyncActionTypeDispatcher_Throws_When_No_Handler()
    {
        var dispatcher = AsyncActionTypeDispatcher<Shape>.Create()
            .On<Circle>(c => { })
            .Build();

        await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(new Triangle()).AsTask());
    }

    #endregion

    #region AsyncTypeDispatcher<TBase, TResult> Tests

    [Scenario("AsyncTypeDispatcher Dispatches With Result")]
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

        ScenarioExpect.Equal(Math.PI * 4, circleArea);
        ScenarioExpect.Equal(12, rectArea);
    }

    [Scenario("AsyncTypeDispatcher Async Handler")]
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

        ScenarioExpect.Equal(10, result);
    }

    [Scenario("AsyncTypeDispatcher Constant Handler")]
    [Fact]
    public async Task AsyncTypeDispatcher_Constant_Handler()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, double>.Create()
            .On<Circle>(42.0)
            .Build();

        var result = await dispatcher.DispatchAsync(new Circle { Radius = 5 });

        ScenarioExpect.Equal(42.0, result);
    }

    [Scenario("AsyncTypeDispatcher TryDispatch Returns Result")]
    [Fact]
    public async Task AsyncTypeDispatcher_TryDispatch_Returns_Result()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, double>.Create()
            .On<Circle>(c => c.Radius * 2)
            .Build();

        var (success, result) = await dispatcher.TryDispatchAsync(new Circle { Radius = 5 });

        ScenarioExpect.True(success);
        ScenarioExpect.Equal(10, result);
    }

    [Scenario("AsyncTypeDispatcher TryDispatch Returns False When No Handler")]
    [Fact]
    public async Task AsyncTypeDispatcher_TryDispatch_Returns_False_When_No_Handler()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, double>.Create()
            .On<Circle>(c => c.Radius)
            .Build();

        var (success, result) = await dispatcher.TryDispatchAsync(new Triangle());

        ScenarioExpect.False(success);
        ScenarioExpect.Equal(default, result);
    }

    [Scenario("AsyncTypeDispatcher Throws When No Handler")]
    [Fact]
    public async Task AsyncTypeDispatcher_Throws_When_No_Handler()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, double>.Create()
            .On<Circle>(c => c.Radius)
            .Build();

        await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(new Triangle()).AsTask());
    }

    [Scenario("AsyncTypeDispatcher Default Handler")]
    [Fact]
    public async Task AsyncTypeDispatcher_Default_Handler()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, string>.Create()
            .On<Circle>(c => "circle")
            .Default(s => $"default-{s.GetType().Name}")
            .Build();

        var result = await dispatcher.DispatchAsync(new Triangle());

        ScenarioExpect.Equal("default-Triangle", result);
    }

    #endregion

    #region Null Behavior Tests

    [Scenario("ActionTypeDispatcher Null Handler Throws At Dispatch")]
    [Fact]
    public void ActionTypeDispatcher_Null_Handler_Throws_At_Dispatch()
    {
        // Null handlers are stored but cause NullReferenceException when dispatched
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .On<Circle>(null!)
            .Build();

        ScenarioExpect.Throws<NullReferenceException>(() =>
            dispatcher.Dispatch(new Circle { Radius = 5 }));
    }

    [Scenario("ActionTypeDispatcher Null Default Throws At Dispatch")]
    [Fact]
    public void ActionTypeDispatcher_Null_Default_Throws_At_Dispatch()
    {
        // Null default is stored but causes NullReferenceException when dispatched
        var dispatcher = ActionTypeDispatcher<Shape>.Create()
            .Default((Action<Shape>)null!)
            .Build();

        ScenarioExpect.Throws<NullReferenceException>(() =>
            dispatcher.Dispatch(new Triangle()));
    }

    [Scenario("AsyncActionTypeDispatcher Null Handler Throws At Dispatch")]
    [Fact]
    public async Task AsyncActionTypeDispatcher_Null_Handler_Throws_At_Dispatch()
    {
        var dispatcher = AsyncActionTypeDispatcher<Shape>.Create()
            .On<Circle>((Action<Circle>)null!)
            .Build();

        await ScenarioExpect.ThrowsAsync<NullReferenceException>(() =>
            dispatcher.DispatchAsync(new Circle { Radius = 5 }).AsTask());
    }

    [Scenario("AsyncActionTypeDispatcher Null Default Throws At Dispatch")]
    [Fact]
    public async Task AsyncActionTypeDispatcher_Null_Default_Throws_At_Dispatch()
    {
        var dispatcher = AsyncActionTypeDispatcher<Shape>.Create()
            .Default((Action<Shape>)null!)
            .Build();

        await ScenarioExpect.ThrowsAsync<NullReferenceException>(() =>
            dispatcher.DispatchAsync(new Triangle()).AsTask());
    }

    [Scenario("AsyncTypeDispatcher Null Handler Throws At Dispatch")]
    [Fact]
    public async Task AsyncTypeDispatcher_Null_Handler_Throws_At_Dispatch()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, double>.Create()
            .On<Circle>((Func<Circle, double>)null!)
            .Build();

        await ScenarioExpect.ThrowsAsync<NullReferenceException>(() =>
            dispatcher.DispatchAsync(new Circle { Radius = 5 }).AsTask());
    }

    [Scenario("AsyncTypeDispatcher Null Default Throws At Dispatch")]
    [Fact]
    public async Task AsyncTypeDispatcher_Null_Default_Throws_At_Dispatch()
    {
        var dispatcher = AsyncTypeDispatcher<Shape, double>.Create()
            .Default((Func<Shape, double>)null!)
            .Build();

        await ScenarioExpect.ThrowsAsync<NullReferenceException>(() =>
            dispatcher.DispatchAsync(new Triangle()).AsTask());
    }

    #endregion
}
