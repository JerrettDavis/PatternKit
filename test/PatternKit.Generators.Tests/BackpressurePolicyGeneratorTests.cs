using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Backpressure;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Backpressure Policy generator")]
public sealed partial class BackpressurePolicyGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates backpressure policy factory")]
    [Fact]
    public Task Generates_Backpressure_Policy_Factory()
        => Given("a configured backpressure policy declaration", () => Compile("""
            using PatternKit.Generators.Backpressure;
            namespace Demo;
            [GenerateBackpressurePolicy(typeof(string), FactoryMethodName = "Build", PolicyName = "checkout", Capacity = 4, Mode = "Wait", WaitTimeoutMilliseconds = 250)]
            public static partial class CheckoutBackpressure;
            """))
        .Then("generated source creates the configured policy", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Equal("CheckoutBackpressure.BackpressurePolicy.g.cs", source.HintName);
            ScenarioExpect.Contains("public static partial class CheckoutBackpressure", source.Source);
            ScenarioExpect.Contains("Build()", source.Source);
            ScenarioExpect.Contains("BackpressurePolicy<string>.Create(\"checkout\")", source.Source);
            ScenarioExpect.Contains(".WithCapacity(4)", source.Source);
            ScenarioExpect.Contains(".WithMode(global::PatternKit.Messaging.Reliability.Backpressure.BackpressureMode.Wait)", source.Source);
            ScenarioExpect.Contains(".WithWaitTimeout(global::System.TimeSpan.FromMilliseconds(250))", source.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid backpressure declarations")]
    [Theory]
    [InlineData("public static class BackpressureHost;", "PKBP001")]
    [InlineData("public static partial class BackpressureHost;", "PKBP002", "Capacity = 0")]
    [InlineData("public static partial class BackpressureHost;", "PKBP002", "WaitTimeoutMilliseconds = -1")]
    [InlineData("public static partial class BackpressureHost;", "PKBP003", "FactoryMethodName = \"1bad\"")]
    [InlineData("public static partial class BackpressureHost;", "PKBP003", "FactoryMethodName = \"class\"")]
    [InlineData("public static partial class BackpressureHost;", "PKBP004", "Mode = \"Unknown\"")]
    public Task Reports_Diagnostics_For_Invalid_Backpressure_Declarations(string declaration, string diagnosticId, string configuration = "")
        => Given("an invalid backpressure policy declaration", () => Compile($$"""
            using PatternKit.Generators.Backpressure;
            [GenerateBackpressurePolicy(typeof(string){{(string.IsNullOrWhiteSpace(configuration) ? "" : ", " + configuration)}})]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generates nested backpressure host wrappers")]
    [Fact]
    public Task Generates_Nested_Backpressure_Host_Wrappers()
        => Given("nested backpressure declarations", () => Compile("""
            using PatternKit.Generators.Backpressure;
            namespace Demo;

            public partial class BackpressureContainer
            {
                private partial class PrivateHost
                {
                    [GenerateBackpressurePolicy(typeof(int), Mode = "DropNewest")]
                    protected partial class ProtectedBackpressure;
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public partial class BackpressureContainer", source.Source);
            ScenarioExpect.Contains("private partial class PrivateHost", source.Source);
            ScenarioExpect.Contains("protected partial class ProtectedBackpressure", source.Source);
            ScenarioExpect.Contains("BackpressureMode.DropNewest", source.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates backpressure defaults and host shapes")]
    [Fact]
    public Task Generates_Backpressure_Defaults_And_Host_Shapes()
        => Given("backpressure policy declarations with default names and host shapes", () => Compile("""
            using PatternKit.Generators.Backpressure;
            namespace Demo;

            [GenerateBackpressurePolicy(typeof(string))]
            internal abstract partial class AbstractBackpressure;

            [GenerateBackpressurePolicy(typeof(string), PolicyName = "tenant\\\"backpressure")]
            public sealed partial class SealedBackpressure;

            [GenerateBackpressurePolicy(typeof(int))]
            internal partial struct StructBackpressure;
            """))
        .Then("generated sources preserve host shape and configured defaults", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources.Select(static source => source.Source));
            ScenarioExpect.Contains("internal abstract partial class AbstractBackpressure", combined);
            ScenarioExpect.Contains("public sealed partial class SealedBackpressure", combined);
            ScenarioExpect.Contains("internal partial struct StructBackpressure", combined);
            ScenarioExpect.Contains("Create(\"backpressure\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"backpressure\")", combined);
            ScenarioExpect.Contains(".WithCapacity(8)", combined);
            ScenarioExpect.Contains("BackpressureMode.Reject", combined);
            ScenarioExpect.Contains("FromMilliseconds(0)", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Backpressure attribute exposes configured values")]
    [Fact]
    public Task Backpressure_Attribute_Exposes_Configured_Values()
        => Given("a backpressure generator attribute", () => new GenerateBackpressurePolicyAttribute(typeof(string))
        {
            FactoryMethodName = "Build",
            PolicyName = "checkout",
            Capacity = 4,
            Mode = "Wait",
            WaitTimeoutMilliseconds = 250
        })
        .Then("configuration values are preserved", attribute =>
        {
            ScenarioExpect.Equal(typeof(string), attribute.ResultType);
            ScenarioExpect.Equal("Build", attribute.FactoryMethodName);
            ScenarioExpect.Equal("checkout", attribute.PolicyName);
            ScenarioExpect.Equal(4, attribute.Capacity);
            ScenarioExpect.Equal("Wait", attribute.Mode);
            ScenarioExpect.Equal(250, attribute.WaitTimeoutMilliseconds);
            ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateBackpressurePolicyAttribute(null!));
        })
        .AssertPassed();

    [Scenario("Skips malformed backpressure result type")]
    [Fact]
    public Task Skips_Malformed_Backpressure_Result_Type()
        => Given("a backpressure declaration with a null result type", () => Compile("""
            using PatternKit.Generators.Backpressure;
            [GenerateBackpressurePolicy(null!)]
            public static partial class BackpressureHost;
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = CreateCompilation(source, "BackpressurePolicyGeneratorTests");
        _ = RoslynTestHelpers.Run(compilation, new BackpressurePolicyGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => new GeneratedSource(source.HintName, source.SourceText.ToString())).ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Reliability.Backpressure.BackpressurePolicy<>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(BackpressurePolicyGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<GeneratedSource> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);

    private sealed record GeneratedSource(string HintName, string Source);
}
