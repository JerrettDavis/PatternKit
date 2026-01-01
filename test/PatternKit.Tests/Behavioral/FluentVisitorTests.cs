using PatternKit.Behavioral.Visitor;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - FluentVisitor (True GoF Visitor with Double Dispatch)")]
public sealed class FluentVisitorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // Element hierarchy implementing IVisitable for double dispatch
    private abstract record Expression : IVisitable
    {
        public abstract TResult Accept<TResult>(IVisitor<TResult> visitor);
    }

    private sealed record NumberExpr(int Value) : Expression
    {
        public override TResult Accept<TResult>(IVisitor<TResult> visitor)
            => visitor is FluentVisitor<Expression, TResult> fv
                ? fv.Handle(this)
                : visitor.VisitDefault(this);
    }

    private sealed record AddExpr(Expression Left, Expression Right) : Expression
    {
        public override TResult Accept<TResult>(IVisitor<TResult> visitor)
            => visitor is FluentVisitor<Expression, TResult> fv
                ? fv.Handle(this)
                : visitor.VisitDefault(this);
    }

    private sealed record NegExpr(Expression Inner) : Expression
    {
        public override TResult Accept<TResult>(IVisitor<TResult> visitor)
            => visitor is FluentVisitor<Expression, TResult> fv
                ? fv.Handle(this)
                : visitor.VisitDefault(this);
    }

    [Scenario("FluentVisitor uses double dispatch to evaluate expression tree")]
    [Fact]
    public Task FluentVisitor_DoubleDispatch_Evaluation()
        => Given("an expression evaluator visitor", () =>
            {
                FluentVisitor<Expression, int>? evaluator = null;
                evaluator = FluentVisitor<Expression, int>.Create()
                    .When<NumberExpr>(n => n.Value)
                    .When<AddExpr>(a => evaluator!.Visit(a.Left) + evaluator!.Visit(a.Right))
                    .When<NegExpr>(n => -evaluator!.Visit(n.Inner))
                    .Build();
                return evaluator;
            })
           .When("evaluating (1 + 2)", v => v.Visit(new AddExpr(new NumberExpr(1), new NumberExpr(2))))
           .Then("result is 3", r => r == 3)
           .AssertPassed();

    [Scenario("FluentVisitor handles complex nested expressions")]
    [Fact]
    public Task FluentVisitor_ComplexExpression()
        => Given("an expression evaluator visitor", () =>
            {
                FluentVisitor<Expression, int>? evaluator = null;
                evaluator = FluentVisitor<Expression, int>.Create()
                    .When<NumberExpr>(n => n.Value)
                    .When<AddExpr>(a => evaluator!.Visit(a.Left) + evaluator!.Visit(a.Right))
                    .When<NegExpr>(n => -evaluator!.Visit(n.Inner))
                    .Build();
                return evaluator;
            })
           .When("evaluating -(1 + (2 + 3))", v =>
            {
                // -(1 + (2 + 3)) = -(1 + 5) = -6
                var expr = new NegExpr(
                    new AddExpr(
                        new NumberExpr(1),
                        new AddExpr(new NumberExpr(2), new NumberExpr(3))));
                return v.Visit(expr);
            })
           .Then("result is -6", r => r == -6)
           .AssertPassed();

    [Scenario("FluentVisitor with default handler")]
    [Fact]
    public Task FluentVisitor_DefaultHandler()
        => Given("a visitor with only number handler and default", () =>
            FluentVisitor<Expression, string>.Create()
                .When<NumberExpr>(n => $"num:{n.Value}")
                .Default(_ => "unknown")
                .Build())
           .When("visiting an AddExpr (no specific handler)", v => v.Visit(new AddExpr(new NumberExpr(1), new NumberExpr(2))))
           .Then("uses default handler", r => r == "unknown")
           .AssertPassed();

    [Scenario("FluentVisitor throws when no handler and no default")]
    [Fact]
    public Task FluentVisitor_ThrowsWithoutDefault()
        => Given("a visitor with only number handler", () =>
            FluentVisitor<Expression, string>.Create()
                .When<NumberExpr>(n => n.Value.ToString())
                .Build())
           .When("visiting an AddExpr", v =>
            {
                try
                {
                    v.Visit(new AddExpr(new NumberExpr(1), new NumberExpr(2)));
                    return false;
                }
                catch (NotSupportedException)
                {
                    return true;
                }
            })
           .Then("throws NotSupportedException", threw => threw)
           .AssertPassed();

    [Scenario("FluentVisitor produces different results from same tree (multiple visitors)")]
    [Fact]
    public Task FluentVisitor_MultipleVisitors()
        => Given("two different visitors for the same expression tree", () =>
            {
                FluentVisitor<Expression, int>? evaluator = null;
                evaluator = FluentVisitor<Expression, int>.Create()
                    .When<NumberExpr>(n => n.Value)
                    .When<AddExpr>(a => evaluator!.Visit(a.Left) + evaluator!.Visit(a.Right))
                    .Build();

                FluentVisitor<Expression, int>? counter = null;
                counter = FluentVisitor<Expression, int>.Create()
                    .When<NumberExpr>(_ => 1)
                    .When<AddExpr>(a => counter!.Visit(a.Left) + counter!.Visit(a.Right) + 1)
                    .Build();

                return (evaluator, counter);
            })
           .When("visiting same expression with both", v =>
            {
                var expr = new AddExpr(new NumberExpr(5), new NumberExpr(3));
                return (eval: v.evaluator.Visit(expr), count: v.counter.Visit(expr));
            })
           .Then("evaluator returns 8", r => r.eval == 8)
           .And("counter returns 3 (2 numbers + 1 add)", r => r.count == 3)
           .AssertPassed();

    [Scenario("Constant result handler works correctly")]
    [Fact]
    public Task FluentVisitor_ConstantHandler()
        => Given("a visitor with constant handlers", () =>
            FluentVisitor<Expression, string>.Create()
                .When<NumberExpr>("number")
                .When<AddExpr>("addition")
                .Default("other")
                .Build())
           .When("visiting different expression types", v => (
               num: v.Visit(new NumberExpr(42)),
               add: v.Visit(new AddExpr(new NumberExpr(1), new NumberExpr(2))),
               neg: v.Visit(new NegExpr(new NumberExpr(1)))
           ))
           .Then("NumberExpr -> 'number'", r => r.num == "number")
           .And("AddExpr -> 'addition'", r => r.add == "addition")
           .And("NegExpr -> 'other' (default)", r => r.neg == "other")
           .AssertPassed();
}

public sealed class FluentActionVisitorTests
{
    // Action visitable elements
    private abstract record ActionElement : IActionVisitable
    {
        public abstract void Accept(IActionVisitor visitor);
    }

    private sealed record LogEntry(string Message) : ActionElement
    {
        public override void Accept(IActionVisitor visitor)
        {
            if (visitor is FluentActionVisitor<ActionElement> fv)
                fv.Handle(this);
            else
                visitor.VisitDefault(this);
        }
    }

    private sealed record ErrorEntry(string Error) : ActionElement
    {
        public override void Accept(IActionVisitor visitor)
        {
            if (visitor is FluentActionVisitor<ActionElement> fv)
                fv.Handle(this);
            else
                visitor.VisitDefault(this);
        }
    }

    private sealed record MetricEntry(string Name, double Value) : ActionElement
    {
        public override void Accept(IActionVisitor visitor)
        {
            if (visitor is FluentActionVisitor<ActionElement> fv)
                fv.Handle(this);
            else
                visitor.VisitDefault(this);
        }
    }

    [Fact]
    public void FluentActionVisitor_Dispatches_To_Handlers()
    {
        var log = new List<string>();
        var visitor = FluentActionVisitor<ActionElement>.Create()
            .When<LogEntry>(e => log.Add($"LOG: {e.Message}"))
            .When<ErrorEntry>(e => log.Add($"ERROR: {e.Error}"))
            .Build();

        visitor.Visit(new LogEntry("Test message"));
        visitor.Visit(new ErrorEntry("Something went wrong"));

        Assert.Equal(2, log.Count);
        Assert.Equal("LOG: Test message", log[0]);
        Assert.Equal("ERROR: Something went wrong", log[1]);
    }

    [Fact]
    public void FluentActionVisitor_Uses_Default_Handler()
    {
        var log = new List<string>();
        var visitor = FluentActionVisitor<ActionElement>.Create()
            .When<LogEntry>(e => log.Add($"LOG: {e.Message}"))
            .Default(e => log.Add($"DEFAULT: {e.GetType().Name}"))
            .Build();

        visitor.Visit(new LogEntry("Hello"));
        visitor.Visit(new ErrorEntry("Oops"));

        Assert.Equal(2, log.Count);
        Assert.Equal("LOG: Hello", log[0]);
        Assert.Equal("DEFAULT: ErrorEntry", log[1]);
    }

    [Fact]
    public void FluentActionVisitor_Throws_When_No_Handler()
    {
        var visitor = FluentActionVisitor<ActionElement>.Create()
            .When<LogEntry>(e => { })
            .Build();

        Assert.Throws<NotSupportedException>(() =>
            visitor.Visit(new ErrorEntry("No handler")));
    }
}

public sealed class AsyncFluentVisitorTests
{
    // Async visitable elements
    private abstract record AsyncElement : IAsyncVisitable
    {
        public abstract ValueTask<TResult> AcceptAsync<TResult>(IAsyncVisitor<TResult> visitor, CancellationToken ct);
    }

    private sealed record AsyncNumber(int Value) : AsyncElement
    {
        public override ValueTask<TResult> AcceptAsync<TResult>(IAsyncVisitor<TResult> visitor, CancellationToken ct)
            => visitor is AsyncFluentVisitor<AsyncElement, TResult> fv
                ? fv.HandleAsync(this, ct)
                : visitor.VisitDefaultAsync(this, ct);
    }

    private sealed record AsyncAdd(AsyncElement Left, AsyncElement Right) : AsyncElement
    {
        public override ValueTask<TResult> AcceptAsync<TResult>(IAsyncVisitor<TResult> visitor, CancellationToken ct)
            => visitor is AsyncFluentVisitor<AsyncElement, TResult> fv
                ? fv.HandleAsync(this, ct)
                : visitor.VisitDefaultAsync(this, ct);
    }

    [Fact]
    public async Task AsyncFluentVisitor_Evaluates_Expression()
    {
        AsyncFluentVisitor<AsyncElement, int>? evaluator = null;
        evaluator = AsyncFluentVisitor<AsyncElement, int>.Create()
            .When<AsyncNumber>(n => n.Value)
            .When<AsyncAdd>(async (a, ct) =>
                await evaluator!.VisitAsync(a.Left, ct) +
                await evaluator!.VisitAsync(a.Right, ct))
            .Build();

        var expr = new AsyncAdd(new AsyncNumber(10), new AsyncNumber(20));
        var result = await evaluator.VisitAsync(expr);

        Assert.Equal(30, result);
    }

    [Fact]
    public async Task AsyncFluentVisitor_Async_Handler()
    {
        var visitor = AsyncFluentVisitor<AsyncElement, string>.Create()
            .When<AsyncNumber>(async (n, ct) =>
            {
                await Task.Delay(1, ct);
                return $"Number: {n.Value}";
            })
            .Default(e => $"Unknown: {e.GetType().Name}")
            .Build();

        var result = await visitor.VisitAsync(new AsyncNumber(42));

        Assert.Equal("Number: 42", result);
    }

    [Fact]
    public async Task AsyncFluentVisitor_Uses_Default()
    {
        var visitor = AsyncFluentVisitor<AsyncElement, string>.Create()
            .When<AsyncNumber>(n => n.Value.ToString())
            .Default(_ => "default")
            .Build();

        var result = await visitor.VisitAsync(new AsyncAdd(new AsyncNumber(1), new AsyncNumber(2)));

        Assert.Equal("default", result);
    }

    [Fact]
    public async Task AsyncFluentVisitor_Constant_Handler()
    {
        var visitor = AsyncFluentVisitor<AsyncElement, int>.Create()
            .When<AsyncNumber>(42)
            .Default(0)
            .Build();

        var result = await visitor.VisitAsync(new AsyncNumber(100));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task AsyncFluentVisitor_Throws_When_No_Handler()
    {
        var visitor = AsyncFluentVisitor<AsyncElement, int>.Create()
            .When<AsyncNumber>(n => n.Value)
            .Build();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            visitor.VisitAsync(new AsyncAdd(new AsyncNumber(1), new AsyncNumber(2))).AsTask());
    }

    [Fact]
    public async Task AsyncFluentVisitor_Async_Default()
    {
        var visitor = AsyncFluentVisitor<AsyncElement, string>.Create()
            .Default(async (e, ct) =>
            {
                await Task.Delay(1, ct);
                return $"async-default-{e.GetType().Name}";
            })
            .Build();

        var result = await visitor.VisitAsync(new AsyncNumber(1));

        Assert.Equal("async-default-AsyncNumber", result);
    }
}

public sealed class AsyncFluentActionVisitorTests
{
    // Async action visitable elements
    private abstract record AsyncActionElement : IAsyncActionVisitable
    {
        public abstract ValueTask AcceptAsync(IAsyncActionVisitor visitor, CancellationToken ct);
    }

    private sealed record AsyncLogEntry(string Message) : AsyncActionElement
    {
        public override ValueTask AcceptAsync(IAsyncActionVisitor visitor, CancellationToken ct)
            => visitor is AsyncFluentActionVisitor<AsyncActionElement> fv
                ? fv.HandleAsync(this, ct)
                : visitor.VisitDefaultAsync(this, ct);
    }

    private sealed record AsyncErrorEntry(string Error) : AsyncActionElement
    {
        public override ValueTask AcceptAsync(IAsyncActionVisitor visitor, CancellationToken ct)
            => visitor is AsyncFluentActionVisitor<AsyncActionElement> fv
                ? fv.HandleAsync(this, ct)
                : visitor.VisitDefaultAsync(this, ct);
    }

    [Fact]
    public async Task AsyncFluentActionVisitor_Dispatches()
    {
        var log = new List<string>();
        var visitor = AsyncFluentActionVisitor<AsyncActionElement>.Create()
            .When<AsyncLogEntry>(e => log.Add($"LOG: {e.Message}"))
            .When<AsyncErrorEntry>(e => log.Add($"ERROR: {e.Error}"))
            .Build();

        await visitor.VisitAsync(new AsyncLogEntry("Hello"));
        await visitor.VisitAsync(new AsyncErrorEntry("Oops"));

        Assert.Equal(2, log.Count);
        Assert.Equal("LOG: Hello", log[0]);
        Assert.Equal("ERROR: Oops", log[1]);
    }

    [Fact]
    public async Task AsyncFluentActionVisitor_Async_Handler()
    {
        var log = new List<string>();
        var visitor = AsyncFluentActionVisitor<AsyncActionElement>.Create()
            .When<AsyncLogEntry>(async (e, ct) =>
            {
                await Task.Delay(1, ct);
                log.Add($"ASYNC-LOG: {e.Message}");
            })
            .Build();

        await visitor.VisitAsync(new AsyncLogEntry("Test"));

        Assert.Single(log);
        Assert.Equal("ASYNC-LOG: Test", log[0]);
    }

    [Fact]
    public async Task AsyncFluentActionVisitor_Uses_Default()
    {
        var log = new List<string>();
        var visitor = AsyncFluentActionVisitor<AsyncActionElement>.Create()
            .When<AsyncLogEntry>(e => log.Add("log"))
            .Default(e => log.Add($"default-{e.GetType().Name}"))
            .Build();

        await visitor.VisitAsync(new AsyncLogEntry("Hi"));
        await visitor.VisitAsync(new AsyncErrorEntry("Err"));

        Assert.Equal(2, log.Count);
        Assert.Equal("log", log[0]);
        Assert.Equal("default-AsyncErrorEntry", log[1]);
    }

    [Fact]
    public async Task AsyncFluentActionVisitor_Async_Default()
    {
        var log = new List<string>();
        var visitor = AsyncFluentActionVisitor<AsyncActionElement>.Create()
            .Default(async (e, ct) =>
            {
                await Task.Delay(1, ct);
                log.Add($"async-default-{e.GetType().Name}");
            })
            .Build();

        await visitor.VisitAsync(new AsyncLogEntry("Test"));

        Assert.Single(log);
        Assert.Equal("async-default-AsyncLogEntry", log[0]);
    }

    [Fact]
    public async Task AsyncFluentActionVisitor_Throws_When_No_Handler()
    {
        var visitor = AsyncFluentActionVisitor<AsyncActionElement>.Create()
            .When<AsyncLogEntry>(e => { })
            .Build();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            visitor.VisitAsync(new AsyncErrorEntry("No handler")).AsTask());
    }
}
