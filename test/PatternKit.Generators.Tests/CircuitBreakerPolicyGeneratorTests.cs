using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.CircuitBreaker;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class CircuitBreakerPolicyGeneratorTests
{
    [Scenario("Generates circuit breaker policy factory")]
    [Fact]
    public void GeneratesCircuitBreakerPolicyFactory()
    {
        var source = """
            using System;
            using PatternKit.Generators.CircuitBreaker;

            namespace Demo;

            [GenerateCircuitBreakerPolicy(typeof(string), FactoryMethodName = "Build", PolicyName = "fulfillment", FailureThreshold = 5, BreakDurationMilliseconds = 250)]
            public static partial class FulfillmentCircuitBreaker
            {
                [CircuitBreakerResultPredicate]
                private static bool HandleResult(string result) => result == "down";

                [CircuitBreakerExceptionPredicate]
                private static bool HandleException(Exception exception) => exception is TimeoutException;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesCircuitBreakerPolicyFactory));
        var gen = new CircuitBreakerPolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("FulfillmentCircuitBreaker.CircuitBreakerPolicy.g.cs", generated.HintName);
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("CircuitBreakerPolicy<string>.Create(\"fulfillment\")", text);
        ScenarioExpect.Contains(".WithFailureThreshold(5)", text);
        ScenarioExpect.Contains(".WithBreakDuration(global::System.TimeSpan.FromMilliseconds(250))", text);
        ScenarioExpect.Contains("builder.HandleResult(static result => HandleResult(result));", text);
        ScenarioExpect.Contains("builder.HandleException(static exception => HandleException(exception));", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial circuit breaker host")]
    [Fact]
    public void ReportsDiagnosticForNonPartialCircuitBreakerHost()
    {
        var source = """
            using PatternKit.Generators.CircuitBreaker;

            namespace Demo;

            [GenerateCircuitBreakerPolicy(typeof(string))]
            public static class CircuitBreakerHost;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForNonPartialCircuitBreakerHost));

        ScenarioExpect.Equal("PKCB001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid circuit breaker configuration")]
    [Fact]
    public void ReportsDiagnosticForInvalidCircuitBreakerConfiguration()
    {
        var source = """
            using PatternKit.Generators.CircuitBreaker;

            namespace Demo;

            [GenerateCircuitBreakerPolicy(typeof(string), FailureThreshold = 0)]
            public static partial class CircuitBreakerHost;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidCircuitBreakerConfiguration));

        ScenarioExpect.Equal("PKCB002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid circuit breaker predicate")]
    [Fact]
    public void ReportsDiagnosticForInvalidCircuitBreakerPredicate()
    {
        var source = """
            using PatternKit.Generators.CircuitBreaker;

            namespace Demo;

            [GenerateCircuitBreakerPolicy(typeof(string))]
            public static partial class CircuitBreakerHost
            {
                [CircuitBreakerResultPredicate]
                private static string HandleResult(string result) => result;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidCircuitBreakerPredicate));

        ScenarioExpect.Equal("PKCB003", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for duplicate circuit breaker predicates")]
    [Fact]
    public void ReportsDiagnosticForDuplicateCircuitBreakerPredicates()
    {
        var source = """
            using PatternKit.Generators.CircuitBreaker;

            namespace Demo;

            [GenerateCircuitBreakerPolicy(typeof(string))]
            public static partial class CircuitBreakerHost
            {
                [CircuitBreakerResultPredicate]
                private static bool First(string result) => false;

                [CircuitBreakerResultPredicate]
                private static bool Second(string result) => true;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForDuplicateCircuitBreakerPredicates));

        ScenarioExpect.Equal("PKCB004", diagnostic.Id);
    }

    [Scenario("Generates circuit breaker policy factory for global struct host without predicates")]
    [Fact]
    public void GeneratesCircuitBreakerPolicyFactoryForGlobalStructHostWithoutPredicates()
    {
        var source = """
            using PatternKit.Generators.CircuitBreaker;

            [GenerateCircuitBreakerPolicy(typeof(int), FactoryMethodName = "CreateNumbers", PolicyName = "numbers")]
            internal partial struct CircuitBreakerHost;
            """;

        var comp = CreateCompilation(source, nameof(GeneratesCircuitBreakerPolicyFactoryForGlobalStructHostWithoutPredicates));
        var gen = new CircuitBreakerPolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("internal partial struct CircuitBreakerHost", text);
        ScenarioExpect.Contains("CreateNumbers()", text);
        ScenarioExpect.DoesNotContain("namespace Demo;", text);
        ScenarioExpect.DoesNotContain("builder.HandleResult", text);
        ScenarioExpect.DoesNotContain("builder.HandleException", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Reports diagnostic for invalid circuit breaker exception predicate")]
    [Fact]
    public void ReportsDiagnosticForInvalidCircuitBreakerExceptionPredicate()
    {
        var source = """
            using PatternKit.Generators.CircuitBreaker;

            namespace Demo;

            [GenerateCircuitBreakerPolicy(typeof(string))]
            public static partial class CircuitBreakerHost
            {
                [CircuitBreakerExceptionPredicate]
                private static bool HandleException(string exception) => true;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidCircuitBreakerExceptionPredicate));

        ScenarioExpect.Equal("PKCB003", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for duplicate circuit breaker exception predicates")]
    [Fact]
    public void ReportsDiagnosticForDuplicateCircuitBreakerExceptionPredicates()
    {
        var source = """
            using System;
            using PatternKit.Generators.CircuitBreaker;

            namespace Demo;

            [GenerateCircuitBreakerPolicy(typeof(string))]
            public static partial class CircuitBreakerHost
            {
                [CircuitBreakerExceptionPredicate]
                private static bool First(Exception exception) => false;

                [CircuitBreakerExceptionPredicate]
                private static bool Second(Exception exception) => true;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForDuplicateCircuitBreakerExceptionPredicates));

        ScenarioExpect.Equal("PKCB004", diagnostic.Id);
    }

    [Scenario("Generates circuit breaker policy factory for abstract and sealed hosts")]
    [Fact]
    public void GeneratesCircuitBreakerPolicyFactoryForAbstractAndSealedHosts()
    {
        var source = """
            using PatternKit.Generators.CircuitBreaker;

            namespace Demo;

            [GenerateCircuitBreakerPolicy(typeof(string), FactoryMethodName = "CreateAbstract")]
            public abstract partial class AbstractCircuitBreakerHost;

            [GenerateCircuitBreakerPolicy(typeof(string), FactoryMethodName = "CreateSealed")]
            public sealed partial class SealedCircuitBreakerHost;
            """;

        var comp = CreateCompilation(source, nameof(GeneratesCircuitBreakerPolicyFactoryForAbstractAndSealedHosts));
        var gen = new CircuitBreakerPolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = run.Results.SelectMany(result => result.GeneratedSources).ToArray();
        ScenarioExpect.Equal(2, generated.Length);
        var abstractText = ScenarioExpect.Single(generated.Where(source => source.HintName == "AbstractCircuitBreakerHost.CircuitBreakerPolicy.g.cs")).SourceText.ToString();
        var sealedText = ScenarioExpect.Single(generated.Where(source => source.HintName == "SealedCircuitBreakerHost.CircuitBreakerPolicy.g.cs")).SourceText.ToString();
        ScenarioExpect.Contains("public abstract partial class AbstractCircuitBreakerHost", abstractText);
        ScenarioExpect.Contains("CreateAbstract()", abstractText);
        ScenarioExpect.Contains("public sealed partial class SealedCircuitBreakerHost", sealedText);
        ScenarioExpect.Contains("CreateSealed()", sealedText);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Generates circuit breaker policy source for nested accessibility variants")]
    [Fact]
    public void GeneratesCircuitBreakerPolicySourceForNestedAccessibilityVariants()
    {
        var source = """
            using PatternKit.Generators.CircuitBreaker;

            namespace Demo;

            public partial class Outer
            {
                [GenerateCircuitBreakerPolicy(typeof(string), FactoryMethodName = "CreatePrivate")]
                private partial class PrivateCircuitBreakerHost;

                [GenerateCircuitBreakerPolicy(typeof(string), FactoryMethodName = "CreateProtected")]
                protected partial class ProtectedCircuitBreakerHost;

                [GenerateCircuitBreakerPolicy(typeof(string), FactoryMethodName = "CreateProtectedInternal")]
                protected internal partial class ProtectedInternalCircuitBreakerHost;

                [GenerateCircuitBreakerPolicy(typeof(string), FactoryMethodName = "CreatePrivateProtected")]
                private protected partial class PrivateProtectedCircuitBreakerHost;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesCircuitBreakerPolicySourceForNestedAccessibilityVariants));
        var gen = new CircuitBreakerPolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generatedText = string.Join("\n", run.Results.SelectMany(result => result.GeneratedSources).Select(source => source.SourceText.ToString()));
        ScenarioExpect.Contains("private partial class PrivateCircuitBreakerHost", generatedText);
        ScenarioExpect.Contains("protected partial class ProtectedCircuitBreakerHost", generatedText);
        ScenarioExpect.Contains("protected internal partial class ProtectedInternalCircuitBreakerHost", generatedText);
        ScenarioExpect.Contains("private protected partial class PrivateProtectedCircuitBreakerHost", generatedText);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(PatternKit.Cloud.CircuitBreaker.CircuitBreakerPolicy<>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(CircuitBreakerPolicyGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new CircuitBreakerPolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
    }
}
