using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class MessageTranslatorGeneratorTests
{
    [Scenario("Generates message translator factory")]
    [Fact]
    public void GeneratesMessageTranslatorFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace Demo;

            public sealed record PartnerOrder(string Id, decimal Amount);
            public sealed record Order(string OrderId, decimal Total);

            [GenerateMessageTranslator(typeof(PartnerOrder), typeof(Order), FactoryName = "Build", TranslatorName = "partner-orders")]
            [MessageTranslatorDropHeader("raw-signature")]
            [MessageTranslatorHeader("content-type", "application/vnd.demo.order+json")]
            public static partial class PartnerOrderTranslator
            {
                [MessageTranslatorHandler]
                private static Order Translate(Message<PartnerOrder> message, MessageContext context)
                    => new(message.Payload.Id, message.Payload.Amount);
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesMessageTranslatorFactory));
        var gen = new MessageTranslatorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("PartnerOrderTranslator.MessageTranslator.g.cs", generated.HintName);
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("MessageTranslator<global::Demo.PartnerOrder, global::Demo.Order>.Create(\"partner-orders\")", text);
        ScenarioExpect.Contains(".PreserveHeaders(true)", text);
        ScenarioExpect.Contains(".TranslateWith(static (message, context) => Translate(message, context));", text);
        ScenarioExpect.Contains("builder.DropHeader(\"raw-signature\");", text);
        ScenarioExpect.Contains("builder.SetHeader(\"content-type\", \"application/vnd.demo.order+json\");", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Reports diagnostic for non-partial message translator host")]
    [Fact]
    public void ReportsDiagnosticForNonPartialMessageTranslatorHost()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace Demo;

            [GenerateMessageTranslator(typeof(string), typeof(int))]
            public static class Host;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForNonPartialMessageTranslatorHost));

        ScenarioExpect.Equal("PKMT001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for missing message translator handler")]
    [Fact]
    public void ReportsDiagnosticForMissingMessageTranslatorHandler()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace Demo;

            [GenerateMessageTranslator(typeof(string), typeof(int))]
            public static partial class Host;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForMissingMessageTranslatorHandler));

        ScenarioExpect.Equal("PKMT002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid message translator handler")]
    [Fact]
    public void ReportsDiagnosticForInvalidMessageTranslatorHandler()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace Demo;

            [GenerateMessageTranslator(typeof(string), typeof(int))]
            public static partial class Host
            {
                [MessageTranslatorHandler]
                private static string Translate(Message<string> message, MessageContext context) => message.Payload;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidMessageTranslatorHandler));

        ScenarioExpect.Equal("PKMT003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(Message<>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(MessageTranslatorGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new MessageTranslatorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
    }
}
