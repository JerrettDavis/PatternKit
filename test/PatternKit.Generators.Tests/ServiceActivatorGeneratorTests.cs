using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ServiceActivatorGeneratorTests
{
    [Scenario("GeneratesServiceActivatorFactory")]
    [Fact]
    public void GeneratesServiceActivatorFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record Request(string Sku);
            public sealed record Response(string Sku);
            [GenerateServiceActivator(typeof(Request), typeof(Response), FactoryName = "Build", ActivatorName = "inventory")]
            public static partial class InventoryActivator
            {
                [ServiceActivatorHandler]
                private static Message<Response> Handle(Message<Request> request, MessageContext context)
                    => Message<Response>.Create(new Response(request.Payload.Sku));
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesServiceActivatorFactory));
        _ = RoslynTestHelpers.Run(comp, new ServiceActivatorGenerator(), out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("ServiceActivator<global::MyApp.Request, global::MyApp.Response>", text);
        ScenarioExpect.Contains(".Handle(Handle)", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("ReportsServiceActivatorDiagnostics")]
    [Theory]
    [InlineData("public static class InventoryActivator { }", "PKSVA001")]
    [InlineData("public static partial class InventoryActivator { }", "PKSVA002")]
    public void ReportsServiceActivatorDiagnostics(string declaration, string expected)
    {
        var source = $$"""
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record Request(string Sku);
            public sealed record Response(string Sku);
            [GenerateServiceActivator(typeof(Request), typeof(Response))]
            {{declaration}}
            """;

        var comp = CreateCompilation(source, nameof(ReportsServiceActivatorDiagnostics) + expected);
        _ = RoslynTestHelpers.Run(comp, new ServiceActivatorGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    [Scenario("ReportsInvalidServiceActivatorHandler")]
    [Fact]
    public void ReportsInvalidServiceActivatorHandler()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record Request(string Sku);
            public sealed record Response(string Sku);
            [GenerateServiceActivator(typeof(Request), typeof(Response))]
            public static partial class InventoryActivator
            {
                [ServiceActivatorHandler]
                private static string Handle(Message<Request> request, MessageContext context) => "bad";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsInvalidServiceActivatorHandler));
        _ = RoslynTestHelpers.Run(comp, new ServiceActivatorGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKSVA003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.Activation.ServiceActivator<,>).Assembly.Location));
}
