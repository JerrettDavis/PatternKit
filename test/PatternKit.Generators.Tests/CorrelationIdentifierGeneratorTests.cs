using Microsoft.CodeAnalysis;
using PatternKit.Generators.Messaging;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Correlation Identifier generator")]
public sealed partial class CorrelationIdentifierGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates correlation identifier factory")]
    [Fact]
    public Task Generates_CorrelationIdentifier_Factory()
        => Given("a partial host marked with GenerateCorrelationIdentifier", () => """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateCorrelationIdentifier(typeof(Order), FactoryName = "Build", HeaderName = "X-Correlation", PreserveExisting = false)]
            public static partial class OrderCorrelation;
            """)
            .When("the generator runs", source =>
            {
                var comp = CreateCompilation(source, nameof(Generates_CorrelationIdentifier_Factory));
                _ = RoslynTestHelpers.Run(comp, new CorrelationIdentifierGenerator(), out var run, out _);
                return run.Results.Single().GeneratedSources.Single().SourceText.ToString();
            })
            .Then("the generated factory returns a configured builder", text =>
            {
                ScenarioExpect.Contains("CorrelationIdentifier<global::MyApp.Order>.Builder Build()", text);
                ScenarioExpect.Contains("CorrelationIdentifier<global::MyApp.Order>.Create()", text);
                ScenarioExpect.Contains(".Header(@\"X-Correlation\")", text);
                ScenarioExpect.Contains(".PreserveExisting(false)", text);
            })
            .AssertPassed();

    [Scenario("Reports correlation identifier diagnostics")]
    [Theory]
    [InlineData("[GenerateCorrelationIdentifier(typeof(Order))] public static class OrderCorrelation;", "PKCI001")]
    [InlineData("[GenerateCorrelationIdentifier(typeof(Order), FactoryName = \"\")] public static partial class OrderCorrelation;", "PKCI002")]
    public Task Reports_CorrelationIdentifier_Diagnostics(string declaration, string expected)
        => Given("an invalid GenerateCorrelationIdentifier declaration", () => $$"""
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            {{declaration}}
            """)
            .When("the generator runs", source =>
            {
                var comp = CreateCompilation(source, nameof(Reports_CorrelationIdentifier_Diagnostics) + expected);
                _ = RoslynTestHelpers.Run(comp, new CorrelationIdentifierGenerator(), out var run, out _);
                return run.Diagnostics.Select(static d => d.Id).ToArray();
            })
            .Then("the expected diagnostic is reported", ids =>
                ScenarioExpect.Contains(expected, ids))
            .AssertPassed();

    private static Compilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(source, assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(typeof(GenerateCorrelationIdentifierAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.Correlation.CorrelationIdentifier<>).Assembly.Location)
            ]);
}
