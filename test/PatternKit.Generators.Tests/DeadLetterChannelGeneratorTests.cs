using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.Reliability;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class DeadLetterChannelGeneratorTests
{
    [Scenario("Generates dead letter channel factory")]
    [Fact]
    public void GeneratesDeadLetterChannelFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging.Reliability;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateDeadLetterChannel(
                typeof(Order),
                FactoryName = "BuildChannel",
                ChannelName = "checkout-dead",
                Source = "checkout.fulfillment",
                IdPrefix = "checkout-dead",
                IncludeExceptionDetails = false)]
            public static partial class CheckoutDeadLetters
            {
                [DeadLetterStoreFactory]
                private static IDeadLetterStore<Order> CreateStore() => new InMemoryDeadLetterStore<Order>();
            }

            public static class Demo
            {
                public static DeadLetterChannel<Order> Run() => CheckoutDeadLetters.BuildChannel();
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesDeadLetterChannelFactory));
        var gen = new DeadLetterChannelGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("CheckoutDeadLetters.DeadLetterChannel.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("BuildChannel", text);
        ScenarioExpect.Contains(".FromSource(\"checkout.fulfillment\")", text);
        ScenarioExpect.Contains(".UseStore(CreateStore())", text);
        ScenarioExpect.Contains("\"checkout-dead:\"", text);
        ScenarioExpect.Contains(".IncludeExceptionDetails(false)", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non partial dead letter channel")]
    [Fact]
    public void ReportsDiagnosticForNonPartialDeadLetterChannel()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateDeadLetterChannel(typeof(Order))]
            public static class CheckoutDeadLetters;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialDeadLetterChannel));
        var gen = new DeadLetterChannelGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKDL001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for missing dead letter store factory")]
    [Fact]
    public void ReportsDiagnosticForMissingDeadLetterStoreFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateDeadLetterChannel(typeof(Order))]
            public static partial class CheckoutDeadLetters;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForMissingDeadLetterStoreFactory));
        var gen = new DeadLetterChannelGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKDL002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid dead letter store factory")]
    [Fact]
    public void ReportsDiagnosticForInvalidDeadLetterStoreFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateDeadLetterChannel(typeof(Order))]
            public static partial class CheckoutDeadLetters
            {
                [DeadLetterStoreFactory]
                private static string CreateStore() => "bad";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidDeadLetterStoreFactory));
        var gen = new DeadLetterChannelGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKDL003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(DeadLetterChannel<>).Assembly.Location));
}
