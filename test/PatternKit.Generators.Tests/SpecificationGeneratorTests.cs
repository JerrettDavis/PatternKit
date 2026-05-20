using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Specification;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class SpecificationGeneratorTests
{
    [Scenario("Generates specification registry from rule methods")]
    [Fact]
    public void GeneratesSpecificationRegistryFromRuleMethods()
    {
        var source = """
            using PatternKit.Generators.Specification;

            namespace Demo;

            public sealed record LoanApplication(decimal Income, int CreditScore);

            [GenerateSpecificationRegistry(typeof(LoanApplication), FactoryMethodName = "Build")]
            public static partial class LoanRules
            {
                [SpecificationRule("high-income")]
                private static bool HighIncome(LoanApplication application) => application.Income >= 100000m;

                [SpecificationRule("prime-credit")]
                private static bool PrimeCredit(LoanApplication application) => application.CreditScore >= 720;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesSpecificationRegistryFromRuleMethods));
        var gen = new SpecificationGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("LoanRules.SpecificationRegistry.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("builder.Add(\"high-income\", static candidate => HighIncome(candidate));", text);
        ScenarioExpect.Contains("builder.Add(\"prime-credit\", static candidate => PrimeCredit(candidate));", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial specification host")]
    [Fact]
    public void ReportsDiagnosticForNonPartialSpecificationHost()
    {
        var source = """
            using PatternKit.Generators.Specification;

            namespace Demo;

            [GenerateSpecificationRegistry(typeof(object))]
            public static class Rules;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForNonPartialSpecificationHost));

        ScenarioExpect.Equal("PKSPEC001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for specification host without rules")]
    [Fact]
    public void ReportsDiagnosticForSpecificationHostWithoutRules()
    {
        var source = """
            using PatternKit.Generators.Specification;

            namespace Demo;

            [GenerateSpecificationRegistry(typeof(object))]
            public static partial class Rules;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForSpecificationHostWithoutRules));

        ScenarioExpect.Equal("PKSPEC002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid specification rule signature")]
    [Fact]
    public void ReportsDiagnosticForInvalidSpecificationRuleSignature()
    {
        var source = """
            using PatternKit.Generators.Specification;

            namespace Demo;

            [GenerateSpecificationRegistry(typeof(object))]
            public static partial class Rules
            {
                [SpecificationRule("broken")]
                private static string Broken(object candidate) => "no";
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidSpecificationRuleSignature));

        ScenarioExpect.Equal("PKSPEC003", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for duplicate specification rule names")]
    [Fact]
    public void ReportsDiagnosticForDuplicateSpecificationRuleNames()
    {
        var source = """
            using PatternKit.Generators.Specification;

            namespace Demo;

            [GenerateSpecificationRegistry(typeof(object))]
            public static partial class Rules
            {
                [SpecificationRule("approved")]
                private static bool Approved(object candidate) => true;

                [SpecificationRule("approved")]
                private static bool AlsoApproved(object candidate) => false;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForDuplicateSpecificationRuleNames));

        ScenarioExpect.Equal("PKSPEC004", diagnostic.Id);
    }

    [Scenario("Generates specification registry for global struct host and escaped names")]
    [Fact]
    public void GeneratesSpecificationRegistryForGlobalStructHostAndEscapedNames()
    {
        var source = """
            using PatternKit.Generators.Specification;

            [GenerateSpecificationRegistry(typeof(string))]
            public partial struct Rules
            {
                [SpecificationRule("quote\"rule")]
                private static bool Quoted(string candidate) => candidate.Length > 0;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesSpecificationRegistryForGlobalStructHostAndEscapedNames));
        var gen = new SpecificationGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("partial struct Rules", text);
        ScenarioExpect.DoesNotContain("namespace ", text);
        ScenarioExpect.Contains("quote\\\"rule", text);

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
                MetadataReference.CreateFromFile(typeof(PatternKit.Application.Specification.SpecificationRegistry<>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(SpecificationGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new SpecificationGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
    }
}
