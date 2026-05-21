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
    [Fact]
    public Task Generator_Reports_Missing_Mapper_Projections()
        => Given("a mapper declaration without projections", () => Compile("""
            using PatternKit.Generators.DataMapping;

            public sealed record DomainOrder(string Id);
            public sealed record OrderRow(string OrderId);

            [GenerateDataMapper(typeof(DomainOrder), typeof(OrderRow))]
            public static partial class OrderDataMapper;
            """))
            .Then("PKMAP002 is reported", result =>
                ScenarioExpect.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PKMAP002"))
            .AssertPassed();

    [Scenario("Generator reports invalid mapper projection signatures")]
    [Fact]
    public Task Generator_Reports_Invalid_Mapper_Projection_Signatures()
        => Given("a mapper declaration with an invalid projection", () => Compile("""
            using PatternKit.Generators.DataMapping;

            public sealed record DomainOrder(string Id);
            public sealed record OrderRow(string OrderId);

            [GenerateDataMapper(typeof(DomainOrder), typeof(OrderRow))]
            public static partial class OrderDataMapper
            {
                [DataMapperToData]
                private static string ToData(DomainOrder order) => order.Id;

                [DataMapperToDomain]
                private static DomainOrder ToDomain(OrderRow row) => new(row.OrderId);
            }
            """))
            .Then("PKMAP003 is reported", result =>
                ScenarioExpect.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PKMAP003"))
            .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "DataMapperGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(DataMapper<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new DataMapperGenerator(), out var run, out _);
        var result = run.Results.Single();
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources);
}
