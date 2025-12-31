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
