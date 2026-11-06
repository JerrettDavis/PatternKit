using Microsoft.CodeAnalysis;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.Routing;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Dynamic Router generator")]
public sealed partial class DynamicRouterGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits dynamic router factory")]
    [Fact]
    public Task Generator_Emits_Dynamic_Router_Factory()
        => Given("a valid dynamic router declaration", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace Demo;

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
            """))
        .Then("generated source creates the router", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class OrderRouter", source);
            ScenarioExpect.Contains("DynamicRouter<global::Demo.Order, string>", source);
            ScenarioExpect.Contains(".When(@\"wholesale\", 10, IsWholesale).Then(Wholesale)", source);
            ScenarioExpect.Contains(".Default(Default)", source);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator reports invalid dynamic router declarations")]
    [Theory]
    [InlineData("public static class OrderRouter { }", "PKDR001")]
    [InlineData("public static partial class OrderRouter { }", "PKDR002")]
    [InlineData("public static partial class OrderRouter { [DynamicRoute(\"bad\", 10, \"Missing\")] private static int Bad(Order order) => 1; }", "PKDR003")]
    [InlineData("public static partial class OrderRouter { private static bool IsWholesale(Message<Order> message, MessageContext context) => true; [DynamicRoute(\" \", 10, nameof(IsWholesale))] private static string Wholesale(Message<Order> message, MessageContext context) => \"wholesale\"; }", "PKDR003")]
    [InlineData("public static partial class OrderRouter { private static bool IsWholesale(Message<Order> message, MessageContext context) => true; [DynamicRoute(\"wholesale\", 10, \" \")] private static string Wholesale(Message<Order> message, MessageContext context) => \"wholesale\"; }", "PKDR003")]
    [InlineData("public static partial class OrderRouter { private static string IsWholesale(Message<Order> message, MessageContext context) => \"yes\"; [DynamicRoute(\"wholesale\", 10, nameof(IsWholesale))] private static string Wholesale(Message<Order> message, MessageContext context) => \"wholesale\"; }", "PKDR003")]
    [InlineData("public static partial class OrderRouter { private static bool IsWholesale(Message<Order> message) => true; [DynamicRoute(\"wholesale\", 10, nameof(IsWholesale))] private static string Wholesale(Message<Order> message, MessageContext context) => \"wholesale\"; }", "PKDR003")]
    [InlineData("public static partial class OrderRouter { private static bool IsWholesale(Message<Order> message, MessageContext context) => true; [DynamicRoute(\"wholesale\", 10, nameof(IsWholesale))] private string Wholesale(Message<Order> message, MessageContext context) => \"wholesale\"; }", "PKDR003")]
    [InlineData("public static partial class OrderRouter { private static bool IsWholesale(Message<Order> message, MessageContext context) => true; [DynamicRoute(\"wholesale\", 10, nameof(IsWholesale))] private static int Wholesale(Message<Order> message, MessageContext context) => 1; }", "PKDR003")]
    [InlineData("public static partial class OrderRouter { private static bool IsWholesale(Message<Order> message, MessageContext context) => true; [DynamicRoute(\"wholesale\", 10, nameof(IsWholesale))] private static string Wholesale(Message<Order> message, MessageContext context) => \"wholesale\"; [DynamicRouteDefault] private static int Default(Message<Order> message, MessageContext context) => 1; }", "PKDR004")]
    [InlineData("public static partial class OrderRouter { private static bool IsWholesale(Message<Order> message, MessageContext context) => true; [DynamicRoute(\"one\", 10, nameof(IsWholesale))] private static string One(Message<Order> message, MessageContext context) => \"one\"; [DynamicRoute(\"one\", 20, nameof(IsWholesale))] private static string Two(Message<Order> message, MessageContext context) => \"two\"; }", "PKDR005")]
    [InlineData("public static partial class OrderRouter { private static bool IsWholesale(Message<Order> message, MessageContext context) => true; [DynamicRoute(\"one\", 10, nameof(IsWholesale))] private static string One(Message<Order> message, MessageContext context) => \"one\"; [DynamicRoute(\"two\", 10, nameof(IsWholesale))] private static string Two(Message<Order> message, MessageContext context) => \"two\"; }", "PKDR005")]
    public Task Generator_Reports_Invalid_Dynamic_Router_Declarations(string declaration, string expected)
        => Given("an invalid dynamic router declaration", () => Compile($$"""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace Demo;
            public sealed record Order(string Channel);
            [GenerateDynamicRouter(typeof(Order), typeof(string))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == expected))
        .AssertPassed();

    [Scenario("Generator emits dynamic router host shapes")]
    [Fact]
    public Task Generator_Emits_Dynamic_Router_Host_Shapes()
        => Given("dynamic router declarations using different host shapes", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace Demo;

            public sealed record Order(string Channel);

            [GenerateDynamicRouter(typeof(Order), typeof(string))]
            internal abstract partial class AbstractRouter
            {
                private static bool Matches(Message<Order> message, MessageContext context) => true;
                [DynamicRoute("all", 1, nameof(Matches))]
                private static string Route(Message<Order> message, MessageContext context) => "all";
            }

            [GenerateDynamicRouter(typeof(Order), typeof(string))]
            public sealed partial class SealedRouter
            {
                private static bool Matches(Message<Order> message, MessageContext context) => true;
                [DynamicRoute("all", 1, nameof(Matches))]
                private static string Route(Message<Order> message, MessageContext context) => "all";
            }

            [GenerateDynamicRouter(typeof(Order), typeof(string))]
            internal partial struct StructRouter
            {
                private static bool Matches(Message<Order> message, MessageContext context) => true;
                [DynamicRoute("all", 1, nameof(Matches))]
                private static string Route(Message<Order> message, MessageContext context) => "all";
            }
            """))
        .Then("generated sources preserve host shape and compile", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractRouter", combined);
            ScenarioExpect.Contains("public sealed partial class SealedRouter", combined);
            ScenarioExpect.Contains("internal partial struct StructRouter", combined);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator emits nested dynamic router host wrappers")]
    [Fact]
    public Task Generator_Emits_Nested_Dynamic_Router_Host_Wrappers()
        => Given("a nested dynamic router declaration", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace Demo;

            public sealed record Order(string Channel);

            public partial class RouterContainer
            {
                private partial class PrivateHost
                {
                    [GenerateDynamicRouter(typeof(Order), typeof(string))]
                    protected partial class ProtectedRouter
                    {
                        private static bool Matches(Message<Order> message, MessageContext context) => true;
                        [DynamicRoute("protected", 1, nameof(Matches))]
                        private static string Route(Message<Order> message, MessageContext context) => "protected";
                    }

                    [GenerateDynamicRouter(typeof(Order), typeof(string))]
                    private protected partial class PrivateProtectedRouter
                    {
                        private static bool Matches(Message<Order> message, MessageContext context) => true;
                        [DynamicRoute("private-protected", 1, nameof(Matches))]
                        private static string Route(Message<Order> message, MessageContext context) => "private-protected";
                    }

                    [GenerateDynamicRouter(typeof(Order), typeof(string))]
                    protected internal partial class ProtectedInternalRouter
                    {
                        private static bool Matches(Message<Order> message, MessageContext context) => true;
                        [DynamicRoute("protected-internal", 1, nameof(Matches))]
                        private static string Route(Message<Order> message, MessageContext context) => "protected-internal";
                    }
                }
            }
            """))
        .Then("generated source preserves containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var source = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class RouterContainer", source);
            ScenarioExpect.Contains("private partial class PrivateHost", source);
            ScenarioExpect.Contains("protected partial class ProtectedRouter", source);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedRouter", source);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalRouter", source);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator skips malformed dynamic router type arguments")]
    [Theory]
    [InlineData("null!", "typeof(string)")]
    [InlineData("typeof(Order)", "null!")]
    public Task Generator_Skips_Malformed_Dynamic_Router_Type_Arguments(string payloadType, string resultType)
        => Given("a dynamic router declaration with a null type argument", () => Compile($$"""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            public sealed record Order(string Channel);

            [GenerateDynamicRouter({{payloadType}}, {{resultType}})]
            public static partial class OrderRouter
            {
                private static bool Matches(Message<Order> message, MessageContext context) => true;
                [DynamicRoute("all", 1, nameof(Matches))]
                private static string Route(Message<Order> message, MessageContext context) => "all";
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "DynamicRouterGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(DynamicRouter<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new DynamicRouterGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(),
            emit.Success,
            string.Join("\n", emit.Diagnostics));
    }

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        bool EmitSuccess,
        string EmitDiagnostics);
}
