using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class DynamicRouterGeneratorTests
{
    [Scenario("GeneratesDynamicRouterFactory")]
    [Fact]
    public void GeneratesDynamicRouterFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateDynamicRouter(typeof(Order), typeof(string), FactoryName = "Build")]
            public static partial class OrderRouter
            {
                private static bool IsWholesale(Message<Order> message, MessageContext context)
                    => message.Payload.Channel == "wholesale";

                [DynamicRoute("wholesale", 10, nameof(IsWholesale))]
                private static string Wholesale(Message<Order> message, MessageContext context)
                    => "wholesale";

                [DynamicRouteDefault]
                private static string Default(Message<Order> message, MessageContext context)
                    => "default";
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesDynamicRouterFactory));
        _ = RoslynTestHelpers.Run(comp, new DynamicRouterGenerator(), out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderRouter.DynamicRouter.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("DynamicRouter<global::MyApp.Order, string>", text);
        ScenarioExpect.Contains(".When(@\"wholesale\", 10, IsWholesale).Then(Wholesale)", text);
        ScenarioExpect.Contains(".Default(Default)", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ReportsDynamicRouterDiagnostics")]
    [Theory]
    [InlineData("public static class OrderRouter { }", "PKDR001")]
    [InlineData("public static partial class OrderRouter { }", "PKDR002")]
    public void ReportsDynamicRouterDiagnostics(string declaration, string expected)
    {
        var source = $$"""
            using PatternKit.Generators.Messaging;

            namespace MyApp;
            public sealed record Order(string Channel);
            [GenerateDynamicRouter(typeof(Order), typeof(string))]
            {{declaration}}
            """;

        var comp = CreateCompilation(source, nameof(ReportsDynamicRouterDiagnostics) + expected);
        _ = RoslynTestHelpers.Run(comp, new DynamicRouterGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    [Scenario("ReportsInvalidDynamicRoute")]
    [Fact]
    public void ReportsInvalidDynamicRoute()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;
            public sealed record Order(string Channel);
            [GenerateDynamicRouter(typeof(Order), typeof(string))]
            public static partial class OrderRouter
            {
                [DynamicRoute("bad", 10, "Missing")]
                private static int Bad(Order order) => 1;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsInvalidDynamicRoute));
        _ = RoslynTestHelpers.Run(comp, new DynamicRouterGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKDR003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Routing.DynamicRouter<,>).Assembly.Location));
}
