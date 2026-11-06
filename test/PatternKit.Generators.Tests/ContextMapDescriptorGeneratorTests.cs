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

    [Scenario("Generates context map descriptor for struct host and all relationship kinds")]
    [Fact]
    public void Generates_Context_Map_Descriptor_For_Struct_Host_And_All_Relationship_Kinds()
    {
        var source = """
            using PatternKit.Generators.ContextMaps;

            [GenerateContextMapDescriptor("Enterprise", FactoryMethodName = "CreateEnterprise")]
            [ContextMapRelationship("A", "B", ContextMapRelationshipKind.Partnership, "AB")]
            [ContextMapRelationship("B", "C", ContextMapRelationshipKind.SharedKernel, "BC")]
            [ContextMapRelationship("C", "D", ContextMapRelationshipKind.CustomerSupplier, "CD")]
            [ContextMapRelationship("D", "E", ContextMapRelationshipKind.Conformist, "DE")]
            [ContextMapRelationship("E", "F", ContextMapRelationshipKind.AntiCorruptionLayer, "EF")]
            [ContextMapRelationship("F", "G", ContextMapRelationshipKind.OpenHostService, "FG")]
            [ContextMapRelationship("G", "H", ContextMapRelationshipKind.PublishedLanguage, "GH")]
            [ContextMapRelationship("H", "I", ContextMapRelationshipKind.SeparateWays, "HI")]
            [ContextMapRelationship("I", "J", (ContextMapRelationshipKind)99, "IJ")]
            public partial struct EnterpriseMap;
            """;

        var comp = CreateCompilation(source, nameof(Generates_Context_Map_Descriptor_For_Struct_Host_And_All_Relationship_Kinds));
        var gen = new ContextMapDescriptorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(static result => result.GeneratedSources));
        ScenarioExpect.Equal("EnterpriseMap.ContextMapDescriptor.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("partial struct EnterpriseMap", text);
        ScenarioExpect.Contains("CreateEnterprise()", text);
        ScenarioExpect.Contains("ContextRelationshipKind.Partnership", text);
        ScenarioExpect.Contains("ContextRelationshipKind.SharedKernel", text);
        ScenarioExpect.Contains("ContextRelationshipKind.CustomerSupplier", text);
        ScenarioExpect.Contains("ContextRelationshipKind.Conformist", text);
        ScenarioExpect.Contains("ContextRelationshipKind.AntiCorruptionLayer", text);
        ScenarioExpect.Contains("ContextRelationshipKind.OpenHostService", text);
        ScenarioExpect.Contains("ContextRelationshipKind.PublishedLanguage", text);
        ScenarioExpect.Contains("ContextRelationshipKind.SeparateWays", text);
        ScenarioExpect.DoesNotContain("namespace Demo;", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
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
