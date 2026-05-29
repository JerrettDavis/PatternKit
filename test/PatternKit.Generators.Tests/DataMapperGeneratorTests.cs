using Microsoft.CodeAnalysis;
using PatternKit.Application.DataMapping;
using PatternKit.Generators.DataMapping;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Data Mapper generator")]
public sealed partial class DataMapperGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits mapper factory for valid projections")]
    [Fact]
    public Task Generator_Emits_Mapper_Factory_For_Valid_Projections()
        => Given("a partial mapper declaration", () => Compile("""
            using PatternKit.Generators.DataMapping;

            namespace Demo;

            public sealed record DomainOrder(string Id, decimal Total);
            public sealed record OrderRow(string OrderId, decimal Amount);

            [GenerateDataMapper(typeof(DomainOrder), typeof(OrderRow), FactoryName = "CreateMapper")]
            public static partial class OrderDataMapper
            {
                [DataMapperToData]
                private static OrderRow ToData(DomainOrder order) => new(order.Id, order.Total);

                [DataMapperToDomain]
                private static DomainOrder ToDomain(OrderRow row) => new(row.OrderId, row.Amount);
            }
            """))
            .Then("generated source contains a fluent Data Mapper factory", result =>
            {
                ScenarioExpect.Empty(result.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
                var source = ScenarioExpect.Single(result.GeneratedSources);
                ScenarioExpect.Contains("CreateMapper()", source);
                ScenarioExpect.Contains(".MapToData(ToData)", source);
                ScenarioExpect.Contains(".MapToDomain(ToDomain)", source);
                ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
            })
            .AssertPassed();

    [Scenario("Generator reports non partial mapper hosts")]
    [Fact]
    public Task Generator_Reports_Non_Partial_Mapper_Hosts()
        => Given("a non partial mapper declaration", () => Compile("""
            using PatternKit.Generators.DataMapping;

            public sealed record DomainOrder(string Id);
            public sealed record OrderRow(string OrderId);

            [GenerateDataMapper(typeof(DomainOrder), typeof(OrderRow))]
            public static class OrderDataMapper
            {
                [DataMapperToData]
                private static OrderRow ToData(DomainOrder order) => new(order.Id);

                [DataMapperToDomain]
                private static DomainOrder ToDomain(OrderRow row) => new(row.OrderId);
            }
            """))
            .Then("PKMAP001 is reported", result =>
                ScenarioExpect.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PKMAP001"))
            .AssertPassed();

    [Scenario("Generator reports missing mapper projections")]
    [Theory]
    [InlineData("")]
    [InlineData("""
        [DataMapperToData]
        private static OrderRow ToData(DomainOrder order) => new(order.Id);
        [DataMapperToData]
        private static OrderRow ToDataAgain(DomainOrder order) => new(order.Id);
        [DataMapperToDomain]
        private static DomainOrder ToDomain(OrderRow row) => new(row.OrderId);
        """)]
    [InlineData("""
        [DataMapperToData]
        private static OrderRow ToData(DomainOrder order) => new(order.Id);
        [DataMapperToDomain]
        private static DomainOrder ToDomain(OrderRow row) => new(row.OrderId);
        [DataMapperToDomain]
        private static DomainOrder ToDomainAgain(OrderRow row) => new(row.OrderId);
        """)]
    public Task Generator_Reports_Missing_Mapper_Projections(string projections)
        => Given("a mapper declaration with missing or duplicate projections", () => Compile($$"""
            using PatternKit.Generators.DataMapping;

            public sealed record DomainOrder(string Id);
            public sealed record OrderRow(string OrderId);

            [GenerateDataMapper(typeof(DomainOrder), typeof(OrderRow))]
            public static partial class OrderDataMapper
            {
                {{projections}}
            }
            """))
            .Then("PKMAP002 is reported", result =>
                ScenarioExpect.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PKMAP002"))
            .AssertPassed();

    [Scenario("Generator reports invalid mapper projection signatures")]
    [Theory]
    [InlineData("[DataMapperToData] private OrderRow ToData(DomainOrder order) => new(order.Id);")]
    [InlineData("[DataMapperToData] private static T ToData<T>(DomainOrder order) => default!;")]
    [InlineData("[DataMapperToData] private static OrderRow ToData() => new(\"missing\");")]
    [InlineData("[DataMapperToData] private static OrderRow ToData(DomainOrder order, string tenant) => new(order.Id);")]
    [InlineData("[DataMapperToData] private static OrderRow ToData(OrderRow row) => row;")]
    [InlineData("[DataMapperToData] private static string ToData(DomainOrder order) => order.Id;")]
    public Task Generator_Reports_Invalid_To_Data_Projection_Signatures(string projection)
        => Given("a mapper declaration with an invalid to-data projection", () => Compile($$"""
            using PatternKit.Generators.DataMapping;

            public sealed record DomainOrder(string Id);
            public sealed record OrderRow(string OrderId);

            [GenerateDataMapper(typeof(DomainOrder), typeof(OrderRow))]
            public static partial class OrderDataMapper
            {
                {{projection}}

                [DataMapperToDomain]
                private static DomainOrder ToDomain(OrderRow row) => new(row.OrderId);
            }
            """))
            .Then("PKMAP003 is reported", result =>
                ScenarioExpect.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PKMAP003"))
            .AssertPassed();

    [Scenario("Generator reports invalid to-domain mapper projection signatures")]
    [Fact]
    public Task Generator_Reports_Invalid_To_Domain_Mapper_Projection_Signatures()
        => Given("a mapper declaration with an invalid to-domain projection", () => Compile("""
            using PatternKit.Generators.DataMapping;

            public sealed record DomainOrder(string Id);
            public sealed record OrderRow(string OrderId);

            [GenerateDataMapper(typeof(DomainOrder), typeof(OrderRow))]
            public static partial class OrderDataMapper
            {
                [DataMapperToData]
                private static OrderRow ToData(DomainOrder order) => new(order.Id);

                [DataMapperToDomain]
                private static OrderRow ToDomain(OrderRow row) => row;
            }
            """))
            .Then("PKMAP003 is reported", result =>
                ScenarioExpect.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PKMAP003"))
            .AssertPassed();

    [Scenario("Generator emits mapper defaults and type shapes")]
    [Fact]
    public Task Generator_Emits_Mapper_Defaults_And_Type_Shapes()
        => Given("mapper declarations using default names and different host shapes", () => Compile("""
            using PatternKit.Generators.DataMapping;

            namespace Demo;

            public sealed record DomainOrder(string Id);
            public sealed record OrderRow(string OrderId);

            [GenerateDataMapper(typeof(DomainOrder), typeof(OrderRow))]
            internal abstract partial class AbstractMapper
            {
                [DataMapperToData]
                private static OrderRow ToData(DomainOrder order) => new(order.Id);

                [DataMapperToDomain]
                private static DomainOrder ToDomain(OrderRow row) => new(row.OrderId);
            }

            [GenerateDataMapper(typeof(DomainOrder), typeof(OrderRow))]
            public sealed partial class SealedMapper
            {
                [DataMapperToData]
                private static OrderRow ToData(DomainOrder order) => new(order.Id);

                [DataMapperToDomain]
                private static DomainOrder ToDomain(OrderRow row) => new(row.OrderId);
            }

            [GenerateDataMapper(typeof(DomainOrder), typeof(OrderRow))]
            internal partial struct StructMapper
            {
                [DataMapperToData]
                private static OrderRow ToData(DomainOrder order) => new(order.Id);

                [DataMapperToDomain]
                private static DomainOrder ToDomain(OrderRow row) => new(row.OrderId);
            }
            """))
            .Then("generated sources preserve host shape and default factory names", result =>
            {
                ScenarioExpect.Empty(result.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
                ScenarioExpect.Equal(3, result.GeneratedSources.Count);

                var combined = string.Join("\n", result.GeneratedSources);
                ScenarioExpect.Contains("internal abstract partial class AbstractMapper", combined);
                ScenarioExpect.Contains("DataMapper<global::Demo.DomainOrder, global::Demo.OrderRow> Create()", combined);
                ScenarioExpect.Contains("public sealed partial class SealedMapper", combined);
                ScenarioExpect.Contains("internal partial struct StructMapper", combined);
                ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
            })
            .AssertPassed();

    [Scenario("Generator emits nested mapper host wrappers")]
    [Fact]
    public Task Generator_Emits_Nested_Mapper_Host_Wrappers()
        => Given("nested mapper declarations with non-public accessibility", () => Compile("""
            using PatternKit.Generators.DataMapping;

            namespace Demo;

            public sealed record DomainOrder(string Id);
            public sealed record OrderRow(string OrderId);

            public partial class MapperContainer
            {
                private partial class PrivateHost
                {
                    [GenerateDataMapper(typeof(DomainOrder), typeof(OrderRow))]
                    protected partial class ProtectedMapper
                    {
                        [DataMapperToData]
                        private static OrderRow ToData(DomainOrder order) => new(order.Id);

                        [DataMapperToDomain]
                        private static DomainOrder ToDomain(OrderRow row) => new(row.OrderId);
                    }

                    [GenerateDataMapper(typeof(DomainOrder), typeof(OrderRow))]
                    private protected partial class PrivateProtectedMapper
                    {
                        [DataMapperToData]
                        private static OrderRow ToData(DomainOrder order) => new(order.Id);

                        [DataMapperToDomain]
                        private static DomainOrder ToDomain(OrderRow row) => new(row.OrderId);
                    }

                    [GenerateDataMapper(typeof(DomainOrder), typeof(OrderRow))]
                    protected internal partial class ProtectedInternalMapper
                    {
                        [DataMapperToData]
                        private static OrderRow ToData(DomainOrder order) => new(order.Id);

                        [DataMapperToDomain]
                        private static DomainOrder ToDomain(OrderRow row) => new(row.OrderId);
                    }
                }
            }
            """))
            .Then("generated sources preserve containing partial type wrappers", result =>
            {
                ScenarioExpect.Empty(result.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
                ScenarioExpect.Equal(3, result.GeneratedSources.Count);

                var combined = string.Join("\n", result.GeneratedSources);
                ScenarioExpect.Contains("public partial class MapperContainer", combined);
                ScenarioExpect.Contains("private partial class PrivateHost", combined);
                ScenarioExpect.Contains("protected partial class ProtectedMapper", combined);
                ScenarioExpect.Contains("private protected partial class PrivateProtectedMapper", combined);
                ScenarioExpect.Contains("protected internal partial class ProtectedInternalMapper", combined);
                ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
            })
            .AssertPassed();

    [Scenario("Generator skips malformed mapper type arguments")]
    [Theory]
    [InlineData("null!", "typeof(OrderRow)")]
    [InlineData("typeof(DomainOrder)", "null!")]
    public Task Generator_Skips_Malformed_Mapper_Type_Arguments(string domainType, string dataType)
        => Given("a mapper declaration with a null type argument", () => Compile($$"""
            using PatternKit.Generators.DataMapping;

            public sealed record DomainOrder(string Id);
            public sealed record OrderRow(string OrderId);

            [GenerateDataMapper({{domainType}}, {{dataType}})]
            public static partial class OrderDataMapper
            {
                [DataMapperToData]
                private static OrderRow ToData(DomainOrder order) => new(order.Id);

                [DataMapperToDomain]
                private static DomainOrder ToDomain(OrderRow row) => new(row.OrderId);
            }
            """))
            .Then("no source is generated", result =>
                ScenarioExpect.Empty(result.GeneratedSources))
            .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "DataMapperGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(DataMapper<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new DataMapperGenerator(), out var run, out var updated);
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
