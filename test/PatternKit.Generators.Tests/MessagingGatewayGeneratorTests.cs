using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class MessagingGatewayGeneratorTests
{
    [Scenario("GeneratesMessagingGatewayFactory")]
    [Fact]
    public void GeneratesMessagingGatewayFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record Request(string OrderId);
            public sealed record Response(string OrderId);
            [GenerateMessagingGateway(typeof(Request), typeof(Response), FactoryName = "Build", GatewayName = "payments")]
            public static partial class PaymentGateway
            {
                [MessagingGatewayHandler]
                private static Message<Response> Handle(Message<Request> request, MessageContext context)
                    => Message<Response>.Create(new Response(request.Payload.OrderId));
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesMessagingGatewayFactory));
        _ = RoslynTestHelpers.Run(comp, new MessagingGatewayGenerator(), out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("MessagingGateway<global::MyApp.Request, global::MyApp.Response>", text);
        ScenarioExpect.Contains(".Handle(Handle)", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("ReportsMessagingGatewayDiagnostics")]
    [Theory]
    [InlineData("public static class PaymentGateway { }", "PKGWY001")]
    [InlineData("public static partial class PaymentGateway { }", "PKGWY002")]
    public void ReportsMessagingGatewayDiagnostics(string declaration, string expected)
    {
        var source = $$"""
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record Request(string OrderId);
            public sealed record Response(string OrderId);
            [GenerateMessagingGateway(typeof(Request), typeof(Response))]
            {{declaration}}
            """;

        var comp = CreateCompilation(source, nameof(ReportsMessagingGatewayDiagnostics) + expected);
        _ = RoslynTestHelpers.Run(comp, new MessagingGatewayGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    [Scenario("ReportsInvalidMessagingGatewayHandler")]
    [Fact]
    public void ReportsInvalidMessagingGatewayHandler()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record Request(string OrderId);
            public sealed record Response(string OrderId);
            [GenerateMessagingGateway(typeof(Request), typeof(Response))]
            public static partial class PaymentGateway
            {
                [MessagingGatewayHandler]
                private static string Handle(Message<Request> request, MessageContext context) => "bad";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsInvalidMessagingGatewayHandler));
        _ = RoslynTestHelpers.Run(comp, new MessagingGatewayGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKGWY003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.Gateways.MessagingGateway<,>).Assembly.Location));
}
