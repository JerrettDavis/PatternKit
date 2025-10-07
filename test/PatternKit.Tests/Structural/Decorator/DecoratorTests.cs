using PatternKit.Structural.Decorator;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Structural.Decorator;

[Feature("Structural - Decorator<TIn,TOut> (fluent wrapping & extension)")]
public sealed class DecoratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Base component executes without decorators")]
    [Fact]
    public Task Component_Only_Executes()
        => Given("a decorator with only base component", () =>
            Decorator<int, int>.Create(static x => x * 2).Build())
            .When("execute with 5", d => d.Execute(5))
            .Then("returns base result", r => r == 10)
            .AssertPassed();

    [Scenario("Before decorator transforms input before component")]
    [Fact]
    public Task Before_Transforms_Input()
        => Given("decorator with Before", () =>
            Decorator<int, int>.Create(static x => x * 2)
                .Before(static x => x + 10)
                .Build())
            .When("execute with 5", d => d.Execute(5))
            .Then("returns (5 + 10) * 2", r => r == 30)
            .AssertPassed();

    [Scenario("After decorator transforms output from component")]
    [Fact]
    public Task After_Transforms_Output()
        => Given("decorator with After", () =>
            Decorator<int, int>.Create(static x => x * 2)
                .After(static (_, result) => result + 100)
                .Build())
            .When("execute with 5", d => d.Execute(5))
            .Then("returns (5 * 2) + 100", r => r == 110)
            .AssertPassed();

    [Scenario("Multiple Before decorators apply in order")]
    [Fact]
    public Task Multiple_Before_Order()
        => Given("decorator with two Before transforms", () =>
            Decorator<int, int>.Create(static x => x * 2)
                .Before(static x => x + 10)  // First: 5 + 10 = 15
                .Before(static x => x * 3)   // Second: 15 * 3 = 45
                .Build())
            .When("execute with 5", d => d.Execute(5))
            .Then("returns ((5 + 10) * 3) * 2", r => r == 90)
            .AssertPassed();

    [Scenario("Multiple After decorators apply in order")]
    [Fact]
    public Task Multiple_After_Order()
        => Given("decorator with two After transforms", () =>
            Decorator<int, int>.Create(static x => x * 2)
                .After(static (_, r) => r + 10)  // First (outer): receives result from inner layers
                .After(static (_, r) => r * 3)   // Second (inner): receives result from component
                .Build())
            .When("execute with 5", d => d.Execute(5))
            .Then("returns (5 * 2 * 3) + 10", r => r == 40)  // Component: 10, Second After: 30, First After: 40
            .AssertPassed();

    [Scenario("Before and After decorators work together")]
    [Fact]
    public Task Before_And_After_Combined()
        => Given("decorator with Before and After", () =>
            Decorator<int, int>.Create(static x => x * 2)
                .Before(static x => x + 1)      // 5 + 1 = 6
                .After(static (_, r) => r * 10) // (6 * 2) * 10 = 120
                .Build())
            .When("execute with 5", d => d.Execute(5))
            .Then("returns ((5 + 1) * 2) * 10", r => r == 120)
            .AssertPassed();

    [Scenario("Around decorator controls execution flow")]
    [Fact]
    public Task Around_Controls_Flow()
        => Given("decorator with Around wrapper", () =>
            {
                var log = new List<string>();
                var dec = Decorator<int, int>.Create(x => { log.Add("component"); return x * 2; })
                    .Around((x, next) =>
                    {
                        log.Add("before");
                        var result = next(x);
                        log.Add("after");
                        return result + 1;
                    })
                    .Build();
                return (dec, log);
            })
            .When("execute with 5", ctx => { var r = ctx.dec.Execute(5); return (r, ctx.log); })
            .Then("result is (5 * 2) + 1", r => r.r == 11)
            .And("log shows before, component, after", r => string.Join(",", r.log) == "before,component,after")
            .AssertPassed();

    [Scenario("Around decorator can skip next layer")]
    [Fact]
    public Task Around_Can_Skip()
        => Given("decorator with Around that short-circuits", () =>
            Decorator<int, int>.Create(static x => x * 2)
                .Around(static (x, next) => x > 10 ? 999 : next(x))
                .Build())
            .When("execute with 15", d => d.Execute(15))
            .Then("returns 999 without calling component", r => r == 999)
            .AssertPassed();

    [Scenario("Around decorator can call next multiple times")]
    [Fact]
    public Task Around_Multiple_Calls()
        => Given("decorator with Around that retries", () =>
            Decorator<int, int>.Create(static x => x + 1)
                .Around(static (x, next) =>
                {
                    var first = next(x);
                    var second = next(x);
                    return first + second;
                })
                .Build())
            .When("execute with 5", d => d.Execute(5))
            .Then("returns (5+1) + (5+1)", r => r == 12)
            .AssertPassed();

    [Scenario("Complex chain with Before, After, and Around")]
    [Fact]
    public Task Complex_Chain()
        => Given("decorator with mixed decorators", () =>
            Decorator<int, int>.Create(static x => x * 2)
                .Before(static x => x + 1)              // Input: 5 + 1 = 6
                .Around(static (x, next) => next(x) + 5) // Next returns 120, Around adds 5 = 125
                .After(static (_, r) => r * 10)         // Component returns 12, After makes it 120
                .Build())
            .When("execute with 5", d => d.Execute(5))
            .Then("returns (((5 + 1) * 2) * 10) + 5", r => r == 125)
            .AssertPassed();

    [Scenario("Decorator works with reference types")]
    [Fact]
    public Task Reference_Types()
        => Given("decorator for strings", () =>
            Decorator<string, string>.Create(static s => s.ToUpper())
                .Before(static s => s.Trim())
                .After(static (_, r) => r + "!")
                .Build())
            .When("execute with '  hello  '", d => d.Execute("  hello  "))
            .Then("returns 'HELLO!'", r => r == "HELLO!")
            .AssertPassed();

    [Scenario("After decorator has access to original input")]
    [Fact]
    public Task After_Has_Input_Access()
        => Given("decorator with After using input", () =>
            Decorator<int, int>.Create(static x => x * 2)
                .After(static (input, result) => result + input)
                .Build())
            .When("execute with 5", d => d.Execute(5))
            .Then("returns (5 * 2) + 5", r => r == 15)
            .AssertPassed();

    [Scenario("Decorator can transform between different types")]
    [Fact]
    public Task Different_Input_Output_Types()
        => Given("decorator string -> int", () =>
            Decorator<string, int>.Create(static s => s.Length)
                .Before(static s => s.Trim())
                .After(static (_, len) => len * 2)
                .Build())
            .When("execute with '  hello  '", d => d.Execute("  hello  "))
            .Then("returns trimmed length * 2", r => r == 10)
            .AssertPassed();

    [Scenario("Decorator reuse produces consistent results")]
    [Fact]
    public Task Reuse_Consistency()
        => Given("a reusable decorator", () =>
            Decorator<int, int>.Create(static x => x * 2)
                .Before(static x => x + 10)
                .Build())
            .When("execute twice with same input", d => (d.Execute(5), d.Execute(5)))
            .Then("both results equal", r => r.Item1 == r.Item2 && r.Item1 == 30)
            .AssertPassed();

    [Scenario("Null component throws ArgumentNullException")]
    [Fact]
    public Task Null_Component_Throws()
        => Given("null component", () => (Decorator<int, int>.Component?)null)
            .When("creating builder", c => Record.Exception(() => Decorator<int, int>.Create(c!)))
            .Then("throws ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();

    [Scenario("Null Before transform throws ArgumentNullException")]
    [Fact]
    public Task Null_Before_Throws()
        => Given("builder with null Before", () => Decorator<int, int>.Create(static x => x))
            .When("adding null Before", b => Record.Exception(() => b.Before(null!)))
            .Then("throws ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();

    [Scenario("Null After transform throws ArgumentNullException")]
    [Fact]
    public Task Null_After_Throws()
        => Given("builder with null After", () => Decorator<int, int>.Create(static x => x))
            .When("adding null After", b => Record.Exception(() => b.After(null!)))
            .Then("throws ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();

    [Scenario("Null Around transform throws ArgumentNullException")]
    [Fact]
    public Task Null_Around_Throws()
        => Given("builder with null Around", () => Decorator<int, int>.Create(static x => x))
            .When("adding null Around", b => Record.Exception(() => b.Around(null!)))
            .Then("throws ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();

    [Scenario("Real-world example: logging decorator")]
    [Fact]
    public Task RealWorld_Logging()
        => Given("a calculator with logging", () =>
            {
                var log = new List<string>();
                var calc = Decorator<int, int>.Create(static x => x * x)
                    .Around((x, next) =>
                    {
                        log.Add($"Input: {x}");
                        var result = next(x);
                        log.Add($"Output: {result}");
                        return result;
                    })
                    .Build();
                return (calc, log);
            })
            .When("calculate square of 7", ctx => { var r = ctx.calc.Execute(7); return (r, ctx.log); })
            .Then("returns 49", r => r.r == 49)
            .And("logged input and output", r => r.log.Count == 2 && r.log[0] == "Input: 7" && r.log[1] == "Output: 49")
            .AssertPassed();

    [Scenario("Real-world example: caching decorator")]
    [Fact]
    public Task RealWorld_Caching()
    {
        var callCount = 0;
        var cache = new Dictionary<int, int>();
        var operation = Decorator<int, int>.Create(x => { callCount++; return x * x; })
            .Around((x, next) =>
            {
                if (cache.TryGetValue(x, out var cached))
                    return cached;
                var result = next(x);
                cache[x] = result;
                return result;
            })
            .Build();

        // Execute with 5 three times
        operation.Execute(5);
        operation.Execute(5);
        operation.Execute(5);

        // Assert
        Assert.Equal(1, callCount); // Component called only once
        Assert.Equal(25, cache[5]); // Cache contains result
        
        return Task.CompletedTask;
    }

    [Scenario("Real-world example: validation decorator")]
    [Fact]
    public Task RealWorld_Validation()
        => Given("an operation with input validation", () =>
            Decorator<int, int>.Create(static x => 100 / x)
                .Before(static x => x == 0 ? throw new ArgumentException("Cannot be zero") : x)
                .Build())
            .When("execute with 0", d => Record.Exception(() => d.Execute(0)))
            .Then("throws ArgumentException", ex => ex is ArgumentException)
            .And("message mentions zero", ex => ex!.Message.Contains("zero"))
            .AssertPassed();
}
