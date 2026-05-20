using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class MessageEnvelopeGeneratorTests
{
    [Scenario("Generates typed envelope and context factories")]
    [Fact]
    public void GeneratesTypedEnvelopeAndContextFactories()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record OrderAccepted(string OrderId);

            [GenerateMessageEnvelope(typeof(OrderAccepted), FactoryName = "CreateAccepted", ContextFactoryName = "ContextFor")]
            [MessageEnvelopeHeader("message-id", typeof(string), ParameterName = "messageId")]
            [MessageEnvelopeHeader("correlation-id", typeof(string), ParameterName = "correlationId")]
            [MessageEnvelopeHeader("tenant-id", typeof(string), ParameterName = "tenantId")]
            public static partial class OrderAcceptedEnvelope;

            public static class Demo
            {
                public static string Run()
                {
                    var message = OrderAcceptedEnvelope.CreateAccepted(new OrderAccepted("order-1"), "msg-1", "corr-1", "north");
                    var context = OrderAcceptedEnvelope.ContextFor(message);
                    return $"{message.Payload.OrderId}:{context.Headers.CorrelationId}:{context.Headers.GetString("tenant-id")}";
                }
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesTypedEnvelopeAndContextFactories));
        var gen = new MessageEnvelopeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderAcceptedEnvelope.MessageEnvelope.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("CreateAccepted(global::MyApp.OrderAccepted payload, string messageId, string correlationId, string tenantId)", text);
        ScenarioExpect.Contains(".WithHeader(\"tenant-id\", tenantId)", text);
        ScenarioExpect.Contains("ContextFor(global::PatternKit.Messaging.Message<global::MyApp.OrderAccepted> message", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial envelope contract")]
    [Fact]
    public void ReportsDiagnosticForNonPartialEnvelopeContract()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record OrderAccepted(string OrderId);

            [GenerateMessageEnvelope(typeof(OrderAccepted))]
            [MessageEnvelopeHeader("message-id", typeof(string))]
            public static class OrderAcceptedEnvelope;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialEnvelopeContract));
        var gen = new MessageEnvelopeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKME001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for missing envelope headers")]
    [Fact]
    public void ReportsDiagnosticForMissingEnvelopeHeaders()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record OrderAccepted(string OrderId);

            [GenerateMessageEnvelope(typeof(OrderAccepted))]
            public static partial class OrderAcceptedEnvelope;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForMissingEnvelopeHeaders));
        var gen = new MessageEnvelopeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKME002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid generated parameter name")]
    [Fact]
    public void ReportsDiagnosticForInvalidGeneratedParameterName()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record OrderAccepted(string OrderId);

            [GenerateMessageEnvelope(typeof(OrderAccepted))]
            [MessageEnvelopeHeader("tenant-id", typeof(string), ParameterName = "class")]
            public static partial class OrderAcceptedEnvelope;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidGeneratedParameterName));
        var gen = new MessageEnvelopeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(result => result.Diagnostics).Select(static diagnostic => diagnostic.Id);
        ScenarioExpect.Contains("PKME003", diagnostics);
    }

    [Scenario("Reports diagnostic for duplicate header names")]
    [Fact]
    public void ReportsDiagnosticForDuplicateHeaderNames()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record OrderAccepted(string OrderId);

            [GenerateMessageEnvelope(typeof(OrderAccepted))]
            [MessageEnvelopeHeader("tenant-id", typeof(string), ParameterName = "tenantId")]
            [MessageEnvelopeHeader("Tenant-Id", typeof(string), ParameterName = "tenant")]
            public static partial class OrderAcceptedEnvelope;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForDuplicateHeaderNames));
        var gen = new MessageEnvelopeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKME004", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Message<>).Assembly.Location));
}
