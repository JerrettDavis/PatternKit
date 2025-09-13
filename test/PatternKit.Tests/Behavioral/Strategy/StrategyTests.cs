using PatternKit.Behavioral.Strategy;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Strategy;

[Feature("Strategy (Try)")]
public class TryStrategyTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private readonly record struct TryResult<TOut>(bool Matched, TOut? Value);

    private static bool IsEven(in int x) => (x & 1) == 0;
    private static bool IsDiv3(in int x) => x % 3 == 0;
    private static bool IsNegative(in int x) => x < 0;
    private static bool IsNull<T>(in T? x) => x is null;
    
    private static string HandleNull(in object? _) => "NULL";

    private static bool EvenHandler(in int x, out string? label)
    {
        if (IsEven(in x))
        {
            label = "even";
            return true;
        }

        label = null;
        return false;
    }

    private static bool Div3Handler(in int x, out string? label)
    {
        if (IsDiv3(in x))
        {
            label = "div3";
            return true;
        }

        label = null;
        return false;
    }

    private static bool NegativeHandler(in int x, out string? label)
    {
        if (IsNegative(in x))
        {
            label = "neg";
            return true;
        }

        label = null;
        return false;
    }

    private static bool Fallback(in int _, out string? label)
    {
        label = "other";
        return true;
    }

    [Scenario("First matching handler wins, order respected")]
    [Fact]
    public async Task FirstMatchWins()
    {
        await Given("a TryStrategy that checks even, then div3, then fallback", BuildPipeline)
            .When("classifying 6", s => Execute(s, 6))
            .Then("should match 'even' (even comes before div3)", r => r is { Matched: true, Value: "even" })
            .AssertPassed();

        static TryStrategy<int, string> BuildPipeline()
            => TryStrategy<int, string>.Create()
                .Always(EvenHandler) // 6 is even; should match here
                .Or.When(() => true).Add(Div3Handler)
                .Finally(Fallback)
                .Build();

        static TryResult<string> Execute(TryStrategy<int, string> s, int x)
            => s.Execute(in x, out var label) ? new(true, label) : new(false, null);
    }

    [Scenario("Conditional .When adds handlers only when condition is true")]
    [Fact]
    public async Task ConditionalWhen()
    {
        await Given("a flag that enables NEGATIVE handler", () => true)
            .When("building the pipeline with that flag", BuildWithFlag)
            .And("classifying -2", s => Execute(s, -2))
            .Then("should match 'neg' because the conditional handler was included", r => r is { Matched: true, Value: "neg" })
            .AssertPassed();

        await Given("a flag that disables NEGATIVE handler", () => false)
            .When("building the pipeline with that flag", BuildWithFlag)
            .And("classifying -2", s => Execute(s, -2))
            .Then("should fall through to fallback 'other'", r => r is { Matched: true, Value: "other" })
            .AssertPassed();
        return;

        static TryStrategy<int, string> BuildWithFlag(bool includeNegative)
            => TryStrategy<int, string>.Create()
                .When(() => includeNegative).Add(NegativeHandler).Or
                .Always(Div3Handler)
                .Finally(Fallback)
                .Build();

        static TryResult<string> Execute(TryStrategy<int, string> s, int x)
            => s.Execute(in x, out var label) ? new(true, label) : new(false, null);
    }

    [Scenario("No handler matches and no fallback -> returns false")]
    [Fact]
    public async Task NoMatchReturnsFalse()
    {
        await Given("a TryStrategy without fallback (only matches even)", BuildNoFallback)
            .When("classifying 7", s => Execute(s, 7))
            .Then("should not match", r => r is { Matched: false, Value: null })
            .AssertPassed();
        return;

        static TryStrategy<int, string> BuildNoFallback()
            => TryStrategy<int, string>.Create()
                .Always(EvenHandler)
                .Build();

        static TryResult<string> Execute(TryStrategy<int, string> s, int x)
            => s.Execute(in x, out var label) ? new(true, label) : new(false, null);
    }
    
    [Scenario("No handler matched and no default -> throws")]
    [Fact]
    public Task NoHandlerMatchesNoDefault_Throws()
    {
        return Given("a Strategy without default (only matches null)", BuildNoDefault)
            .When("executing with 42", s => Record.Exception(() => s.Execute(42)))
            .Then("should throw NoStrategyMatchedException", ex => ex is InvalidOperationException)
            .AssertPassed();

        static Strategy<object?, string> BuildNoDefault()
            => Strategy<object?, string>.Create()
                .When(IsNull).Then(HandleNull)
                .Build();
    }
    
}