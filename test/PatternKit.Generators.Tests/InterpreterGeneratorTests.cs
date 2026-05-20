using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Interpreter;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class InterpreterGeneratorTests
{
    [Scenario("Generates interpreter factory from terminal and non-terminal rules")]
    [Fact]
    public void GeneratesInterpreterFactoryFromTerminalAndNonTerminalRules()
    {
        var source = """
            using PatternKit.Generators.Interpreter;

            namespace Demo;

            public sealed class PricingContext
            {
                public decimal CartTotal { get; set; }
            }

            [GenerateInterpreter(typeof(PricingContext), typeof(decimal), FactoryMethodName = "BuildPricingRules")]
            public static partial class PricingRules
            {
                [InterpreterTerminal("number")]
                private static decimal Number(string token) => decimal.Parse(token);

                [InterpreterTerminal("cart")]
                private static decimal Cart(string token, PricingContext context) => context.CartTotal;

                [InterpreterNonTerminal("add")]
                private static decimal Add(decimal[] args) => args[0] + args[1];

                [InterpreterNonTerminal("mul")]
                private static decimal Multiply(decimal[] args, PricingContext context) => args[0] * args[1];
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesInterpreterFactoryFromTerminalAndNonTerminalRules));
        var gen = new InterpreterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("PricingRules.Interpreter.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("BuildPricingRules()", text);
        ScenarioExpect.Contains("builder.Terminal(\"cart\", static (token, context) => Cart(token, context));", text);
        ScenarioExpect.Contains("builder.Terminal(\"number\", static (token, context) => Number(token));", text);
        ScenarioExpect.Contains("builder.NonTerminal(\"add\", static (args, context) => Add(args));", text);
        ScenarioExpect.Contains("builder.NonTerminal(\"mul\", static (args, context) => Multiply(args, context));", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial interpreter host")]
    [Fact]
    public void ReportsDiagnosticForNonPartialInterpreterHost()
    {
        var source = """
            using PatternKit.Generators.Interpreter;

            namespace Demo;

            [GenerateInterpreter(typeof(object), typeof(decimal))]
            public static class PricingRules;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForNonPartialInterpreterHost));

        ScenarioExpect.Equal("PKINT001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for interpreter without rules")]
    [Fact]
    public void ReportsDiagnosticForInterpreterWithoutRules()
    {
        var source = """
            using PatternKit.Generators.Interpreter;

            namespace Demo;

            [GenerateInterpreter(typeof(object), typeof(decimal))]
            public static partial class PricingRules;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInterpreterWithoutRules));

        ScenarioExpect.Equal("PKINT002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid interpreter rule signature")]
    [Fact]
    public void ReportsDiagnosticForInvalidInterpreterRuleSignature()
    {
        var source = """
            using PatternKit.Generators.Interpreter;

            namespace Demo;

            [GenerateInterpreter(typeof(object), typeof(decimal))]
            public static partial class PricingRules
            {
                [InterpreterTerminal("number")]
                private static string Number(string token) => token;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidInterpreterRuleSignature));

        ScenarioExpect.Equal("PKINT003", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for duplicate interpreter rules")]
    [Fact]
    public void ReportsDiagnosticForDuplicateInterpreterRules()
    {
        var source = """
            using PatternKit.Generators.Interpreter;

            namespace Demo;

            [GenerateInterpreter(typeof(object), typeof(decimal))]
            public static partial class PricingRules
            {
                [InterpreterTerminal("number")]
                private static decimal Number(string token) => 1m;

                [InterpreterTerminal("number")]
                private static decimal AlsoNumber(string token) => 2m;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForDuplicateInterpreterRules));

        ScenarioExpect.Equal("PKINT004", diagnostic.Id);
    }

    [Scenario("Generates interpreter for global struct host")]
    [Fact]
    public void GeneratesInterpreterForGlobalStructHost()
    {
        var source = """
            using PatternKit.Generators.Interpreter;

            [GenerateInterpreter(typeof(object), typeof(int))]
            public partial struct RuleHost
            {
                [InterpreterTerminal("number")]
                private static int Number(string token) => int.Parse(token);
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesInterpreterForGlobalStructHost));
        var gen = new InterpreterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("partial struct RuleHost", text);
        ScenarioExpect.DoesNotContain("namespace ", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Escapes generated rule names")]
    [Fact]
    public void EscapesGeneratedRuleNames()
    {
        var source = """
            using PatternKit.Generators.Interpreter;

            namespace Demo;

            [GenerateInterpreter(typeof(object), typeof(string))]
            public static partial class RuleHost
            {
                [InterpreterTerminal("quote\"rule")]
                private static string Quote(string token) => token;
            }
            """;

        var comp = CreateCompilation(source, nameof(EscapesGeneratedRuleNames));
        var gen = new InterpreterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Contains("quote\\\"rule", generated.SourceText.ToString());

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(PatternKit.Behavioral.Interpreter.Interpreter<,>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(InterpreterGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new InterpreterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
    }
}
