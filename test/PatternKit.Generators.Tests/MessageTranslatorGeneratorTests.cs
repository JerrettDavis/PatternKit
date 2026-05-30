using Microsoft.CodeAnalysis;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Message Translator generator")]
public sealed partial class MessageTranslatorGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates message translator factory")]
    [Fact]
    public Task Generates_Message_Translator_Factory()
        => Given("a message translator declaration", () => Compile("""
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
            """))
        .Then("the generated source creates the configured translator", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var generated = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Equal("PartnerOrderTranslator.MessageTranslator.g.cs", generated.HintName);
            ScenarioExpect.Contains("Build()", generated.Source);
            ScenarioExpect.Contains("MessageTranslator<global::Demo.PartnerOrder, global::Demo.Order>.Create(\"partner-orders\")", generated.Source);
            ScenarioExpect.Contains(".PreserveHeaders(true)", generated.Source);
            ScenarioExpect.Contains(".TranslateWith(static (message, context) => Translate(message, context));", generated.Source);
            ScenarioExpect.Contains("builder.DropHeader(\"raw-signature\");", generated.Source);
            ScenarioExpect.Contains("builder.SetHeader(\"content-type\", \"application/vnd.demo.order+json\");", generated.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid message translator declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Message_Translator_Declarations()
        => Given("invalid message translator declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.Messaging;
                namespace Demo;
                [GenerateMessageTranslator(typeof(string), typeof(int))]
                public static class Host;
                """),
            Compile("""
                using PatternKit.Generators.Messaging;
                namespace Demo;
                [GenerateMessageTranslator(typeof(string), typeof(int))]
                public static partial class Host;
                """),
            Compile("""
                using PatternKit.Generators.Messaging;
                using PatternKit.Messaging;
                namespace Demo;
                [GenerateMessageTranslator(typeof(string), typeof(int))]
                public static partial class Host
                {
                    [MessageTranslatorHandler]
                    private static string Translate(Message<string> message, MessageContext context) => message.Payload;
                }
                """),
            Compile("""
                using PatternKit.Generators.Messaging;
                using PatternKit.Messaging;
                namespace Demo;
                [GenerateMessageTranslator(typeof(string), typeof(int))]
                public static partial class Host
                {
                    [MessageTranslatorHandler]
                    private int Translate(Message<string> message, MessageContext context) => 1;
                }
                """),
            Compile("""
                using PatternKit.Generators.Messaging;
                using PatternKit.Messaging;
                namespace Demo;
                [GenerateMessageTranslator(typeof(string), typeof(int))]
                public static partial class Host
                {
                    [MessageTranslatorHandler]
                    private static int Translate(Message<string> message) => 1;
                }
                """),
            Compile("""
                using PatternKit.Generators.Messaging;
                using PatternKit.Messaging;
                namespace Demo;
                [GenerateMessageTranslator(typeof(string), typeof(int))]
                public static partial class Host
                {
                    [MessageTranslatorHandler]
                    private static int Translate(Message<int> message, MessageContext context) => 1;
                }
                """),
            Compile("""
                using PatternKit.Generators.Messaging;
                using PatternKit.Messaging;
                namespace Demo;
                [GenerateMessageTranslator(typeof(string), typeof(int))]
                public static partial class Host
                {
                    [MessageTranslatorHandler]
                    private static int Translate<T>(Message<string> message, MessageContext context) => 1;
                }
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKMT001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKMT002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKMT003");
            ScenarioExpect.Contains(results[3].Diagnostics, diagnostic => diagnostic.Id == "PKMT003");
            ScenarioExpect.Contains(results[4].Diagnostics, diagnostic => diagnostic.Id == "PKMT003");
            ScenarioExpect.Contains(results[5].Diagnostics, diagnostic => diagnostic.Id == "PKMT003");
            ScenarioExpect.Contains(results[6].Diagnostics, diagnostic => diagnostic.Id == "PKMT003");
        })
        .AssertPassed();

    [Scenario("Generates translator defaults inside nested hosts")]
    [Fact]
    public Task Generates_Translator_Defaults_Inside_Nested_Hosts()
        => Given("a nested message translator declaration", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace Demo;
            public static partial class IntegrationModule
            {
                internal abstract partial class Translators
                {
                    [GenerateMessageTranslator(typeof(string), typeof(int), TranslatorName = "translate\"" + "\\order", PreserveHeaders = false)]
                    [MessageTranslatorDropHeader("")]
                    [MessageTranslatorHeader("", "ignored")]
                    [MessageTranslatorHeader("x-source", "partner\"" + "\\feed")]
                    private sealed partial class OrderTranslator
                    {
                        [MessageTranslatorHandler]
                        private static int Translate(Message<string> message, MessageContext context) => message.Payload.Length;
                    }
                }
            }
            """))
        .Then("the generated source preserves host shape and configured headers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var generated = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class IntegrationModule", generated.Source);
            ScenarioExpect.Contains("internal abstract partial class Translators", generated.Source);
            ScenarioExpect.Contains("private sealed partial class OrderTranslator", generated.Source);
            ScenarioExpect.Contains("Create(\"translate\\\"\\\\order\")", generated.Source);
            ScenarioExpect.Contains(".PreserveHeaders(false)", generated.Source);
            ScenarioExpect.DoesNotContain("DropHeader(\"\")", generated.Source);
            ScenarioExpect.DoesNotContain("SetHeader(\"\"", generated.Source);
            ScenarioExpect.Contains("builder.SetHeader(\"x-source\", \"partner\\\"\\\\feed\");", generated.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips message translator generation for malformed type arguments")]
    [Fact]
    public Task Skips_Message_Translator_Generation_For_Malformed_Type_Arguments()
        => Given("message translator declarations with unresolved input and output types", () => new[]
        {
            Compile("""
                using PatternKit.Generators.Messaging;
                [GenerateMessageTranslator(typeof(MissingInput), typeof(int))]
                public static partial class MissingInputTranslator;
                """),
            Compile("""
                using PatternKit.Generators.Messaging;
                [GenerateMessageTranslator(typeof(string), typeof(MissingOutput))]
                public static partial class MissingOutputTranslator;
                """)
        })
        .Then("no generated sources are produced by the generator", results =>
        {
            foreach (var result in results)
            {
                ScenarioExpect.Empty(result.Diagnostics);
                ScenarioExpect.Empty(result.GeneratedSources);
                ScenarioExpect.False(result.EmitSuccess);
            }
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "MessageTranslatorGeneratorTests",
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(Message<>).Assembly.Location)
            ]);
        _ = RoslynTestHelpers.Run(compilation, new MessageTranslatorGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => new GeneratedSource(source.HintName, source.SourceText.ToString())).ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(MessageTranslatorGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<GeneratedSource> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);

    private sealed record GeneratedSource(string HintName, string Source);
}
