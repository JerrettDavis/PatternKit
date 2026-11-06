using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class WireTapGeneratorTests
{
    [Scenario("GeneratesWireTapFactory")]
    [Fact]
    public void GeneratesWireTapFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateWireTap(typeof(Order), FactoryName = "Build", TapName = "orders")]
            public static partial class OrderWireTap
            {
                [WireTapHandler("metrics", 20)]
                private static void Metrics(Message<Order> message, MessageContext context) { }

                [WireTapHandler("audit", 10)]
                private static void Audit(Message<Order> message, MessageContext context) { }
            }

            public static class Demo
            {
                public static int Run()
                    => OrderWireTap.Build().Publish(Message<Order>.Create(new Order("o-1"))).InvokedTaps.Count;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesWireTapFactory));
        var gen = new WireTapGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderWireTap.WireTap.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("WireTap<global::MyApp.Order>", text);
        ScenarioExpect.Contains(".AddTap(@\"audit\", Audit)", text);
        ScenarioExpect.Contains(".AddTap(@\"metrics\", Metrics)", text);
        ScenarioExpect.True(text.IndexOf("audit", StringComparison.Ordinal) < text.IndexOf("metrics", StringComparison.Ordinal));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ReportsDiagnosticForNonPartialWireTap")]
    [Fact]
    public void ReportsDiagnosticForNonPartialWireTap()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateWireTap(typeof(Order))]
            public static class OrderWireTap
            {
                [WireTapHandler("audit", 10)]
                private static void Audit(Message<Order> message, MessageContext context) { }
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialWireTap));
        var gen = new WireTapGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKWT001", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForMissingHandlers")]
    [Fact]
    public void ReportsDiagnosticForMissingHandlers()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateWireTap(typeof(Order))]
            public static partial class OrderWireTap;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForMissingHandlers));
        var gen = new WireTapGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKWT002", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForInvalidHandlerSignature")]
    [Fact]
    public void ReportsDiagnosticForInvalidHandlerSignature()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateWireTap(typeof(Order))]
            public static partial class OrderWireTap
            {
                [WireTapHandler("audit", 10)]
                private static string Audit(Message<Order> message, MessageContext context) => message.Payload.Id;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidHandlerSignature));
        var gen = new WireTapGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKWT003", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForDuplicateHandlerNameOrOrder")]
    [Fact]
    public void ReportsDiagnosticForDuplicateHandlerNameOrOrder()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateWireTap(typeof(Order))]
            public static partial class OrderWireTap
            {
                [WireTapHandler("audit", 10)]
                private static void Audit(Message<Order> message, MessageContext context) { }

                [WireTapHandler("metrics", 10)]
                private static void Metrics(Message<Order> message, MessageContext context) { }
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForDuplicateHandlerNameOrOrder));
        var gen = new WireTapGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKWT004", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Message<>).Assembly.Location));
}
