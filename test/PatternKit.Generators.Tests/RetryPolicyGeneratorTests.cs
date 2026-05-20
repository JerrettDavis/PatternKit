using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Retry;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class RetryPolicyGeneratorTests
{
    [Scenario("Generates retry policy factory")]
    [Fact]
    public void GeneratesRetryPolicyFactory()
    {
        var source = """
            using System;
            using PatternKit.Generators.Retry;

            namespace Demo;

            [GenerateRetryPolicy(typeof(string), FactoryMethodName = "Build", PolicyName = "inventory", MaxAttempts = 5, InitialDelayMilliseconds = 10, BackoffFactor = 2)]
            public static partial class InventoryRetry
            {
                [RetryResultPredicate]
                private static bool RetryResult(string result) => result == "retry";

                [RetryExceptionPredicate]
                private static bool RetryException(Exception exception) => exception is TimeoutException;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesRetryPolicyFactory));
        var gen = new RetryPolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("InventoryRetry.RetryPolicy.g.cs", generated.HintName);
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("RetryPolicy<string>.Create(\"inventory\")", text);
        ScenarioExpect.Contains(".WithMaxAttempts(5)", text);
        ScenarioExpect.Contains(".WithInitialDelay(global::System.TimeSpan.FromMilliseconds(10))", text);
        ScenarioExpect.Contains(".WithExponentialBackoff(2)", text);
        ScenarioExpect.Contains("builder.HandleResult(static result => RetryResult(result));", text);
        ScenarioExpect.Contains("builder.HandleException(static exception => RetryException(exception));", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial retry policy host")]
    [Fact]
    public void ReportsDiagnosticForNonPartialRetryPolicyHost()
    {
        var source = """
            using PatternKit.Generators.Retry;

            namespace Demo;

            [GenerateRetryPolicy(typeof(string))]
            public static class RetryHost;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForNonPartialRetryPolicyHost));

        ScenarioExpect.Equal("PKRET001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid retry configuration")]
    [Fact]
    public void ReportsDiagnosticForInvalidRetryConfiguration()
    {
        var source = """
            using PatternKit.Generators.Retry;

            namespace Demo;

            [GenerateRetryPolicy(typeof(string), MaxAttempts = 0)]
            public static partial class RetryHost;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidRetryConfiguration));

        ScenarioExpect.Equal("PKRET002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid retry predicate")]
    [Fact]
    public void ReportsDiagnosticForInvalidRetryPredicate()
    {
        var source = """
            using PatternKit.Generators.Retry;

            namespace Demo;

            [GenerateRetryPolicy(typeof(string))]
            public static partial class RetryHost
            {
                [RetryResultPredicate]
                private static string RetryResult(string result) => result;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidRetryPredicate));

        ScenarioExpect.Equal("PKRET003", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for duplicate retry predicates")]
    [Fact]
    public void ReportsDiagnosticForDuplicateRetryPredicates()
    {
        var source = """
            using PatternKit.Generators.Retry;

            namespace Demo;

            [GenerateRetryPolicy(typeof(string))]
            public static partial class RetryHost
            {
                [RetryResultPredicate]
                private static bool First(string result) => false;

                [RetryResultPredicate]
                private static bool Second(string result) => true;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForDuplicateRetryPredicates));

        ScenarioExpect.Equal("PKRET004", diagnostic.Id);
    }

    [Scenario("Generates retry policy factory for global struct host without predicates")]
    [Fact]
    public void GeneratesRetryPolicyFactoryForGlobalStructHostWithoutPredicates()
    {
        var source = """
            using PatternKit.Generators.Retry;

            [GenerateRetryPolicy(typeof(int), FactoryMethodName = "CreateNumbers", PolicyName = "numbers")]
            internal partial struct RetryHost;
            """;

        var comp = CreateCompilation(source, nameof(GeneratesRetryPolicyFactoryForGlobalStructHostWithoutPredicates));
        var gen = new RetryPolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("internal partial struct RetryHost", text);
        ScenarioExpect.Contains("CreateNumbers()", text);
        ScenarioExpect.DoesNotContain("namespace Demo;", text);
        ScenarioExpect.DoesNotContain("builder.HandleResult", text);
        ScenarioExpect.DoesNotContain("builder.HandleException", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Reports diagnostic for invalid retry exception predicate")]
    [Fact]
    public void ReportsDiagnosticForInvalidRetryExceptionPredicate()
    {
        var source = """
            using PatternKit.Generators.Retry;

            namespace Demo;

            [GenerateRetryPolicy(typeof(string))]
            public static partial class RetryHost
            {
                [RetryExceptionPredicate]
                private static bool RetryException(string exception) => true;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidRetryExceptionPredicate));

        ScenarioExpect.Equal("PKRET003", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for duplicate retry exception predicates")]
    [Fact]
    public void ReportsDiagnosticForDuplicateRetryExceptionPredicates()
    {
        var source = """
            using System;
            using PatternKit.Generators.Retry;

            namespace Demo;

            [GenerateRetryPolicy(typeof(string))]
            public static partial class RetryHost
            {
                [RetryExceptionPredicate]
                private static bool First(Exception exception) => false;

                [RetryExceptionPredicate]
                private static bool Second(Exception exception) => true;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForDuplicateRetryExceptionPredicates));

        ScenarioExpect.Equal("PKRET004", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(PatternKit.Cloud.Retry.RetryPolicy<>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(RetryPolicyGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new RetryPolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
    }
}
