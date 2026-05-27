using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class GuaranteedDeliveryGeneratorTests
{
    [Scenario("Generates guaranteed delivery queue factory")]
    [Fact]
    public void GeneratesGuaranteedDeliveryQueueFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Shipment(string Id);

            [GenerateGuaranteedDelivery(
                typeof(Shipment),
                FactoryName = "Build",
                QueueName = "shipment-delivery",
                LeaseMilliseconds = 45000,
                MaxDeliveryAttempts = 7)]
            public static partial class ShipmentDelivery;

            public static class Demo
            {
                public static string Run() => ShipmentDelivery.Build().Name;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesGuaranteedDeliveryQueueFactory));
        var gen = new GuaranteedDeliveryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("ShipmentDelivery.GuaranteedDelivery.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("GuaranteedDeliveryQueue<global::MyApp.Shipment>", text);
        ScenarioExpect.Contains("InMemoryGuaranteedDeliveryStore<global::MyApp.Shipment>", text);
        ScenarioExpect.Contains(".Name(@\"shipment-delivery\")", text);
        ScenarioExpect.Contains(".LeaseDuration(global::System.TimeSpan.FromMilliseconds(45000))", text);
        ScenarioExpect.Contains(".MaxDeliveryAttempts(7)", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Generates defaults for guaranteed delivery struct host")]
    [Fact]
    public void GeneratesDefaultsForGuaranteedDeliveryStructHost()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            [GenerateGuaranteedDelivery(typeof(string))]
            public readonly partial struct Delivery;
            """;

        var comp = CreateCompilation(source, nameof(GeneratesDefaultsForGuaranteedDeliveryStructHost));
        var gen = new GuaranteedDeliveryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.Empty(run.Results.SelectMany(result => result.Diagnostics));
        var text = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources)).SourceText.ToString();
        ScenarioExpect.Contains("partial struct Delivery", text);
        ScenarioExpect.Contains(".Name(@\"guaranteed-delivery\")", text);
        ScenarioExpect.Contains(".MaxDeliveryAttempts(5)", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Reports diagnostic for non-partial guaranteed delivery host")]
    [Fact]
    public void ReportsDiagnosticForNonPartialGuaranteedDeliveryHost()
    {
        var diagnostic = RunAndGetSingleDiagnostic("""
            using PatternKit.Generators.Messaging;

            [GenerateGuaranteedDelivery(typeof(string))]
            public static class Delivery;
            """, nameof(ReportsDiagnosticForNonPartialGuaranteedDeliveryHost));

        ScenarioExpect.Equal("PKMGD001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid guaranteed delivery configuration")]
    [Fact]
    public void ReportsDiagnosticForInvalidGuaranteedDeliveryConfiguration()
    {
        var diagnostic = RunAndGetSingleDiagnostic("""
            using PatternKit.Generators.Messaging;

            [GenerateGuaranteedDelivery(typeof(string), LeaseMilliseconds = 0)]
            public static partial class Delivery;
            """, nameof(ReportsDiagnosticForInvalidGuaranteedDeliveryConfiguration));

        ScenarioExpect.Equal("PKMGD002", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Message<>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(GuaranteedDeliveryGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new GuaranteedDeliveryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
    }
}
