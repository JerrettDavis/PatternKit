using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class MessageFilterGeneratorTests
{
    [Scenario("GeneratesMessageFilterFactory")]
    [Fact]
    public void GeneratesMessageFilterFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel, decimal Total);

            [GenerateMessageFilter(typeof(Order), FactoryName = "Build", FilterName = "orders", RejectionReason = "manual review")]
            public static partial class OrderFilter
            {
                [MessageFilterRule("low-value", 20)]
                private static bool IsLowValue(Message<Order> message, MessageContext context)
                    => message.Payload.Total < 100m;

                [MessageFilterRule("trusted-channel", 10)]
                private static bool IsTrustedChannel(Message<Order> message, MessageContext context)
                    => message.Payload.Channel == "trusted";
            }

            public static class Demo
            {
                public static bool Run()
                    => OrderFilter.Build().Filter(Message<Order>.Create(new Order("trusted", 250m))).Accepted;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesMessageFilterFactory));
        var gen = new MessageFilterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderFilter.MessageFilter.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("MessageFilter<global::MyApp.Order>", text);
        ScenarioExpect.Contains(".AllowWhen(@\"trusted-channel\", IsTrustedChannel)", text);
        ScenarioExpect.Contains(".AllowWhen(@\"low-value\", IsLowValue)", text);
        ScenarioExpect.True(text.IndexOf("trusted-channel", StringComparison.Ordinal) < text.IndexOf("low-value", StringComparison.Ordinal));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ReportsDiagnosticForNonPartialFilter")]
    [Fact]
    public void ReportsDiagnosticForNonPartialFilter()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateMessageFilter(typeof(Order))]
            public static class OrderFilter
            {
                [MessageFilterRule("trusted", 10)]
                private static bool Trusted(Message<Order> message, MessageContext context) => true;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialFilter));
        var gen = new MessageFilterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKMF001", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForMissingRules")]
    [Fact]
    public void ReportsDiagnosticForMissingRules()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateMessageFilter(typeof(Order))]
            public static partial class OrderFilter;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForMissingRules));
        var gen = new MessageFilterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKMF002", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForInvalidRuleSignature")]
    [Fact]
    public void ReportsDiagnosticForInvalidRuleSignature()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateMessageFilter(typeof(Order))]
            public static partial class OrderFilter
            {
                [MessageFilterRule("trusted", 10)]
                private static string Trusted(Message<Order> message, MessageContext context) => "yes";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidRuleSignature));
        var gen = new MessageFilterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKMF003", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForDuplicateRuleNameOrOrder")]
    [Fact]
    public void ReportsDiagnosticForDuplicateRuleNameOrOrder()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateMessageFilter(typeof(Order))]
            public static partial class OrderFilter
            {
                [MessageFilterRule("trusted", 10)]
                private static bool Trusted(Message<Order> message, MessageContext context) => true;

                [MessageFilterRule("guest", 10)]
                private static bool Guest(Message<Order> message, MessageContext context) => true;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForDuplicateRuleNameOrOrder));
        var gen = new MessageFilterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKMF004", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Message<>).Assembly.Location));
}
