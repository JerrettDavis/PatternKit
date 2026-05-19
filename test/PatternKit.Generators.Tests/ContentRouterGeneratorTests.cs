using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ContentRouterGeneratorTests
{
    [Scenario("GeneratesContentRouterFactory")]
    [Fact]
    public void GeneratesContentRouterFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateContentRouter(typeof(Order), typeof(string), FactoryName = "Build")]
            public static partial class OrderRouter
            {
                private static bool IsWholesale(Message<Order> message, MessageContext context)
                    => message.Payload.Channel == "wholesale";

                private static bool IsRetail(Message<Order> message, MessageContext context)
                    => message.Payload.Channel == "retail";

                [ContentRoute("retail", 20, nameof(IsRetail))]
                private static string Retail(Message<Order> message, MessageContext context) => "retail";

                [ContentRoute("wholesale", 10, nameof(IsWholesale))]
                private static string Wholesale(Message<Order> message, MessageContext context) => "wholesale";

                [ContentRouteDefault]
                private static string Default(Message<Order> message, MessageContext context) => "default";
            }

            public static class Demo
            {
                public static string Run()
                    => OrderRouter.Build().Route(Message<Order>.Create(new Order("wholesale")));
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesContentRouterFactory));
        var gen = new ContentRouterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderRouter.ContentRouter.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains(".When(IsWholesale).Then(Wholesale)", text);
        ScenarioExpect.Contains(".When(IsRetail).Then(Retail)", text);
        ScenarioExpect.Contains(".Default(Default)", text);
        ScenarioExpect.True(text.IndexOf("IsWholesale", StringComparison.Ordinal) < text.IndexOf("IsRetail", StringComparison.Ordinal));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ReportsDiagnosticForNonPartialRouter")]
    [Fact]
    public void ReportsDiagnosticForNonPartialRouter()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateContentRouter(typeof(Order), typeof(string))]
            public static class OrderRouter
            {
                private static bool IsRetail(Message<Order> message, MessageContext context) => true;

                [ContentRoute("retail", 10, nameof(IsRetail))]
                private static string Retail(Message<Order> message, MessageContext context) => "retail";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialRouter));
        var gen = new ContentRouterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKCR001", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForMissingRoutes")]
    [Fact]
    public void ReportsDiagnosticForMissingRoutes()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateContentRouter(typeof(Order), typeof(string))]
            public static partial class OrderRouter;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForMissingRoutes));
        var gen = new ContentRouterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKCR002", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForInvalidRouteSignature")]
    [Fact]
    public void ReportsDiagnosticForInvalidRouteSignature()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateContentRouter(typeof(Order), typeof(string))]
            public static partial class OrderRouter
            {
                private static bool IsRetail(Message<Order> message, MessageContext context) => true;

                [ContentRoute("retail", 10, nameof(IsRetail))]
                private static int Retail(Message<Order> message, MessageContext context) => 1;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidRouteSignature));
        var gen = new ContentRouterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKCR003", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForInvalidDefaultSignature")]
    [Fact]
    public void ReportsDiagnosticForInvalidDefaultSignature()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateContentRouter(typeof(Order), typeof(string))]
            public static partial class OrderRouter
            {
                private static bool IsRetail(Message<Order> message, MessageContext context) => true;

                [ContentRoute("retail", 10, nameof(IsRetail))]
                private static string Retail(Message<Order> message, MessageContext context) => "retail";

                [ContentRouteDefault]
                private static int Default(Message<Order> message, MessageContext context) => 1;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidDefaultSignature));
        var gen = new ContentRouterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKCR004", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForDuplicateRouteNameOrOrder")]
    [Fact]
    public void ReportsDiagnosticForDuplicateRouteNameOrOrder()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateContentRouter(typeof(Order), typeof(string))]
            public static partial class OrderRouter
            {
                private static bool IsRetail(Message<Order> message, MessageContext context) => true;
                private static bool IsWholesale(Message<Order> message, MessageContext context) => true;

                [ContentRoute("retail", 10, nameof(IsRetail))]
                private static string Retail(Message<Order> message, MessageContext context) => "retail";

                [ContentRoute("wholesale", 10, nameof(IsWholesale))]
                private static string Wholesale(Message<Order> message, MessageContext context) => "wholesale";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForDuplicateRouteNameOrOrder));
        var gen = new ContentRouterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKCR005", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Message<>).Assembly.Location));
}
