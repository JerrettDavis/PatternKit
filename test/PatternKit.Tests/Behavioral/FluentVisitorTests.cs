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

    [Scenario("FluentActionVisitor Dispatches To Handlers")]
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

        ScenarioExpect.Equal(2, log.Count);
        ScenarioExpect.Equal("LOG: Test message", log[0]);
        ScenarioExpect.Equal("ERROR: Something went wrong", log[1]);
    }

    [Scenario("FluentActionVisitor Uses Default Handler")]
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

        ScenarioExpect.Equal(2, log.Count);
        ScenarioExpect.Equal("LOG: Hello", log[0]);
        ScenarioExpect.Equal("DEFAULT: ErrorEntry", log[1]);
    }

    [Scenario("FluentActionVisitor Throws When No Handler")]
    [Fact]
    public void FluentActionVisitor_Throws_When_No_Handler()
    {
        var visitor = FluentActionVisitor<ActionElement>.Create()
            .When<LogEntry>(e => { })
            .Build();

        ScenarioExpect.Throws<NotSupportedException>(() =>
            visitor.Visit(new ErrorEntry("No handler")));
    }

    [Scenario("FluentActionVisitor VisitDefault With Default Handler")]
    [Fact]
    public void FluentActionVisitor_VisitDefault_With_Default_Handler()
    {
        var log = new List<string>();
        var visitor = FluentActionVisitor<ActionElement>.Create()
            .Default(e => log.Add($"default:{e.GetType().Name}"))
            .Build();

        // Call VisitDefault directly through interface
        ((IActionVisitor)visitor).VisitDefault(new LogEntry("test"));

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("default:LogEntry", log[0]);
    }

    [Scenario("FluentActionVisitor VisitDefault Without Default Throws")]
    [Fact]
    public void FluentActionVisitor_VisitDefault_Without_Default_Throws()
    {
        var visitor = FluentActionVisitor<ActionElement>.Create()
            .When<LogEntry>(_ => { })
            .Build();

        ScenarioExpect.Throws<NotSupportedException>(() =>
            ((IActionVisitor)visitor).VisitDefault(new ErrorEntry("test")));
    }

    [Scenario("FluentActionVisitor AllTypes Handled")]
    [Fact]
    public void FluentActionVisitor_AllTypes_Handled()
    {
        var log = new List<string>();
        var visitor = FluentActionVisitor<ActionElement>.Create()
            .When<LogEntry>(e => log.Add($"LOG:{e.Message}"))
            .When<ErrorEntry>(e => log.Add($"ERR:{e.Error}"))
            .When<MetricEntry>(e => log.Add($"METRIC:{e.Name}={e.Value}"))
            .Build();

        visitor.Visit(new LogEntry("hello"));
        visitor.Visit(new ErrorEntry("oops"));
        visitor.Visit(new MetricEntry("cpu", 0.5));

        ScenarioExpect.Equal(3, log.Count);
        ScenarioExpect.Equal("LOG:hello", log[0]);
        ScenarioExpect.Equal("ERR:oops", log[1]);
        ScenarioExpect.Equal("METRIC:cpu=0.5", log[2]);
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

    [Scenario("AsyncFluentVisitor Evaluates Expression")]
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

        ScenarioExpect.Equal(30, result);
    }

    [Scenario("AsyncFluentVisitor Async Handler")]
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

        ScenarioExpect.Equal("Number: 42", result);
    }

    [Scenario("AsyncFluentVisitor Uses Default")]
    [Fact]
    public async Task AsyncFluentVisitor_Uses_Default()
    {
        var visitor = AsyncFluentVisitor<AsyncElement, string>.Create()
            .When<AsyncNumber>(n => n.Value.ToString())
            .Default(_ => "default")
            .Build();

        var result = await visitor.VisitAsync(new AsyncAdd(new AsyncNumber(1), new AsyncNumber(2)));

        ScenarioExpect.Equal("default", result);
    }

    [Scenario("AsyncFluentVisitor Constant Handler")]
    [Fact]
    public async Task AsyncFluentVisitor_Constant_Handler()
    {
        var visitor = AsyncFluentVisitor<AsyncElement, int>.Create()
            .When<AsyncNumber>(42)
            .Default(0)
            .Build();

        var result = await visitor.VisitAsync(new AsyncNumber(100));

        ScenarioExpect.Equal(42, result);
    }

    [Scenario("AsyncFluentVisitor Throws When No Handler")]
    [Fact]
    public async Task AsyncFluentVisitor_Throws_When_No_Handler()
    {
        var visitor = AsyncFluentVisitor<AsyncElement, int>.Create()
            .When<AsyncNumber>(n => n.Value)
            .Build();

        await ScenarioExpect.ThrowsAsync<NotSupportedException>(() =>
            visitor.VisitAsync(new AsyncAdd(new AsyncNumber(1), new AsyncNumber(2))).AsTask());
    }

    [Scenario("AsyncFluentVisitor Async Default")]
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

        ScenarioExpect.Equal("async-default-AsyncNumber", result);
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

    [Scenario("AsyncFluentActionVisitor Dispatches")]
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

        ScenarioExpect.Equal(2, log.Count);
        ScenarioExpect.Equal("LOG: Hello", log[0]);
        ScenarioExpect.Equal("ERROR: Oops", log[1]);
    }

    [Scenario("AsyncFluentActionVisitor Async Handler")]
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

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("ASYNC-LOG: Test", log[0]);
    }

    [Scenario("AsyncFluentActionVisitor Uses Default")]
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

        ScenarioExpect.Equal(2, log.Count);
        ScenarioExpect.Equal("log", log[0]);
        ScenarioExpect.Equal("default-AsyncErrorEntry", log[1]);
    }

    [Scenario("AsyncFluentActionVisitor Async Default")]
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

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("async-default-AsyncLogEntry", log[0]);
    }

    [Scenario("AsyncFluentActionVisitor Throws When No Handler")]
    [Fact]
    public async Task AsyncFluentActionVisitor_Throws_When_No_Handler()
    {
        var visitor = AsyncFluentActionVisitor<AsyncActionElement>.Create()
            .When<AsyncLogEntry>(e => { })
            .Build();

        await ScenarioExpect.ThrowsAsync<NotSupportedException>(() =>
            visitor.VisitAsync(new AsyncErrorEntry("No handler")).AsTask());
    }
}

#region Additional FluentVisitor Coverage Tests

public sealed class FluentVisitorCoverageTests
{
    private abstract record Elem : IVisitable
    {
        public abstract TResult Accept<TResult>(IVisitor<TResult> visitor);
    }
    private sealed record ElemA(string Value) : Elem
    {
        public override TResult Accept<TResult>(IVisitor<TResult> visitor)
            => visitor is FluentVisitor<Elem, TResult> fv
                ? fv.Handle(this)
                : visitor.VisitDefault(this);
    }
    private sealed record ElemB(int Value) : Elem
    {
        public override TResult Accept<TResult>(IVisitor<TResult> visitor)
            => visitor is FluentVisitor<Elem, TResult> fv
                ? fv.Handle(this)
                : visitor.VisitDefault(this);
    }

    [Scenario("FluentVisitor When Constant Handler")]
    [Fact]
    public void FluentVisitor_When_Constant_Handler()
    {
        var visitor = FluentVisitor<Elem, string>.Create()
            .When<ElemA>("constant-result")
            .Build();

        var result = visitor.Visit(new ElemA("test"));

        ScenarioExpect.Equal("constant-result", result);
    }

    [Scenario("FluentVisitor Default Constant")]
    [Fact]
    public void FluentVisitor_Default_Constant()
    {
        var visitor = FluentVisitor<Elem, string>.Create()
            .Default("default-constant")
            .Build();

        var result = visitor.Visit(new ElemA("test"));

        ScenarioExpect.Equal("default-constant", result);
    }

    [Scenario("FluentVisitor VisitDefault With Default Returns Result")]
    [Fact]
    public void FluentVisitor_VisitDefault_With_Default_Returns_Result()
    {
        var visitor = FluentVisitor<Elem, string>.Create()
            .Default(e => "from-default")
            .Build();

        var result = ((IVisitor<string>)visitor).VisitDefault(new ElemA("test"));

        ScenarioExpect.Equal("from-default", result);
    }

    [Scenario("FluentVisitor VisitDefault Without Default Throws")]
    [Fact]
    public void FluentVisitor_VisitDefault_Without_Default_Throws()
    {
        var visitor = FluentVisitor<Elem, string>.Create()
            .When<ElemA>(e => e.Value)
            .Build();

        ScenarioExpect.Throws<NotSupportedException>(() =>
            ((IVisitor<string>)visitor).VisitDefault(new ElemB(42)));
    }

    [Scenario("FluentVisitor Multiple Handlers Work")]
    [Fact]
    public void FluentVisitor_Multiple_Handlers_Work()
    {
        var visitor = FluentVisitor<Elem, string>.Create()
            .When<ElemA>(e => $"A:{e.Value}")
            .When<ElemB>(e => $"B:{e.Value}")
            .Build();

        ScenarioExpect.Equal("A:hello", visitor.Visit(new ElemA("hello")));
        ScenarioExpect.Equal("B:42", visitor.Visit(new ElemB(42)));
    }

    [Scenario("FluentVisitor Handler Override Works")]
    [Fact]
    public void FluentVisitor_Handler_Override_Works()
    {
        var visitor = FluentVisitor<Elem, string>.Create()
            .When<ElemA>(_ => "first")
            .When<ElemA>(_ => "second")
            .Build();

        ScenarioExpect.Equal("second", visitor.Visit(new ElemA("test")));
    }
}

public sealed class AsyncFluentVisitorCoverageTests
{
    private abstract record AsyncElement : IAsyncVisitable
    {
        public abstract ValueTask<TResult> AcceptAsync<TResult>(IAsyncVisitor<TResult> visitor, CancellationToken ct);
    }

    private sealed record AsyncNumberElement(int Value) : AsyncElement
    {
        public override ValueTask<TResult> AcceptAsync<TResult>(IAsyncVisitor<TResult> visitor, CancellationToken ct)
            => visitor is AsyncFluentVisitor<AsyncElement, TResult> fv
                ? fv.HandleAsync(this, ct)
                : visitor.VisitDefaultAsync(this, ct);
    }

    private sealed record AsyncTextElement(string Text) : AsyncElement
    {
        public override ValueTask<TResult> AcceptAsync<TResult>(IAsyncVisitor<TResult> visitor, CancellationToken ct)
            => visitor is AsyncFluentVisitor<AsyncElement, TResult> fv
                ? fv.HandleAsync(this, ct)
                : visitor.VisitDefaultAsync(this, ct);
    }

    [Scenario("AsyncFluentVisitor VisitDefaultAsync With Default")]
    [Fact]
    public async Task AsyncFluentVisitor_VisitDefaultAsync_With_Default()
    {
        var visitor = AsyncFluentVisitor<AsyncElement, string>.Create()
            .Default(e => "default-handler")
            .Build();

        var result = await ((IAsyncVisitor<string>)visitor).VisitDefaultAsync(new AsyncNumberElement(42), CancellationToken.None);

        ScenarioExpect.Equal("default-handler", result);
    }

    [Scenario("AsyncFluentVisitor VisitDefaultAsync Without Default Throws")]
    [Fact]
    public async Task AsyncFluentVisitor_VisitDefaultAsync_Without_Default_Throws()
    {
        var visitor = AsyncFluentVisitor<AsyncElement, string>.Create()
            .When<AsyncNumberElement>(e => e.Value.ToString())
            .Build();

        await ScenarioExpect.ThrowsAsync<NotSupportedException>(() =>
            ((IAsyncVisitor<string>)visitor).VisitDefaultAsync(new AsyncTextElement("test"), CancellationToken.None).AsTask());
    }

    [Scenario("AsyncFluentVisitor Async Handler With Cancellation")]
    [Fact]
    public async Task AsyncFluentVisitor_Async_Handler_With_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        var visitor = AsyncFluentVisitor<AsyncElement, string>.Create()
            .When<AsyncNumberElement>(async (e, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return e.Value.ToString();
            })
            .Build();

        var result = await visitor.VisitAsync(new AsyncNumberElement(42), cts.Token);

        ScenarioExpect.Equal("42", result);
    }

    [Scenario("AsyncFluentVisitor Sync Handler Ignores CancellationToken")]
    [Fact]
    public async Task AsyncFluentVisitor_Sync_Handler_Ignores_CancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var visitor = AsyncFluentVisitor<AsyncElement, string>.Create()
            .When<AsyncNumberElement>(e => e.Value.ToString())
            .Build();

        var result = await visitor.VisitAsync(new AsyncNumberElement(99), cts.Token);

        ScenarioExpect.Equal("99", result);
    }
}

public sealed class AsyncFluentActionVisitorCoverageTests
{
    private abstract record AsyncActionElement : IAsyncActionVisitable
    {
        public abstract ValueTask AcceptAsync(IAsyncActionVisitor visitor, CancellationToken ct);
    }

    private sealed record AsyncLogElement(string Message) : AsyncActionElement
    {
        public override ValueTask AcceptAsync(IAsyncActionVisitor visitor, CancellationToken ct)
            => visitor is AsyncFluentActionVisitor<AsyncActionElement> fv
                ? fv.HandleAsync(this, ct)
                : visitor.VisitDefaultAsync(this, ct);
    }

    private sealed record AsyncErrorElement(string Error) : AsyncActionElement
    {
        public override ValueTask AcceptAsync(IAsyncActionVisitor visitor, CancellationToken ct)
            => visitor is AsyncFluentActionVisitor<AsyncActionElement> fv
                ? fv.HandleAsync(this, ct)
                : visitor.VisitDefaultAsync(this, ct);
    }

    [Scenario("AsyncFluentActionVisitor VisitDefaultAsync With Default")]
    [Fact]
    public async Task AsyncFluentActionVisitor_VisitDefaultAsync_With_Default()
    {
        var log = new List<string>();
        var visitor = AsyncFluentActionVisitor<AsyncActionElement>.Create()
            .Default(e => log.Add("default"))
            .Build();

        await ((IAsyncActionVisitor)visitor).VisitDefaultAsync(new AsyncLogElement("test"), CancellationToken.None);

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("default", log[0]);
    }

    [Scenario("AsyncFluentActionVisitor VisitDefaultAsync Without Default Throws")]
    [Fact]
    public async Task AsyncFluentActionVisitor_VisitDefaultAsync_Without_Default_Throws()
    {
        var visitor = AsyncFluentActionVisitor<AsyncActionElement>.Create()
            .When<AsyncLogElement>(e => { })
            .Build();

        await ScenarioExpect.ThrowsAsync<NotSupportedException>(() =>
            ((IAsyncActionVisitor)visitor).VisitDefaultAsync(new AsyncErrorElement("err"), CancellationToken.None).AsTask());
    }

    [Scenario("AsyncFluentActionVisitor Async Handler With Cancellation")]
    [Fact]
    public async Task AsyncFluentActionVisitor_Async_Handler_With_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        var log = new List<string>();
        var visitor = AsyncFluentActionVisitor<AsyncActionElement>.Create()
            .When<AsyncLogElement>(async (e, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                log.Add(e.Message);
            })
            .Build();

        await visitor.VisitAsync(new AsyncLogElement("hello"), cts.Token);

        ScenarioExpect.Single(log);
    }
}

#endregion
