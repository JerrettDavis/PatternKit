using PatternKit.Behavioral.Interpreter;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;
using static PatternKit.Behavioral.Interpreter.ExpressionExtensions;

namespace PatternKit.Tests.Behavioral;

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
}
