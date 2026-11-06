using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.DomainServices;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class DomainServiceRegistryGeneratorTests
{
    [Scenario("Generates domain service registry from operation methods")]
    [Fact]
    public void Generates_Domain_Service_Registry_From_Operation_Methods()
    {
        var source = """
            using PatternKit.Generators.DomainServices;

            namespace Demo;

            public sealed record QuoteRequest(decimal Weight);
            public sealed record QuoteDecision(string Carrier, decimal Cost);

            [GenerateDomainServiceRegistry(typeof(QuoteRequest), typeof(QuoteDecision), FactoryMethodName = "Build")]
            public static partial class ShippingServices
            {
                [DomainServiceOperation("ground")]
                private static QuoteDecision Ground(QuoteRequest request) => new("ground", request.Weight);

                [DomainServiceOperation("air")]
                private static QuoteDecision Air(QuoteRequest request) => new("air", request.Weight * 2m);
            }
            """;

        var comp = CreateCompilation(source, nameof(Generates_Domain_Service_Registry_From_Operation_Methods));
        var gen = new DomainServiceRegistryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(static result => result.GeneratedSources));
        ScenarioExpect.Equal("ShippingServices.DomainServiceRegistry.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("builder.Add(\"air\", static request => Air(request));", text);
        ScenarioExpect.Contains("builder.Add(\"ground\", static request => Ground(request));", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Theory]
    [InlineData("""
        using PatternKit.Generators.DomainServices;
        [GenerateDomainServiceRegistry(typeof(object), typeof(string))]
        public static class Services;
        """, "PKDOM001")]
    [InlineData("""
        using PatternKit.Generators.DomainServices;
        [GenerateDomainServiceRegistry(typeof(object), typeof(string))]
        public static partial class Services;
        """, "PKDOM002")]
    [InlineData("""
        using PatternKit.Generators.DomainServices;
        [GenerateDomainServiceRegistry(typeof(object), typeof(string))]
        public static partial class Services
        {
            [DomainServiceOperation("broken")]
            private static int Broken(object request) => 1;
        }
        """, "PKDOM003")]
    [InlineData("""
        using PatternKit.Generators.DomainServices;
        [GenerateDomainServiceRegistry(typeof(object), typeof(string))]
        public static partial class Services
        {
            [DomainServiceOperation("quote")]
            private static string One(object request) => "";
            [DomainServiceOperation("quote")]
            private static string Two(object request) => "";
        }
        """, "PKDOM004")]
    public void Reports_Domain_Service_Registry_Diagnostics(string source, string expected)
    {
        var diagnostic = RunAndGetSingleDiagnostic(source, expected);

        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(PatternKit.Application.DomainServices.DomainServiceRegistry<,>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(DomainServiceRegistryGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new DomainServiceRegistryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(static result => result.Diagnostics));
    }
}
