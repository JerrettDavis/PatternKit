using BenchmarkDotNet.Attributes;
using PatternKit.Behavioral.Interpreter;
using PatternKit.Generators.Interpreter;

namespace PatternKit.Benchmarks.Behavioral;

[BenchmarkCategory("Behavioral", "GoF", "Interpreter")]
public class InterpreterBenchmarks
{
    private static readonly PricingContext Context = new(20m);
    private static readonly IExpression Expression = new NonTerminalExpression(
        "add",
        new TerminalExpression("cart", ""),
        new NonTerminalExpression(
            "mul",
            new TerminalExpression("number", "2"),
            new TerminalExpression("number", "3")));

    [Benchmark(Baseline = true, Description = "Fluent: create interpreter")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Interpreter<PricingContext, decimal> Fluent_CreateInterpreter()
        => Interpreter.Create<PricingContext, decimal>()
            .Terminal("number", static token => decimal.Parse(token))
            .Terminal("cart", static (_, context) => context.CartTotal)
            .Binary("add", static (left, right) => left + right)
            .Binary("mul", static (left, right) => left * right)
            .Build();

    [Benchmark(Description = "Generated: create interpreter")]
    [BenchmarkCategory("Generated", "Construction")]
    public Interpreter<PricingContext, decimal> Generated_CreateInterpreter()
        => GeneratedPricingRules.BuildPricingRules();

    [Benchmark(Description = "Fluent: interpret pricing rule")]
    [BenchmarkCategory("Fluent", "Execution")]
    public decimal Fluent_InterpretRule()
        => Fluent_CreateInterpreter().Interpret(Expression, Context);

    [Benchmark(Description = "Generated: interpret pricing rule")]
    [BenchmarkCategory("Generated", "Execution")]
    public decimal Generated_InterpretRule()
        => GeneratedPricingRules.BuildPricingRules().Interpret(Expression, Context);
}

public sealed record PricingContext(decimal CartTotal);

[GenerateInterpreter(typeof(PricingContext), typeof(decimal), FactoryMethodName = "BuildPricingRules")]
public static partial class GeneratedPricingRules
{
    [InterpreterTerminal("number")]
    private static decimal Number(string token) => decimal.Parse(token);

    [InterpreterTerminal("cart")]
    private static decimal Cart(string token, PricingContext context) => context.CartTotal;

    [InterpreterNonTerminal("add")]
    private static decimal Add(decimal[] args) => args[0] + args[1];

    [InterpreterNonTerminal("mul")]
    private static decimal Multiply(decimal[] args) => args[0] * args[1];
}
