using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.ContextMaps;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ContextMapDescriptorGeneratorTests
{
    [Scenario("Generates context map descriptor from attributes")]
    [Fact]
    public void Generates_Context_Map_Descriptor_From_Attributes()
    {
        var source = """
            using PatternKit.Generators.ContextMaps;

            namespace Demo;

            [GenerateContextMapDescriptor("Commerce", FactoryMethodName = "Build")]
            [ContextMapRelationship("Catalog", "Fulfillment", ContextMapRelationshipKind.PublishedLanguage, "ProductFeed")]
            [ContextMapRelationship("Fulfillment", "Billing", ContextMapRelationshipKind.CustomerSupplier, "ShipmentBilling")]
            public static partial class CommerceMap;
            """;

        var comp = CreateCompilation(source, nameof(Generates_Context_Map_Descriptor_From_Attributes));
        var gen = new ContextMapDescriptorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(static result => result.GeneratedSources));
        ScenarioExpect.Equal("CommerceMap.ContextMapDescriptor.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("ContextMapDescriptor.Create(\"Commerce\")", text);
        ScenarioExpect.Contains("builder.AddRelationship(\"Catalog\", \"Fulfillment\", global::PatternKit.Application.ContextMaps.ContextRelationshipKind.PublishedLanguage, \"ProductFeed\");", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Theory]
    [InlineData("""
        using PatternKit.Generators.ContextMaps;
        [GenerateContextMapDescriptor("Commerce")]
        public static class Contexts;
        """, "PKCMAP001")]
    [InlineData("""
        using PatternKit.Generators.ContextMaps;
        [GenerateContextMapDescriptor("Commerce")]
        public static partial class Contexts;
        """, "PKCMAP002")]
    [InlineData("""
        using PatternKit.Generators.ContextMaps;
        [GenerateContextMapDescriptor("Commerce")]
        [ContextMapRelationship("Catalog", "Fulfillment", ContextMapRelationshipKind.PublishedLanguage, "ProductFeed")]
        [ContextMapRelationship("Catalog", "Fulfillment", ContextMapRelationshipKind.PublishedLanguage, "ProductFeed")]
        public static partial class Contexts;
        """, "PKCMAP003")]
    public void Reports_Context_Map_Diagnostics(string source, string expected)
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
                MetadataReference.CreateFromFile(typeof(PatternKit.Application.ContextMaps.ContextMapDescriptor).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(ContextMapDescriptorGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new ContextMapDescriptorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(static result => result.Diagnostics));
    }
}
