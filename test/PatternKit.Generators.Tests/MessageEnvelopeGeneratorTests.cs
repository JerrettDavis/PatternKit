using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Message Envelope generator")]
public sealed partial class MessageEnvelopeGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates typed envelope and context factories")]
    [Fact]
    public Task Generates_Typed_Envelope_And_Context_Factories()
        => Given("a typed message envelope declaration", () => Compile("""
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
            """))
        .Then("generated source creates the configured envelope", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var generated = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Equal("OrderAcceptedEnvelope.MessageEnvelope.g.cs", generated.HintName);
            ScenarioExpect.Contains("public static partial class OrderAcceptedEnvelope", generated.Source);
            ScenarioExpect.Contains("CreateAccepted(global::MyApp.OrderAccepted payload, string messageId, string correlationId, string tenantId)", generated.Source);
            ScenarioExpect.Contains(".WithHeader(\"tenant-id\", tenantId)", generated.Source);
            ScenarioExpect.Contains("ContextFor(global::PatternKit.Messaging.Message<global::MyApp.OrderAccepted> message", generated.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid message envelope declarations")]
    [Theory]
    [InlineData("[GenerateMessageEnvelope(typeof(OrderAccepted))] [MessageEnvelopeHeader(\"message-id\", typeof(string))] public static class OrderAcceptedEnvelope;", "PKME001")]
    [InlineData("[GenerateMessageEnvelope(typeof(OrderAccepted))] public static partial class OrderAcceptedEnvelope;", "PKME002")]
    [InlineData("[GenerateMessageEnvelope(typeof(OrderAccepted))] [MessageEnvelopeHeader(\"\", typeof(string))] public static partial class OrderAcceptedEnvelope;", "PKME003")]
    [InlineData("[GenerateMessageEnvelope(typeof(OrderAccepted))] [MessageEnvelopeHeader(\"tenant-id\", null!)] public static partial class OrderAcceptedEnvelope;", "PKME003")]
    [InlineData("[GenerateMessageEnvelope(typeof(OrderAccepted))] [MessageEnvelopeHeader(\"tenant-id\", typeof(string), ParameterName = \"class\")] public static partial class OrderAcceptedEnvelope;", "PKME003")]
    [InlineData("[GenerateMessageEnvelope(typeof(OrderAccepted))] [MessageEnvelopeHeader(\"tenant-id\", typeof(string), ParameterName = \"tenantId\")] [MessageEnvelopeHeader(\"Tenant-Id\", typeof(string), ParameterName = \"tenant\")] public static partial class OrderAcceptedEnvelope;", "PKME004")]
    [InlineData("[GenerateMessageEnvelope(typeof(OrderAccepted))] [MessageEnvelopeHeader(\"tenant-id\", typeof(string), ParameterName = \"tenantId\")] [MessageEnvelopeHeader(\"customer-id\", typeof(string), ParameterName = \"tenantId\")] public static partial class OrderAcceptedEnvelope;", "PKME004")]
    public Task Reports_Diagnostics_For_Invalid_Message_Envelope_Declarations(string declaration, string diagnosticId)
        => Given("an invalid message envelope declaration", () => Compile($$"""
            using PatternKit.Generators.Messaging;
            public sealed record OrderAccepted(string OrderId);
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generates message envelope defaults and host shapes")]
    [Fact]
    public Task Generates_Message_Envelope_Defaults_And_Host_Shapes()
        => Given("message envelope declarations with default names and different host shapes", () => Compile("""
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record OrderAccepted(string OrderId);

            [GenerateMessageEnvelope(typeof(OrderAccepted))]
            [MessageEnvelopeHeader("message-id", typeof(string))]
            internal abstract partial class AbstractEnvelope;

            [GenerateMessageEnvelope(typeof(OrderAccepted))]
            [MessageEnvelopeHeader("tenant-id", typeof(string), ParameterName = "tenantId")]
            public sealed partial class SealedEnvelope;

            [GenerateMessageEnvelope(typeof(OrderAccepted))]
            [MessageEnvelopeHeader("customer-id", typeof(string), ParameterName = "customerId")]
            internal partial struct StructEnvelope;
            """))
        .Then("generated sources preserve host shape and default factories", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources.Select(static source => source.Source));
            ScenarioExpect.Contains("internal abstract partial class AbstractEnvelope", combined);
            ScenarioExpect.Contains("public sealed partial class SealedEnvelope", combined);
            ScenarioExpect.Contains("internal partial struct StructEnvelope", combined);
            ScenarioExpect.Contains("Create(global::MyApp.OrderAccepted payload", combined);
            ScenarioExpect.Contains("CreateContext(global::PatternKit.Messaging.Message<global::MyApp.OrderAccepted> message", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested message envelope host wrappers")]
    [Fact]
    public Task Generates_Nested_Message_Envelope_Host_Wrappers()
        => Given("nested message envelope declarations", () => Compile("""
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record OrderAccepted(string OrderId);

            public partial class EnvelopeContainer
            {
                private partial class PrivateHost
                {
                    [GenerateMessageEnvelope(typeof(OrderAccepted))]
                    [MessageEnvelopeHeader("protected-id", typeof(string), ParameterName = "protectedId")]
                    protected partial class ProtectedEnvelope;

                    [GenerateMessageEnvelope(typeof(OrderAccepted))]
                    [MessageEnvelopeHeader("private-protected-id", typeof(string), ParameterName = "privateProtectedId")]
                    private protected partial class PrivateProtectedEnvelope;

                    [GenerateMessageEnvelope(typeof(OrderAccepted))]
                    [MessageEnvelopeHeader("protected-internal-id", typeof(string), ParameterName = "protectedInternalId")]
                    protected internal partial class ProtectedInternalEnvelope;
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources.Select(static source => source.Source));
            ScenarioExpect.Contains("public partial class EnvelopeContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedEnvelope", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedEnvelope", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalEnvelope", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed message envelope type arguments")]
    [Fact]
    public Task Skips_Malformed_Message_Envelope_Type_Arguments()
        => Given("a message envelope declaration with a null type argument", () => Compile("""
            using PatternKit.Generators.Messaging;
            [GenerateMessageEnvelope(null!)]
            [MessageEnvelopeHeader("message-id", typeof(string))]
            public static partial class OrderAcceptedEnvelope;
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = CreateCompilation(source, "MessageEnvelopeGeneratorTests");
        _ = RoslynTestHelpers.Run(compilation, new MessageEnvelopeGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources
                .Select(static source => new GeneratedSource(source.HintName, source.SourceText.ToString()))
                .ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Message<>).Assembly.Location));

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<GeneratedSource> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);

    private sealed record GeneratedSource(string HintName, string Source);
}
