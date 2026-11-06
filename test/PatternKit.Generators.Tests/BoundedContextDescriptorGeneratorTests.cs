using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.BoundedContexts;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class BoundedContextDescriptorGeneratorTests
{
    [Scenario("Generates bounded context descriptor from attributes")]
    [Fact]
    public void Generates_Bounded_Context_Descriptor_From_Attributes()
    {
        var source = """
            using PatternKit.Generators.BoundedContexts;

            namespace Demo;

            public interface IShipmentQuoter;
            public interface IInventoryAllocator;
            public sealed record Product(string Sku);
            public sealed record ShipmentItem(string Sku);

            [GenerateBoundedContextDescriptor("Fulfillment", FactoryMethodName = "Build")]
            [BoundedContextCapability("quote shipment", typeof(IShipmentQuoter))]
            [BoundedContextCapability("allocate inventory", typeof(IInventoryAllocator))]
            [BoundedContextAdapter("Catalog", "Fulfillment", typeof(Product), typeof(ShipmentItem))]
            public static partial class FulfillmentContext;
            """;

        var comp = CreateCompilation(source, nameof(Generates_Bounded_Context_Descriptor_From_Attributes));
        var gen = new BoundedContextDescriptorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(static result => result.GeneratedSources));
        ScenarioExpect.Equal("FulfillmentContext.BoundedContextDescriptor.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("BoundedContextDescriptor.Create(\"Fulfillment\")", text);
        ScenarioExpect.Contains("builder.AddCapability(\"allocate inventory\", typeof(global::Demo.IInventoryAllocator));", text);
        ScenarioExpect.Contains("builder.AddAdapter(\"Catalog\", \"Fulfillment\", typeof(global::Demo.Product), typeof(global::Demo.ShipmentItem));", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Theory]
    [InlineData("""
        using PatternKit.Generators.BoundedContexts;
        [GenerateBoundedContextDescriptor("Fulfillment")]
        public static class Context;
        """, "PKCTX001")]
    [InlineData("""
        using PatternKit.Generators.BoundedContexts;
        [GenerateBoundedContextDescriptor("Fulfillment")]
        public static partial class Context;
        """, "PKCTX002")]
    [InlineData("""
        using PatternKit.Generators.BoundedContexts;
        public interface IShipmentQuoter;
        [GenerateBoundedContextDescriptor("Fulfillment")]
        [BoundedContextCapability("quote shipment", typeof(IShipmentQuoter))]
        [BoundedContextCapability("quote shipment", typeof(IShipmentQuoter))]
        public static partial class Context;
        """, "PKCTX003")]
    [InlineData("""
        using PatternKit.Generators.BoundedContexts;
        public interface IShipmentQuoter;
        public sealed record Product(string Sku);
        public sealed record ShipmentItem(string Sku);
        [GenerateBoundedContextDescriptor("Fulfillment")]
        [BoundedContextCapability("quote shipment", typeof(IShipmentQuoter))]
        [BoundedContextAdapter("Catalog", "Fulfillment", typeof(Product), typeof(ShipmentItem))]
        [BoundedContextAdapter("Catalog", "Fulfillment", typeof(Product), typeof(ShipmentItem))]
        public static partial class Context;
        """, "PKCTX004")]
    public void Reports_Bounded_Context_Diagnostics(string source, string expected)
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
                MetadataReference.CreateFromFile(typeof(PatternKit.Application.BoundedContexts.BoundedContextDescriptor).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(BoundedContextDescriptorGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new BoundedContextDescriptorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(static result => result.Diagnostics));
    }
}
