using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.ChangeDataCapture;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Change Data Capture generator")]
public sealed partial class ChangeDataCaptureGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates change data capture factory")]
    [Fact]
    public Task Generates_Change_Data_Capture_Factory()
        => Given("a configured CDC declaration", () => Compile("""
            using PatternKit.Generators.ChangeDataCapture;
            using System.Threading;
            using System.Threading.Tasks;
            namespace Demo;

            public sealed record Mutation(string Sku, int Quantity);
            public sealed record Changed(long Sequence, string Sku, int Quantity);

            [GenerateChangeDataCapture(typeof(Mutation), typeof(Changed), FactoryMethodName = "Build", MapperMethodName = "MapMutation", PipelineName = "inventory-cdc")]
            public static partial class InventoryCdc
            {
                public static Changed MapMutation(Mutation mutation, long sequence) => new(sequence, mutation.Sku, mutation.Quantity);
            }
            """))
        .Then("generated source creates the configured pipeline", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Equal("InventoryCdc.ChangeDataCapture.g.cs", source.HintName);
            ScenarioExpect.Contains("Build(", source.Source);
            ScenarioExpect.Contains("Create(\"inventory-cdc\")", source.Source);
            ScenarioExpect.Contains(".MapWith(MapMutation)", source.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates change data capture factory for nested hosts with defaults")]
    [Fact]
    public Task Generates_Change_Data_Capture_Factory_For_Nested_Hosts_With_Defaults()
        => Given("a nested CDC declaration with default configuration", () => Compile("""
            using PatternKit.Generators.ChangeDataCapture;

            public sealed record Mutation(string Sku);
            public sealed record Changed(string Sku);

            public abstract partial class Outer
            {
                [GenerateChangeDataCapture(typeof(Mutation), typeof(Changed))]
                internal sealed partial class CdcHost
                {
                    public static Changed Map(Mutation mutation, long sequence) => new(mutation.Sku);
                }
            }
            """))
        .Then("generated source preserves the nested partial shape and default names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public abstract partial class Outer", source.Source);
            ScenarioExpect.Contains("internal sealed partial class CdcHost", source.Source);
            ScenarioExpect.Contains("Create(\"change-data-capture\")", source.Source);
            ScenarioExpect.Contains("Create(", source.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid change data capture declarations")]
    [Theory]
    [InlineData("public static class CdcHost { }", "PKCDC001")]
    [InlineData("public static partial class CdcHost { }", "PKCDC002", "FactoryMethodName = \"class\"")]
    [InlineData("public static partial class CdcHost { }", "PKCDC002", "MapperMethodName = \"1bad\"")]
    public Task Reports_Diagnostics_For_Invalid_Change_Data_Capture_Declarations(string declaration, string diagnosticId, string configuration = "")
        => Given("an invalid CDC declaration", () => Compile($$"""
            using PatternKit.Generators.ChangeDataCapture;
            public sealed record Mutation(string Sku);
            public sealed record Changed(string Sku);
            [GenerateChangeDataCapture(typeof(Mutation), typeof(Changed){{(string.IsNullOrWhiteSpace(configuration) ? "" : ", " + configuration)}})]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Reports diagnostic when change data capture host has a non-partial containing type")]
    [Fact]
    public Task Reports_Diagnostic_When_Change_Data_Capture_Host_Has_A_Non_Partial_Containing_Type()
        => Given("a nested CDC host inside a non-partial container", () => Compile("""
            using PatternKit.Generators.ChangeDataCapture;
            public sealed record Mutation(string Sku);
            public sealed record Changed(string Sku);

            public static class Outer
            {
                [GenerateChangeDataCapture(typeof(Mutation), typeof(Changed))]
                public static partial class CdcHost
                {
                    public static Changed Map(Mutation mutation, long sequence) => new(mutation.Sku);
                }
            }
            """))
        .Then("the containing type diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKCDC004"))
        .AssertPassed();

    [Scenario("Change data capture attribute exposes generator configuration")]
    [Fact]
    public void Change_Data_Capture_Attribute_Exposes_Generator_Configuration()
    {
        var attribute = new GenerateChangeDataCaptureAttribute(typeof(string), typeof(int))
        {
            FactoryMethodName = "CreateCdc",
            MapperMethodName = "MapCdc",
            PipelineName = "cdc"
        };

        ScenarioExpect.Equal(typeof(string), attribute.MutationType);
        ScenarioExpect.Equal(typeof(int), attribute.EventType);
        ScenarioExpect.Equal("CreateCdc", attribute.FactoryMethodName);
        ScenarioExpect.Equal("MapCdc", attribute.MapperMethodName);
        ScenarioExpect.Equal("cdc", attribute.PipelineName);
    }

    private static GeneratorResult Compile(string source)
    {
        var compilation = CreateCompilation(source, "ChangeDataCaptureGeneratorTests");
        _ = RoslynTestHelpers.Run(compilation, new ChangeDataCaptureGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new(
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
                MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.ChangeDataCapture.ChangeDataCapturePipeline<,>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(ChangeDataCaptureGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<GeneratedSource> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);

    private sealed record GeneratedSource(string HintName, string Source);
}
