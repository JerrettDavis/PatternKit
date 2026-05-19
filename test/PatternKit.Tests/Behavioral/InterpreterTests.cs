using PatternKit.Behavioral.Interpreter;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;
using static PatternKit.Behavioral.Interpreter.ExpressionExtensions;

namespace PatternKit.Tests.Behavioral;

#region Sync Interpreter Tests

[Feature("Behavioral - Interpreter (Expression Evaluation)")]
public sealed class InterpreterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Interpreter evaluates simple arithmetic expressions")]
    [Fact]
    public Task Interpreter_SimpleArithmetic()
        => Given("an arithmetic interpreter", () =>
            Interpreter.Create<object, double>()
                .Terminal("number", token => double.Parse(token))
                .Binary("add", (left, right) => left + right)
                .Binary("mul", (left, right) => left * right)
                .Build())
           .When("evaluating 1 + 2", interp =>
            {
                var expr = NonTerminal("add",
                    Terminal("number", "1"),
                    Terminal("number", "2"));
                return interp.Interpret(expr);
            })
           .Then("result is 3", r => r == 3.0)
           .AssertPassed();

    [Scenario("Interpreter evaluates nested expressions")]
    [Fact]
    public Task Interpreter_NestedExpressions()
        => Given("an arithmetic interpreter", () =>
            Interpreter.Create<object, double>()
                .Terminal("number", token => double.Parse(token))
                .Binary("add", (left, right) => left + right)
                .Binary("mul", (left, right) => left * right)
                .Build())
           .When("evaluating 1 + (2 * 3)", interp =>
            {
                // 1 + (2 * 3) = 1 + 6 = 7
                var expr = NonTerminal("add",
                    Terminal("number", "1"),
                    NonTerminal("mul",
                        Terminal("number", "2"),
                        Terminal("number", "3")));
                return interp.Interpret(expr);
            })
           .Then("result is 7", r => r == 7.0)
           .AssertPassed();

    [Scenario("Interpreter supports unary operators")]
    [Fact]
    public Task Interpreter_UnaryOperators()
        => Given("an interpreter with unary negation", () =>
            Interpreter.Create<object, double>()
                .Terminal("number", token => double.Parse(token))
                .Unary("neg", value => -value)
                .Binary("add", (left, right) => left + right)
                .Build())
           .When("evaluating -(5) + 3", interp =>
            {
                var expr = NonTerminal("add",
                    NonTerminal("neg", Terminal("number", "5")),
                    Terminal("number", "3"));
                return interp.Interpret(expr);
            })
           .Then("result is -2", r => r == -2.0)
           .AssertPassed();

    [Scenario("Interpreter uses context for variable lookup")]
    [Fact]
    public Task Interpreter_WithContext()
        => Given("an interpreter with variable lookup", () =>
            Interpreter.Create<Dictionary<string, double>, double>()
                .Terminal("number", token => double.Parse(token))
                .Terminal("var", (name, ctx) => ctx[name])
                .Binary("add", (left, right) => left + right)
                .Build())
           .When("evaluating x + 10 where x=5", interp =>
            {
                var ctx = new Dictionary<string, double> { ["x"] = 5.0 };
                var expr = NonTerminal("add",
                    Terminal("var", "x"),
                    Terminal("number", "10"));
                return interp.Interpret(expr, ctx);
            })
           .Then("result is 15", r => r == 15.0)
           .AssertPassed();

    [Scenario("Interpreter throws for unknown terminal type")]
    [Fact]
    public Task Interpreter_ThrowsUnknownTerminal()
        => Given("an interpreter without 'string' terminal", () =>
            Interpreter.Create<object, double>()
                .Terminal("number", token => double.Parse(token))
                .Build())
           .When("interpreting unknown terminal", interp =>
            {
                try
                {
                    interp.Interpret(Terminal("string", "hello"));
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            })
           .Then("throws InvalidOperationException", threw => threw)
           .AssertPassed();

    [Scenario("Interpreter throws for unknown non-terminal type")]
    [Fact]
    public Task Interpreter_ThrowsUnknownNonTerminal()
        => Given("an interpreter without 'div' operator", () =>
            Interpreter.Create<object, double>()
                .Terminal("number", token => double.Parse(token))
                .Binary("add", (left, right) => left + right)
                .Build())
           .When("interpreting unknown operator", interp =>
            {
                try
                {
                    var expr = NonTerminal("div",
                        Terminal("number", "10"),
                        Terminal("number", "2"));
                    interp.Interpret(expr);
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            })
           .Then("throws InvalidOperationException", threw => threw)
           .AssertPassed();

    [Scenario("TryInterpret returns false for invalid expressions")]
    [Fact]
    public Task Interpreter_TryInterpret_ReturnsFalse()
        => Given("an interpreter", () =>
            Interpreter.Create<object, double>()
                .Terminal("number", token => double.Parse(token))
                .Build())
           .When("trying to interpret unknown terminal", interp =>
               interp.TryInterpret(Terminal("unknown", "value"), null!, out _))
           .Then("returns false", ok => !ok)
           .AssertPassed();

    [Scenario("HasTerminal and HasNonTerminal check registrations")]
    [Fact]
    public Task Interpreter_HasChecks()
        => Given("an interpreter with number terminal and add operator", () =>
            Interpreter.Create<object, double>()
                .Terminal("number", token => double.Parse(token))
                .Binary("add", (left, right) => left + right)
                .Build())
           .When("checking registrations", interp => (
               hasNumber: interp.HasTerminal("number"),
               hasString: interp.HasTerminal("string"),
               hasAdd: interp.HasNonTerminal("add"),
               hasMul: interp.HasNonTerminal("mul")
           ))
           .Then("has number terminal", r => r.hasNumber)
           .And("no string terminal", r => !r.hasString)
           .And("has add operator", r => r.hasAdd)
           .And("no mul operator", r => !r.hasMul)
           .AssertPassed();

    [Scenario("Interpreter evaluates boolean expressions")]
    [Fact]
    public Task Interpreter_BooleanExpressions()
        => Given("a boolean interpreter", () =>
            Interpreter.Create<object, bool>()
                .Terminal("bool", token => bool.Parse(token))
                .Binary("and", (left, right) => left && right)
                .Binary("or", (left, right) => left || right)
                .Unary("not", value => !value)
                .Build())
           .When("evaluating true && !false", interp =>
            {
                var expr = NonTerminal("and",
                    Terminal("bool", "true"),
                    NonTerminal("not", Terminal("bool", "false")));
                return interp.Interpret(expr);
            })
           .Then("result is true", r => r)
           .AssertPassed();

    [Scenario("Interpreter evaluates string expressions")]
    [Fact]
    public Task Interpreter_StringExpressions()
        => Given("a string interpreter", () =>
            Interpreter.Create<object, string>()
                .Terminal("str", token => token)
                .Binary("concat", (left, right) => left + right)
                .Unary("upper", value => value.ToUpperInvariant())
                .Build())
           .When("evaluating concat('hello', upper(' world'))", interp =>
            {
                var expr = NonTerminal("concat",
                    Terminal("str", "hello"),
                    NonTerminal("upper", Terminal("str", " world")));
                return interp.Interpret(expr);
            })
           .Then("result is 'hello WORLD'", r => r == "hello WORLD")
           .AssertPassed();

    [Scenario("Complex expression tree with multiple operators")]
    [Fact]
    public Task Interpreter_ComplexExpression()
        => Given("a full arithmetic interpreter", () =>
            Interpreter.Create<object, double>()
                .Terminal("number", token => double.Parse(token))
                .Binary("add", (l, r) => l + r)
                .Binary("sub", (l, r) => l - r)
                .Binary("mul", (l, r) => l * r)
                .Binary("div", (l, r) => l / r)
                .Unary("neg", v => -v)
                .Build())
           .When("evaluating ((10 - 4) * 3) / (-(2))", interp =>
            {
                // ((10 - 4) * 3) / (-(2)) = (6 * 3) / (-2) = 18 / -2 = -9
                var expr = NonTerminal("div",
                    NonTerminal("mul",
                        NonTerminal("sub",
                            Terminal("number", "10"),
                            Terminal("number", "4")),
                        Terminal("number", "3")),
                    NonTerminal("neg", Terminal("number", "2")));
                return interp.Interpret(expr);
            })
           .Then("result is -9", r => r == -9.0)
           .AssertPassed();

    [Scenario("NonTerminal with custom handler")]
    [Fact]
    public Task Interpreter_NonTerminalWithContext()
        => Given("an interpreter with context-aware non-terminal", () =>
            Interpreter.Create<Dictionary<string, double>, double>()
                .Terminal("number", token => double.Parse(token))
                .NonTerminal("lookup", (args, ctx) => ctx.TryGetValue("multiplier", out var m) ? args[0] * m : args[0])
                .Build())
           .When("evaluating with multiplier=2", interp =>
            {
                var ctx = new Dictionary<string, double> { ["multiplier"] = 2.0 };
                var expr = NonTerminal("lookup", Terminal("number", "10"));
                return interp.Interpret(expr, ctx);
            })
           .Then("result is 20", r => r == 20.0)
           .AssertPassed();

    [Scenario("Binary throws with wrong operand count")]
    [Fact]
    public Task Interpreter_BinaryThrowsWithWrongOperandCount()
        => Given("an interpreter with binary operator", () =>
            Interpreter.Create<object, double>()
                .Terminal("number", token => double.Parse(token))
                .Binary("add", (l, r) => l + r)
                .Build())
           .When("evaluating add with one operand", interp =>
            {
                try
                {
                    var expr = NonTerminal("add", Terminal("number", "1"));
                    interp.Interpret(expr);
                    return false;
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message.Contains("exactly 2 operands");
                }
            })
           .Then("throws with proper message", threw => threw)
           .AssertPassed();

    [Scenario("Unary throws with wrong operand count")]
    [Fact]
    public Task Interpreter_UnaryThrowsWithWrongOperandCount()
        => Given("an interpreter with unary operator", () =>
            Interpreter.Create<object, double>()
                .Terminal("number", token => double.Parse(token))
                .Unary("neg", v => -v)
                .Build())
           .When("evaluating neg with two operands", interp =>
            {
                try
                {
                    var expr = NonTerminal("neg", Terminal("number", "1"), Terminal("number", "2"));
                    interp.Interpret(expr);
                    return false;
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message.Contains("exactly 1 operand");
                }
            })
           .Then("throws with proper message", threw => threw)
           .AssertPassed();

    [Scenario("TryInterpret success case")]
    [Fact]
    public Task Interpreter_TryInterpret_Success()
        => Given("an interpreter", () =>
            Interpreter.Create<object, double>()
                .Terminal("number", token => double.Parse(token))
                .Binary("add", (l, r) => l + r)
                .Build())
           .When("trying to interpret valid expression", interp =>
            {
                var expr = NonTerminal("add", Terminal("number", "1"), Terminal("number", "2"));
                var ok = interp.TryInterpret(expr, null!, out var result);
                return (ok, result);
            })
           .Then("succeeds", r => r.ok)
           .And("returns correct result", r => r.result == 3.0)
           .AssertPassed();

    [Scenario("Interpret without context uses default")]
    [Fact]
    public Task Interpreter_InterpretWithoutContext()
        => Given("an interpreter", () =>
            Interpreter.Create<object?, double>()
                .Terminal("number", token => double.Parse(token))
                .Build())
           .When("interpreting without context", interp =>
               interp.Interpret(Terminal("number", "42")))
           .Then("succeeds with result", r => r == 42.0)
           .AssertPassed();
}

#endregion

#region Action Interpreter Tests

public sealed class ActionInterpreterTests
{
    [Scenario("ActionInterpreter Terminal Executes")]
    [Fact]
    public void ActionInterpreter_Terminal_Executes()
    {
        var log = new List<string>();
        var interpreter = ActionInterpreter.Create<object>()
            .Terminal("log", msg => log.Add(msg))
            .Build();

        interpreter.Interpret(Terminal("log", "hello"));

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("hello", log[0]);
    }

    [Scenario("ActionInterpreter Terminal WithContext")]
    [Fact]
    public void ActionInterpreter_Terminal_WithContext()
    {
        var log = new List<string>();
        var interpreter = ActionInterpreter.Create<string>()
            .Terminal("log", (msg, ctx) => log.Add($"{ctx}: {msg}"))
            .Build();

        interpreter.Interpret(Terminal("log", "hello"), "PREFIX");

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("PREFIX: hello", log[0]);
    }

    [Scenario("ActionInterpreter Sequence ExecutesInOrder")]
    [Fact]
    public void ActionInterpreter_Sequence_ExecutesInOrder()
    {
        var log = new List<string>();
        var interpreter = ActionInterpreter.Create<object>()
            .Terminal("log", msg => log.Add(msg))
            .Sequence("seq")
            .Build();

        var expr = NonTerminal("seq",
            Terminal("log", "first"),
            Terminal("log", "second"),
            Terminal("log", "third"));
        interpreter.Interpret(expr);

        ScenarioExpect.Equal(3, log.Count);
        ScenarioExpect.Equal("first", log[0]);
        ScenarioExpect.Equal("second", log[1]);
        ScenarioExpect.Equal("third", log[2]);
    }

    [Scenario("ActionInterpreter Parallel ExecutesAll")]
    [Fact]
    public void ActionInterpreter_Parallel_ExecutesAll()
    {
        var log = new List<string>();
        var interpreter = ActionInterpreter.Create<object>()
            .Terminal("log", msg => log.Add(msg))
            .Parallel("parallel")
            .Build();

        var expr = NonTerminal("parallel",
            Terminal("log", "a"),
            Terminal("log", "b"));
        interpreter.Interpret(expr);

        ScenarioExpect.Equal(2, log.Count);
        ScenarioExpect.Contains("a", log);
        ScenarioExpect.Contains("b", log);
    }

    [Scenario("ActionInterpreter Conditional ThenBranch")]
    [Fact]
    public void ActionInterpreter_Conditional_ThenBranch()
    {
        var log = new List<string>();
        var interpreter = ActionInterpreter.Create<bool>()
            .Terminal("log", msg => log.Add(msg))
            .Conditional("if", ctx => ctx)
            .Build();

        var expr = NonTerminal("if",
            Terminal("log", "condition"),
            Terminal("log", "then"),
            Terminal("log", "else"));
        interpreter.Interpret(expr, true);

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("then", log[0]);
    }

    [Scenario("ActionInterpreter Conditional ElseBranch")]
    [Fact]
    public void ActionInterpreter_Conditional_ElseBranch()
    {
        var log = new List<string>();
        var interpreter = ActionInterpreter.Create<bool>()
            .Terminal("log", msg => log.Add(msg))
            .Conditional("if", ctx => ctx)
            .Build();

        var expr = NonTerminal("if",
            Terminal("log", "condition"),
            Terminal("log", "then"),
            Terminal("log", "else"));
        interpreter.Interpret(expr, false);

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("else", log[0]);
    }

    [Scenario("ActionInterpreter Conditional NoElseBranch")]
    [Fact]
    public void ActionInterpreter_Conditional_NoElseBranch()
    {
        var log = new List<string>();
        var interpreter = ActionInterpreter.Create<bool>()
            .Terminal("log", msg => log.Add(msg))
            .Conditional("if", ctx => ctx)
            .Build();

        var expr = NonTerminal("if",
            Terminal("log", "condition"),
            Terminal("log", "then"));
        interpreter.Interpret(expr, false);

        ScenarioExpect.Empty(log);
    }

    [Scenario("ActionInterpreter Conditional ThrowsWithTooFewChildren")]
    [Fact]
    public void ActionInterpreter_Conditional_ThrowsWithTooFewChildren()
    {
        var interpreter = ActionInterpreter.Create<bool>()
            .Terminal("log", msg => { })
            .Conditional("if", ctx => ctx)
            .Build();

        var expr = NonTerminal("if", Terminal("log", "only-one"));

        var ex = ScenarioExpect.Throws<InvalidOperationException>(() => interpreter.Interpret(expr, true));
        ScenarioExpect.Contains("at least 2 children", ex.Message);
    }

    [Scenario("ActionInterpreter NonTerminal WithContext")]
    [Fact]
    public void ActionInterpreter_NonTerminal_WithContext()
    {
        var log = new List<string>();
        var interpreter = ActionInterpreter.Create<string>()
            .Terminal("log", msg => log.Add(msg))
            .NonTerminal("wrap", (ctx, children) =>
            {
                log.Add($"start-{ctx}");
                foreach (var child in children) child();
                log.Add($"end-{ctx}");
            })
            .Build();

        var expr = NonTerminal("wrap",
            Terminal("log", "inner"));
        interpreter.Interpret(expr, "CTX");

        ScenarioExpect.Equal(3, log.Count);
        ScenarioExpect.Equal("start-CTX", log[0]);
        ScenarioExpect.Equal("inner", log[1]);
        ScenarioExpect.Equal("end-CTX", log[2]);
    }

    [Scenario("ActionInterpreter NonTerminal WithoutContext")]
    [Fact]
    public void ActionInterpreter_NonTerminal_WithoutContext()
    {
        var log = new List<string>();
        var interpreter = ActionInterpreter.Create<object>()
            .Terminal("log", msg => log.Add(msg))
            .NonTerminal("wrap", children =>
            {
                log.Add("start");
                foreach (var child in children) child();
                log.Add("end");
            })
            .Build();

        var expr = NonTerminal("wrap", Terminal("log", "inner"));
        interpreter.Interpret(expr);

        ScenarioExpect.Equal(3, log.Count);
    }

    [Scenario("ActionInterpreter ThrowsForUnknownTerminal")]
    [Fact]
    public void ActionInterpreter_ThrowsForUnknownTerminal()
    {
        var interpreter = ActionInterpreter.Create<object>()
            .Terminal("log", msg => { })
            .Build();

        var ex = ScenarioExpect.Throws<InvalidOperationException>(() =>
            interpreter.Interpret(Terminal("unknown", "value")));
        ScenarioExpect.Contains("No terminal handler", ex.Message);
    }

    [Scenario("ActionInterpreter ThrowsForUnknownNonTerminal")]
    [Fact]
    public void ActionInterpreter_ThrowsForUnknownNonTerminal()
    {
        var interpreter = ActionInterpreter.Create<object>()
            .Terminal("log", msg => { })
            .Build();

        var ex = ScenarioExpect.Throws<InvalidOperationException>(() =>
            interpreter.Interpret(NonTerminal("unknown", Terminal("log", "value"))));
        ScenarioExpect.Contains("No non-terminal handler", ex.Message);
    }

    [Scenario("ActionInterpreter TryInterpret Success")]
    [Fact]
    public void ActionInterpreter_TryInterpret_Success()
    {
        var log = new List<string>();
        var interpreter = ActionInterpreter.Create<object>()
            .Terminal("log", msg => log.Add(msg))
            .Build();

        var ok = interpreter.TryInterpret(Terminal("log", "test"), null!, out var error);

        ScenarioExpect.True(ok);
        ScenarioExpect.Null(error);
        ScenarioExpect.Single(log);
    }

    [Scenario("ActionInterpreter TryInterpret Failure")]
    [Fact]
    public void ActionInterpreter_TryInterpret_Failure()
    {
        var interpreter = ActionInterpreter.Create<object>()
            .Terminal("log", msg => { })
            .Build();

        var ok = interpreter.TryInterpret(Terminal("unknown", "value"), null!, out var error);

        ScenarioExpect.False(ok);
        ScenarioExpect.NotNull(error);
        ScenarioExpect.Contains("No terminal handler", error);
    }

    [Scenario("ActionInterpreter HasTerminal")]
    [Fact]
    public void ActionInterpreter_HasTerminal()
    {
        var interpreter = ActionInterpreter.Create<object>()
            .Terminal("log", msg => { })
            .Build();

        ScenarioExpect.True(interpreter.HasTerminal("log"));
        ScenarioExpect.False(interpreter.HasTerminal("unknown"));
    }

    [Scenario("ActionInterpreter HasNonTerminal")]
    [Fact]
    public void ActionInterpreter_HasNonTerminal()
    {
        var interpreter = ActionInterpreter.Create<object>()
            .Sequence("seq")
            .Build();

        ScenarioExpect.True(interpreter.HasNonTerminal("seq"));
        ScenarioExpect.False(interpreter.HasNonTerminal("unknown"));
    }

    [Scenario("ActionInterpreter InterpretWithDefaultContext")]
    [Fact]
    public void ActionInterpreter_InterpretWithDefaultContext()
    {
        var log = new List<string>();
        var interpreter = ActionInterpreter.Create<object?>()
            .Terminal("log", msg => log.Add(msg))
            .Build();

        interpreter.Interpret(Terminal("log", "test"));

        ScenarioExpect.Single(log);
    }

    [Scenario("ActionInterpreter NestedNonTerminals")]
    [Fact]
    public void ActionInterpreter_NestedNonTerminals()
    {
        var log = new List<string>();
        var interpreter = ActionInterpreter.Create<object>()
            .Terminal("log", msg => log.Add(msg))
            .Sequence()
            .Build();

        var expr = NonTerminal("sequence",
            NonTerminal("sequence",
                Terminal("log", "a"),
                Terminal("log", "b")),
            Terminal("log", "c"));
        interpreter.Interpret(expr);

        ScenarioExpect.Equal(3, log.Count);
        ScenarioExpect.Equal("a", log[0]);
        ScenarioExpect.Equal("b", log[1]);
        ScenarioExpect.Equal("c", log[2]);
    }
}

#endregion

#region Async Interpreter Tests

public sealed class AsyncInterpreterTests
{
    [Scenario("AsyncInterpreter Terminal Evaluates")]
    [Fact]
    public async Task AsyncInterpreter_Terminal_Evaluates()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Terminal("number", token => int.Parse(token))
            .Build();

        var result = await interpreter.InterpretAsync(Terminal("number", "42"));

        ScenarioExpect.Equal(42, result);
    }

    [Scenario("AsyncInterpreter Terminal WithContext")]
    [Fact]
    public async Task AsyncInterpreter_Terminal_WithContext()
    {
        var interpreter = AsyncInterpreter.Create<Dictionary<string, int>, int>()
            .Terminal("var", (name, ctx) => ctx[name])
            .Build();

        var ctx = new Dictionary<string, int> { ["x"] = 100 };
        var result = await interpreter.InterpretAsync(Terminal("var", "x"), ctx);

        ScenarioExpect.Equal(100, result);
    }

    [Scenario("AsyncInterpreter Terminal Async")]
    [Fact]
    public async Task AsyncInterpreter_Terminal_Async()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Terminal("number", async (token, ct) =>
            {
                await Task.Delay(1, ct);
                return int.Parse(token);
            })
            .Build();

        var result = await interpreter.InterpretAsync(Terminal("number", "42"));

        ScenarioExpect.Equal(42, result);
    }

    [Scenario("AsyncInterpreter Terminal AsyncWithContext")]
    [Fact]
    public async Task AsyncInterpreter_Terminal_AsyncWithContext()
    {
        var interpreter = AsyncInterpreter.Create<int, int>()
            .Terminal("number", async (token, ctx, ct) =>
            {
                await Task.Delay(1, ct);
                return int.Parse(token) * ctx;
            })
            .Build();

        var result = await interpreter.InterpretAsync(Terminal("number", "10"), 5);

        ScenarioExpect.Equal(50, result);
    }

    [Scenario("AsyncInterpreter Binary Evaluates")]
    [Fact]
    public async Task AsyncInterpreter_Binary_Evaluates()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Terminal("number", token => int.Parse(token))
            .Binary("add", (l, r) => l + r)
            .Build();

        var expr = NonTerminal("add",
            Terminal("number", "10"),
            Terminal("number", "20"));
        var result = await interpreter.InterpretAsync(expr);

        ScenarioExpect.Equal(30, result);
    }

    [Scenario("AsyncInterpreter Binary ThrowsWithWrongOperandCount")]
    [Fact]
    public async Task AsyncInterpreter_Binary_ThrowsWithWrongOperandCount()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Terminal("number", token => int.Parse(token))
            .Binary("add", (l, r) => l + r)
            .Build();

        var expr = NonTerminal("add", Terminal("number", "10"));

        var ex = await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() =>
            interpreter.InterpretAsync(expr).AsTask());
        ScenarioExpect.Contains("exactly 2 operands", ex.Message);
    }

    [Scenario("AsyncInterpreter Unary Evaluates")]
    [Fact]
    public async Task AsyncInterpreter_Unary_Evaluates()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Terminal("number", token => int.Parse(token))
            .Unary("neg", v => -v)
            .Build();

        var expr = NonTerminal("neg", Terminal("number", "10"));
        var result = await interpreter.InterpretAsync(expr);

        ScenarioExpect.Equal(-10, result);
    }

    [Scenario("AsyncInterpreter Unary ThrowsWithWrongOperandCount")]
    [Fact]
    public async Task AsyncInterpreter_Unary_ThrowsWithWrongOperandCount()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Terminal("number", token => int.Parse(token))
            .Unary("neg", v => -v)
            .Build();

        var expr = NonTerminal("neg",
            Terminal("number", "10"),
            Terminal("number", "20"));

        var ex = await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() =>
            interpreter.InterpretAsync(expr).AsTask());
        ScenarioExpect.Contains("exactly 1 operand", ex.Message);
    }

    [Scenario("AsyncInterpreter NonTerminal WithContext")]
    [Fact]
    public async Task AsyncInterpreter_NonTerminal_WithContext()
    {
        var interpreter = AsyncInterpreter.Create<int, int>()
            .Terminal("number", token => int.Parse(token))
            .NonTerminal("scale", (args, ctx) => args.Sum() * ctx)
            .Build();

        var expr = NonTerminal("scale",
            Terminal("number", "1"),
            Terminal("number", "2"),
            Terminal("number", "3"));
        var result = await interpreter.InterpretAsync(expr, 10);

        ScenarioExpect.Equal(60, result); // (1+2+3) * 10
    }

    [Scenario("AsyncInterpreter NonTerminal WithoutContext")]
    [Fact]
    public async Task AsyncInterpreter_NonTerminal_WithoutContext()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Terminal("number", token => int.Parse(token))
            .NonTerminal("sum", args => args.Sum())
            .Build();

        var expr = NonTerminal("sum",
            Terminal("number", "1"),
            Terminal("number", "2"),
            Terminal("number", "3"));
        var result = await interpreter.InterpretAsync(expr);

        ScenarioExpect.Equal(6, result);
    }

    [Scenario("AsyncInterpreter NonTerminal Async")]
    [Fact]
    public async Task AsyncInterpreter_NonTerminal_Async()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Terminal("number", token => int.Parse(token))
            .NonTerminal("sum", async (args, ct) =>
            {
                await Task.Delay(1, ct);
                return args.Sum();
            })
            .Build();

        var expr = NonTerminal("sum", Terminal("number", "10"), Terminal("number", "20"));
        var result = await interpreter.InterpretAsync(expr);

        ScenarioExpect.Equal(30, result);
    }

    [Scenario("AsyncInterpreter TryInterpret Success")]
    [Fact]
    public async Task AsyncInterpreter_TryInterpret_Success()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Terminal("number", token => int.Parse(token))
            .Build();

        var (success, result) = await interpreter.TryInterpretAsync(Terminal("number", "42"), null!);

        ScenarioExpect.True(success);
        ScenarioExpect.Equal(42, result);
    }

    [Scenario("AsyncInterpreter TryInterpret Failure")]
    [Fact]
    public async Task AsyncInterpreter_TryInterpret_Failure()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Terminal("number", token => int.Parse(token))
            .Build();

        var (success, result) = await interpreter.TryInterpretAsync(Terminal("unknown", "value"), null!);

        ScenarioExpect.False(success);
        ScenarioExpect.Equal(default, result);
    }

    [Scenario("AsyncInterpreter HasTerminal")]
    [Fact]
    public async Task AsyncInterpreter_HasTerminal()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Terminal("number", token => int.Parse(token))
            .Build();

        ScenarioExpect.True(interpreter.HasTerminal("number"));
        ScenarioExpect.False(interpreter.HasTerminal("unknown"));
    }

    [Scenario("AsyncInterpreter HasNonTerminal")]
    [Fact]
    public async Task AsyncInterpreter_HasNonTerminal()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Binary("add", (l, r) => l + r)
            .Build();

        ScenarioExpect.True(interpreter.HasNonTerminal("add"));
        ScenarioExpect.False(interpreter.HasNonTerminal("unknown"));
    }

    [Scenario("AsyncInterpreter ThrowsForUnknownTerminal")]
    [Fact]
    public async Task AsyncInterpreter_ThrowsForUnknownTerminal()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Terminal("number", token => int.Parse(token))
            .Build();

        var ex = await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() =>
            interpreter.InterpretAsync(Terminal("unknown", "value")).AsTask());
        ScenarioExpect.Contains("No terminal handler", ex.Message);
    }

    [Scenario("AsyncInterpreter ThrowsForUnknownNonTerminal")]
    [Fact]
    public async Task AsyncInterpreter_ThrowsForUnknownNonTerminal()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Terminal("number", token => int.Parse(token))
            .Build();

        var ex = await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() =>
            interpreter.InterpretAsync(NonTerminal("unknown", Terminal("number", "1"))).AsTask());
        ScenarioExpect.Contains("No non-terminal handler", ex.Message);
    }

    [Scenario("AsyncInterpreter NestedExpressions")]
    [Fact]
    public async Task AsyncInterpreter_NestedExpressions()
    {
        var interpreter = AsyncInterpreter.Create<object, int>()
            .Terminal("number", token => int.Parse(token))
            .Binary("add", (l, r) => l + r)
            .Binary("mul", (l, r) => l * r)
            .Build();

        // (1 + 2) * (3 + 4) = 3 * 7 = 21
        var expr = NonTerminal("mul",
            NonTerminal("add", Terminal("number", "1"), Terminal("number", "2")),
            NonTerminal("add", Terminal("number", "3"), Terminal("number", "4")));
        var result = await interpreter.InterpretAsync(expr);

        ScenarioExpect.Equal(21, result);
    }
}

#endregion

#region Async Action Interpreter Tests

public sealed class AsyncActionInterpreterTests
{
    [Scenario("AsyncActionInterpreter Terminal Executes")]
    [Fact]
    public async Task AsyncActionInterpreter_Terminal_Executes()
    {
        var log = new List<string>();
        var interpreter = AsyncActionInterpreter.Create<object>()
            .Terminal("log", msg => log.Add(msg))
            .Build();

        await interpreter.InterpretAsync(Terminal("log", "hello"));

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("hello", log[0]);
    }

    [Scenario("AsyncActionInterpreter Terminal WithContext")]
    [Fact]
    public async Task AsyncActionInterpreter_Terminal_WithContext()
    {
        var log = new List<string>();
        var interpreter = AsyncActionInterpreter.Create<string>()
            .Terminal("log", (msg, ctx) => log.Add($"{ctx}: {msg}"))
            .Build();

        await interpreter.InterpretAsync(Terminal("log", "hello"), "PREFIX");

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("PREFIX: hello", log[0]);
    }

    [Scenario("AsyncActionInterpreter Terminal Async")]
    [Fact]
    public async Task AsyncActionInterpreter_Terminal_Async()
    {
        var log = new List<string>();
        var interpreter = AsyncActionInterpreter.Create<object>()
            .Terminal("log", async (msg, ct) =>
            {
                await Task.Delay(1, ct);
                log.Add(msg);
            })
            .Build();

        await interpreter.InterpretAsync(Terminal("log", "hello"));

        ScenarioExpect.Single(log);
    }

    [Scenario("AsyncActionInterpreter Terminal AsyncWithContext")]
    [Fact]
    public async Task AsyncActionInterpreter_Terminal_AsyncWithContext()
    {
        var log = new List<string>();
        var interpreter = AsyncActionInterpreter.Create<string>()
            .Terminal("log", async (msg, ctx, ct) =>
            {
                await Task.Delay(1, ct);
                log.Add($"{ctx}: {msg}");
            })
            .Build();

        await interpreter.InterpretAsync(Terminal("log", "hello"), "PREFIX");

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("PREFIX: hello", log[0]);
    }

    [Scenario("AsyncActionInterpreter Sequence ExecutesInOrder")]
    [Fact]
    public async Task AsyncActionInterpreter_Sequence_ExecutesInOrder()
    {
        var log = new List<string>();
        var interpreter = AsyncActionInterpreter.Create<object>()
            .Terminal("log", msg => log.Add(msg))
            .Sequence("seq")
            .Build();

        var expr = NonTerminal("seq",
            Terminal("log", "first"),
            Terminal("log", "second"),
            Terminal("log", "third"));
        await interpreter.InterpretAsync(expr);

        ScenarioExpect.Equal(3, log.Count);
        ScenarioExpect.Equal("first", log[0]);
        ScenarioExpect.Equal("second", log[1]);
        ScenarioExpect.Equal("third", log[2]);
    }

    [Scenario("AsyncActionInterpreter Parallel ExecutesAll")]
    [Fact]
    public async Task AsyncActionInterpreter_Parallel_ExecutesAll()
    {
        var log = new List<string>();
        var interpreter = AsyncActionInterpreter.Create<object>()
            .Terminal("log", msg => log.Add(msg))
            .Parallel("parallel")
            .Build();

        var expr = NonTerminal("parallel",
            Terminal("log", "a"),
            Terminal("log", "b"));
        await interpreter.InterpretAsync(expr);

        ScenarioExpect.Equal(2, log.Count);
        ScenarioExpect.Contains("a", log);
        ScenarioExpect.Contains("b", log);
    }

    [Scenario("AsyncActionInterpreter Conditional ThenBranch")]
    [Fact]
    public async Task AsyncActionInterpreter_Conditional_ThenBranch()
    {
        var log = new List<string>();
        var interpreter = AsyncActionInterpreter.Create<bool>()
            .Terminal("log", msg => log.Add(msg))
            .Conditional("if", ctx => ctx)
            .Build();

        var expr = NonTerminal("if",
            Terminal("log", "condition"),
            Terminal("log", "then"),
            Terminal("log", "else"));
        await interpreter.InterpretAsync(expr, true);

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("then", log[0]);
    }

    [Scenario("AsyncActionInterpreter Conditional ElseBranch")]
    [Fact]
    public async Task AsyncActionInterpreter_Conditional_ElseBranch()
    {
        var log = new List<string>();
        var interpreter = AsyncActionInterpreter.Create<bool>()
            .Terminal("log", msg => log.Add(msg))
            .Conditional("if", ctx => ctx)
            .Build();

        var expr = NonTerminal("if",
            Terminal("log", "condition"),
            Terminal("log", "then"),
            Terminal("log", "else"));
        await interpreter.InterpretAsync(expr, false);

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("else", log[0]);
    }

    [Scenario("AsyncActionInterpreter Conditional Async")]
    [Fact]
    public async Task AsyncActionInterpreter_Conditional_Async()
    {
        var log = new List<string>();
        var interpreter = AsyncActionInterpreter.Create<bool>()
            .Terminal("log", msg => log.Add(msg))
            .Conditional("if", async (ctx, ct) =>
            {
                await Task.Delay(1, ct);
                return ctx;
            })
            .Build();

        var expr = NonTerminal("if",
            Terminal("log", "condition"),
            Terminal("log", "then"),
            Terminal("log", "else"));
        await interpreter.InterpretAsync(expr, true);

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("then", log[0]);
    }

    [Scenario("AsyncActionInterpreter Conditional ThrowsWithTooFewChildren")]
    [Fact]
    public async Task AsyncActionInterpreter_Conditional_ThrowsWithTooFewChildren()
    {
        var interpreter = AsyncActionInterpreter.Create<bool>()
            .Terminal("log", msg => { })
            .Conditional("if", ctx => ctx)
            .Build();

        var expr = NonTerminal("if", Terminal("log", "only-one"));

        var ex = await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() =>
            interpreter.InterpretAsync(expr, true).AsTask());
        ScenarioExpect.Contains("at least 2 children", ex.Message);
    }

    [Scenario("AsyncActionInterpreter NonTerminal WithContext")]
    [Fact]
    public async Task AsyncActionInterpreter_NonTerminal_WithContext()
    {
        var log = new List<string>();
        var interpreter = AsyncActionInterpreter.Create<string>()
            .Terminal("log", msg => log.Add(msg))
            .NonTerminal("wrap", async (ctx, children, ct) =>
            {
                log.Add($"start-{ctx}");
                foreach (var child in children) await child();
                log.Add($"end-{ctx}");
            })
            .Build();

        var expr = NonTerminal("wrap", Terminal("log", "inner"));
        await interpreter.InterpretAsync(expr, "CTX");

        ScenarioExpect.Equal(3, log.Count);
        ScenarioExpect.Equal("start-CTX", log[0]);
        ScenarioExpect.Equal("inner", log[1]);
        ScenarioExpect.Equal("end-CTX", log[2]);
    }

    [Scenario("AsyncActionInterpreter NonTerminal WithoutContext")]
    [Fact]
    public async Task AsyncActionInterpreter_NonTerminal_WithoutContext()
    {
        var log = new List<string>();
        var interpreter = AsyncActionInterpreter.Create<object>()
            .Terminal("log", msg => log.Add(msg))
            .NonTerminal("wrap", async (children, ct) =>
            {
                log.Add("start");
                foreach (var child in children) await child();
                log.Add("end");
            })
            .Build();

        var expr = NonTerminal("wrap", Terminal("log", "inner"));
        await interpreter.InterpretAsync(expr);

        ScenarioExpect.Equal(3, log.Count);
    }

    [Scenario("AsyncActionInterpreter TryInterpret Success")]
    [Fact]
    public async Task AsyncActionInterpreter_TryInterpret_Success()
    {
        var log = new List<string>();
        var interpreter = AsyncActionInterpreter.Create<object>()
            .Terminal("log", msg => log.Add(msg))
            .Build();

        var (success, error) = await interpreter.TryInterpretAsync(Terminal("log", "test"), null!);

        ScenarioExpect.True(success);
        ScenarioExpect.Null(error);
        ScenarioExpect.Single(log);
    }

    [Scenario("AsyncActionInterpreter TryInterpret Failure")]
    [Fact]
    public async Task AsyncActionInterpreter_TryInterpret_Failure()
    {
        var interpreter = AsyncActionInterpreter.Create<object>()
            .Terminal("log", msg => { })
            .Build();

        var (success, error) = await interpreter.TryInterpretAsync(Terminal("unknown", "value"), null!);

        ScenarioExpect.False(success);
        ScenarioExpect.NotNull(error);
        ScenarioExpect.Contains("No terminal handler", error);
    }

    [Scenario("AsyncActionInterpreter HasTerminal")]
    [Fact]
    public async Task AsyncActionInterpreter_HasTerminal()
    {
        var interpreter = AsyncActionInterpreter.Create<object>()
            .Terminal("log", msg => { })
            .Build();

        ScenarioExpect.True(interpreter.HasTerminal("log"));
        ScenarioExpect.False(interpreter.HasTerminal("unknown"));
    }

    [Scenario("AsyncActionInterpreter HasNonTerminal")]
    [Fact]
    public async Task AsyncActionInterpreter_HasNonTerminal()
    {
        var interpreter = AsyncActionInterpreter.Create<object>()
            .Sequence("seq")
            .Build();

        ScenarioExpect.True(interpreter.HasNonTerminal("seq"));
        ScenarioExpect.False(interpreter.HasNonTerminal("unknown"));
    }

    [Scenario("AsyncActionInterpreter ThrowsForUnknownTerminal")]
    [Fact]
    public async Task AsyncActionInterpreter_ThrowsForUnknownTerminal()
    {
        var interpreter = AsyncActionInterpreter.Create<object>()
            .Terminal("log", msg => { })
            .Build();

        var ex = await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() =>
            interpreter.InterpretAsync(Terminal("unknown", "value")).AsTask());
        ScenarioExpect.Contains("No terminal handler", ex.Message);
    }

    [Scenario("AsyncActionInterpreter ThrowsForUnknownNonTerminal")]
    [Fact]
    public async Task AsyncActionInterpreter_ThrowsForUnknownNonTerminal()
    {
        var interpreter = AsyncActionInterpreter.Create<object>()
            .Terminal("log", msg => { })
            .Build();

        var ex = await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() =>
            interpreter.InterpretAsync(NonTerminal("unknown", Terminal("log", "value"))).AsTask());
        ScenarioExpect.Contains("No non-terminal handler", ex.Message);
    }

    [Scenario("AsyncActionInterpreter InterpretWithoutContext")]
    [Fact]
    public async Task AsyncActionInterpreter_InterpretWithoutContext()
    {
        var log = new List<string>();
        var interpreter = AsyncActionInterpreter.Create<object?>()
            .Terminal("log", msg => log.Add(msg))
            .Build();

        await interpreter.InterpretAsync(Terminal("log", "test"));

        ScenarioExpect.Single(log);
    }

    [Scenario("AsyncActionInterpreter ThrowsForUnknownExpressionType")]
    [Fact]
    public async Task AsyncActionInterpreter_ThrowsForUnknownExpressionType()
    {
        var interpreter = AsyncActionInterpreter.Create<object>()
            .Terminal("log", msg => { })
            .Build();

        // Create a custom expression type
        var customExpr = new CustomExpression();

        var ex = await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() =>
            interpreter.InterpretAsync(customExpr).AsTask());
        ScenarioExpect.Contains("Unknown expression type", ex.Message);
    }

    private class CustomExpression : IExpression
    {
        public string Type => "custom";
    }
}

#endregion

#region Expression Extensions Tests

public sealed class ExpressionExtensionsTests
{
    [Scenario("Terminal CreatesTerminalExpression")]
    [Fact]
    public void Terminal_CreatesTerminalExpression()
    {
        var expr = Terminal("number", "42");

        ScenarioExpect.IsType<TerminalExpression>(expr);
        var terminal = (TerminalExpression)expr;
        ScenarioExpect.Equal("number", terminal.Type);
        ScenarioExpect.Equal("42", terminal.Value);
    }

    [Scenario("NonTerminal CreatesNonTerminalExpression")]
    [Fact]
    public void NonTerminal_CreatesNonTerminalExpression()
    {
        var expr = NonTerminal("add",
            Terminal("number", "1"),
            Terminal("number", "2"));

        ScenarioExpect.IsType<NonTerminalExpression>(expr);
        var nonTerminal = (NonTerminalExpression)expr;
        ScenarioExpect.Equal("add", nonTerminal.Type);
        ScenarioExpect.Equal(2, nonTerminal.Children.Length);
    }

    [Scenario("Number CreatesNumberTerminal")]
    [Fact]
    public void Number_CreatesNumberTerminal()
    {
        var expr = Number(42.5);

        ScenarioExpect.IsType<TerminalExpression>(expr);
        var terminal = (TerminalExpression)expr;
        ScenarioExpect.Equal("number", terminal.Type);
        ScenarioExpect.Equal("42.5", terminal.Value);
    }

    [Scenario("String CreatesStringTerminal")]
    [Fact]
    public void String_CreatesStringTerminal()
    {
        var expr = ExpressionExtensions.String("hello");

        ScenarioExpect.IsType<TerminalExpression>(expr);
        var terminal = (TerminalExpression)expr;
        ScenarioExpect.Equal("string", terminal.Type);
        ScenarioExpect.Equal("hello", terminal.Value);
    }

    [Scenario("Identifier CreatesIdentifierTerminal")]
    [Fact]
    public void Identifier_CreatesIdentifierTerminal()
    {
        var expr = Identifier("x");

        ScenarioExpect.IsType<TerminalExpression>(expr);
        var terminal = (TerminalExpression)expr;
        ScenarioExpect.Equal("identifier", terminal.Type);
        ScenarioExpect.Equal("x", terminal.Value);
    }

    [Scenario("Boolean CreatesBooleanTerminal")]
    [Fact]
    public void Boolean_CreatesBooleanTerminal()
    {
        var trueExpr = Boolean(true);
        var falseExpr = Boolean(false);

        ScenarioExpect.IsType<TerminalExpression>(trueExpr);
        ScenarioExpect.IsType<TerminalExpression>(falseExpr);

        var trueTerminal = (TerminalExpression)trueExpr;
        var falseTerminal = (TerminalExpression)falseExpr;

        ScenarioExpect.Equal("boolean", trueTerminal.Type);
        ScenarioExpect.Equal("true", trueTerminal.Value);
        ScenarioExpect.Equal("boolean", falseTerminal.Type);
        ScenarioExpect.Equal("false", falseTerminal.Value);
    }

    [Scenario("TerminalExpression Properties")]
    [Fact]
    public void TerminalExpression_Properties()
    {
        var expr = new TerminalExpression("type", "value");

        ScenarioExpect.Equal("type", expr.Type);
        ScenarioExpect.Equal("value", expr.Value);
    }

    [Scenario("NonTerminalExpression Properties")]
    [Fact]
    public void NonTerminalExpression_Properties()
    {
        var child1 = Terminal("a", "1");
        var child2 = Terminal("b", "2");
        var expr = new NonTerminalExpression("type", child1, child2);

        ScenarioExpect.Equal("type", expr.Type);
        ScenarioExpect.Equal(2, expr.Children.Length);
        ScenarioExpect.Same(child1, expr.Children[0]);
        ScenarioExpect.Same(child2, expr.Children[1]);
    }

    [Scenario("NonTerminalExpression EmptyChildren")]
    [Fact]
    public void NonTerminalExpression_EmptyChildren()
    {
        var expr = new NonTerminalExpression("empty");

        ScenarioExpect.Equal("empty", expr.Type);
        ScenarioExpect.Empty(expr.Children);
    }

    [Scenario("InterpretWithExtensions ArithmeticExample")]
    [Fact]
    public void InterpretWithExtensions_ArithmeticExample()
    {
        var interpreter = Interpreter.Create<object, double>()
            .Terminal("number", token => double.Parse(token))
            .Binary("add", (l, r) => l + r)
            .Binary("mul", (l, r) => l * r)
            .Build();

        // Use extension methods to build expression
        var expr = NonTerminal("add",
            Number(10),
            NonTerminal("mul",
                Number(2),
                Number(3)));

        var result = interpreter.Interpret(expr);

        ScenarioExpect.Equal(16, result); // 10 + (2 * 3)
    }

    [Scenario("InterpretWithExtensions BooleanExample")]
    [Fact]
    public void InterpretWithExtensions_BooleanExample()
    {
        var interpreter = Interpreter.Create<object, bool>()
            .Terminal("boolean", token => bool.Parse(token))
            .Binary("and", (l, r) => l && r)
            .Unary("not", v => !v)
            .Build();

        var expr = NonTerminal("and",
            Boolean(true),
            NonTerminal("not", Boolean(false)));

        var result = interpreter.Interpret(expr);

        ScenarioExpect.True(result);
    }
}

#endregion
