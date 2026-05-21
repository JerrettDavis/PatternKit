using Microsoft.CodeAnalysis;
using PatternKit.Application.TableDataGateway;
using PatternKit.Generators.TableDataGateway;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Table Data Gateway generator")]
public sealed partial class TableDataGatewayGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits table data gateway factory")]
    [Fact]
    public Task Generator_Emits_Table_Data_Gateway_Factory()
        => Given("a valid table data gateway declaration", () => Compile("""
            using PatternKit.Generators.TableDataGateway;
            namespace Demo;
            public sealed record OrderRow(string OrderId);
            [GenerateTableDataGateway(typeof(OrderRow), typeof(string), FactoryName = "Build", TableName = "orders")]
            public static partial class OrderTableGateway
            {
                [TableGatewayKeySelector]
                private static string SelectKey(OrderRow row) => row.OrderId;
            }
            """))
        .Then("generated source creates the gateway", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("Create(\"orders\", SelectKey).Build()", source);
        })
        .AssertPassed();

    [Scenario("Generator reports invalid table data gateway declarations")]
    [Theory]
    [InlineData("public static class OrderTableGateway { [TableGatewayKeySelector] private static string SelectKey(OrderRow row) => row.OrderId; }", "PKTDG001")]
    [InlineData("public static partial class OrderTableGateway;", "PKTDG002")]
    [InlineData("public static partial class OrderTableGateway { [TableGatewayKeySelector] private static string One(OrderRow row) => row.OrderId; [TableGatewayKeySelector] private static string Two(OrderRow row) => row.OrderId; }", "PKTDG002")]
    [InlineData("public static partial class OrderTableGateway { [TableGatewayKeySelector] private static int SelectKey(OrderRow row) => 1; }", "PKTDG003")]
    public Task Generator_Reports_Invalid_Table_Data_Gateway_Declarations(string declaration, string diagnosticId)
        => Given("an invalid table data gateway declaration", () => Compile($$"""
            using PatternKit.Generators.TableDataGateway;
            public sealed record OrderRow(string OrderId);
            [GenerateTableDataGateway(typeof(OrderRow), typeof(string))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "TableDataGatewayGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(InMemoryTableDataGateway<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new TableDataGatewayGenerator(), out var run, out _);
        var result = run.Results.Single();
        return new GeneratorResult(result.Diagnostics.ToArray(), result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources);
}
