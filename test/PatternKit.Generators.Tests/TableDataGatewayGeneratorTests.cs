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
            ScenarioExpect.Contains("public static partial class OrderTableGateway", source);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("Create(\"orders\", SelectKey).Build()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator reports non-partial table data gateway declarations")]
    [Fact]
    public Task Generator_Reports_Non_Partial_Table_Data_Gateway_Declarations()
        => Given("a non-partial table data gateway declaration", () => Compile("""
            using PatternKit.Generators.TableDataGateway;

            public sealed record OrderRow(string OrderId);

            [GenerateTableDataGateway(typeof(OrderRow), typeof(string))]
            public static class OrderTableGateway
            {
                [TableGatewayKeySelector]
                private static string SelectKey(OrderRow row) => row.OrderId;
            }
            """))
        .Then("the diagnostic identifies the host", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKTDG001"))
        .AssertPassed();

    [Scenario("Generator reports missing or duplicate table data gateway key selectors")]
    [Theory]
    [InlineData("public static partial class OrderTableGateway;", "PKTDG002")]
    [InlineData("public static partial class OrderTableGateway { [TableGatewayKeySelector] private static string One(OrderRow row) => row.OrderId; [TableGatewayKeySelector] private static string Two(OrderRow row) => row.OrderId; }", "PKTDG002")]
    public Task Generator_Reports_Missing_Or_Duplicate_Table_Data_Gateway_Key_Selectors(string declaration, string diagnosticId)
        => Given("a table data gateway declaration with an invalid selector count", () => Compile($$"""
            using PatternKit.Generators.TableDataGateway;

            public sealed record OrderRow(string OrderId);

            [GenerateTableDataGateway(typeof(OrderRow), typeof(string))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generator reports invalid table data gateway key selector signatures")]
    [Theory]
    [InlineData("[TableGatewayKeySelector] private string SelectKey(OrderRow row) => row.OrderId;")]
    [InlineData("[TableGatewayKeySelector] private static T SelectKey<T>(OrderRow row) => default!;")]
    [InlineData("[TableGatewayKeySelector] private static string SelectKey() => \"missing\";")]
    [InlineData("[TableGatewayKeySelector] private static string SelectKey(OrderRow row, string tenant) => row.OrderId;")]
    [InlineData("[TableGatewayKeySelector] private static string SelectKey(string row) => row;")]
    [InlineData("[TableGatewayKeySelector] private static int SelectKey(OrderRow row) => 1;")]
    public Task Generator_Reports_Invalid_Table_Data_Gateway_Key_Selector_Signatures(string selector)
        => Given("a table data gateway declaration with an invalid selector signature", () => Compile($$"""
            using PatternKit.Generators.TableDataGateway;

            public sealed record OrderRow(string OrderId);

            [GenerateTableDataGateway(typeof(OrderRow), typeof(string))]
            public partial class OrderTableGateway
            {
                {{selector}}
            }
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKTDG003"))
        .AssertPassed();

    [Scenario("Generator emits table data gateway defaults and type shapes")]
    [Fact]
    public Task Generator_Emits_Table_Data_Gateway_Defaults_And_Type_Shapes()
        => Given("table data gateway declarations using default names and different host shapes", () => Compile("""
            using PatternKit.Generators.TableDataGateway;

            namespace Demo;

            public sealed record OrderRow(string OrderId);

            [GenerateTableDataGateway(typeof(OrderRow), typeof(string))]
            internal abstract partial class AbstractGateway
            {
                [TableGatewayKeySelector]
                private static string SelectKey(OrderRow row) => row.OrderId;
            }

            [GenerateTableDataGateway(typeof(OrderRow), typeof(string), TableName = "tenant\\\"orders")]
            public sealed partial class SealedGateway
            {
                [TableGatewayKeySelector]
                private static string SelectKey(OrderRow row) => row.OrderId;
            }

            [GenerateTableDataGateway(typeof(OrderRow), typeof(string))]
            internal partial struct StructGateway
            {
                [TableGatewayKeySelector]
                private static string SelectKey(OrderRow row) => row.OrderId;
            }
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractGateway", combined);
            ScenarioExpect.Contains("Create()", combined);
            ScenarioExpect.Contains("Create(\"AbstractGateway\", SelectKey).Build()", combined);
            ScenarioExpect.Contains("public sealed partial class SealedGateway", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"orders\", SelectKey).Build()", combined);
            ScenarioExpect.Contains("internal partial struct StructGateway", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator emits nested table data gateway host wrappers")]
    [Fact]
    public Task Generator_Emits_Nested_Table_Data_Gateway_Host_Wrappers()
        => Given("nested table data gateway declarations with non-public accessibility", () => Compile("""
            using PatternKit.Generators.TableDataGateway;

            namespace Demo;

            public sealed record OrderRow(string OrderId);

            public partial class GatewayContainer
            {
                private partial class PrivateHost
                {
                    [GenerateTableDataGateway(typeof(OrderRow), typeof(string))]
                    protected partial class ProtectedGateway
                    {
                        [TableGatewayKeySelector]
                        private static string SelectKey(OrderRow row) => row.OrderId;
                    }

                    [GenerateTableDataGateway(typeof(OrderRow), typeof(string))]
                    private protected partial class PrivateProtectedGateway
                    {
                        [TableGatewayKeySelector]
                        private static string SelectKey(OrderRow row) => row.OrderId;
                    }

                    [GenerateTableDataGateway(typeof(OrderRow), typeof(string))]
                    protected internal partial class ProtectedInternalGateway
                    {
                        [TableGatewayKeySelector]
                        private static string SelectKey(OrderRow row) => row.OrderId;
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class GatewayContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedGateway", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedGateway", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalGateway", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator skips malformed table data gateway type arguments")]
    [Theory]
    [InlineData("null!", "typeof(string)")]
    [InlineData("typeof(OrderRow)", "null!")]
    public Task Generator_Skips_Malformed_Table_Data_Gateway_Type_Arguments(string rowType, string keyType)
        => Given("a table data gateway declaration with a null type argument", () => Compile($$"""
            using PatternKit.Generators.TableDataGateway;

            public sealed record OrderRow(string OrderId);

            [GenerateTableDataGateway({{rowType}}, {{keyType}})]
            public static partial class OrderTableGateway
            {
                [TableGatewayKeySelector]
                private static string SelectKey(OrderRow row) => row.OrderId;
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "TableDataGatewayGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(InMemoryTableDataGateway<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new TableDataGatewayGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);
}
