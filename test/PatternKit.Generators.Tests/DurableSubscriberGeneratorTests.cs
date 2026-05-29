using Microsoft.CodeAnalysis;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.Consumers;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Durable Subscriber generator")]
public sealed partial class DurableSubscriberGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits durable subscriber factory")]
    [Fact]
    public Task Generator_Emits_Durable_Subscriber_Factory()
        => Given("a valid durable subscriber declaration", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.Consumers;
            using PatternKit.Messaging.Storage;

            namespace Demo;

            public sealed record Order(string Id);

            [GenerateDurableSubscriber(typeof(Order), FactoryName = "Build", SubscriberName = "shipping-projection")]
            public static partial class ShippingSubscriber
            {
                [DurableSubscriberHandler("project")]
                private static DurableSubscriberHandlerResult Project(StoredMessage<Order> message, MessageContext context)
                    => DurableSubscriberHandlerResult.Success("project");
            }
            """))
        .Then("generated source creates the subscriber", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class ShippingSubscriber", source);
            ScenarioExpect.Contains("DurableSubscriber<global::Demo.Order>", source);
            ScenarioExpect.Contains(".From(store)", source);
            ScenarioExpect.Contains(".TrackWith(checkpoints)", source);
            ScenarioExpect.Contains(".Handle(@\"project\", Project)", source);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator reports invalid durable subscriber declarations")]
    [Theory]
    [InlineData("public static class ShippingSubscriber { }", "PKDS001")]
    [InlineData("public static partial class ShippingSubscriber { }", "PKDS002")]
    [InlineData("public static partial class ShippingSubscriber { [DurableSubscriberHandler(\"project\")] private static int Project(Order order) => 1; }", "PKDS003")]
    [InlineData("public partial class ShippingSubscriber { [DurableSubscriberHandler(\"project\")] private DurableSubscriberHandlerResult Project(StoredMessage<Order> message, MessageContext context) => DurableSubscriberHandlerResult.Success(\"project\"); }", "PKDS003")]
    [InlineData("public static partial class ShippingSubscriber { [DurableSubscriberHandler(\"project\")] private static DurableSubscriberHandlerResult Project(StoredMessage<Order> message) => DurableSubscriberHandlerResult.Success(\"project\"); }", "PKDS003")]
    [InlineData("public static partial class ShippingSubscriber { [DurableSubscriberHandler(\"project\")] private static DurableSubscriberHandlerResult Project(StoredMessage<string> message, MessageContext context) => DurableSubscriberHandlerResult.Success(\"project\"); }", "PKDS003")]
    [InlineData("public static partial class ShippingSubscriber { [DurableSubscriberHandler(\"project\")] private static DurableSubscriberHandlerResult Project(StoredMessage<Order> message, string context) => DurableSubscriberHandlerResult.Success(\"project\"); }", "PKDS003")]
    public Task Generator_Reports_Invalid_Durable_Subscriber_Declarations(string declaration, string expected)
        => Given("an invalid durable subscriber declaration", () => Compile($$"""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.Consumers;
            using PatternKit.Messaging.Storage;

            namespace Demo;
            public sealed record Order(string Id);
            [GenerateDurableSubscriber(typeof(Order))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == expected))
        .AssertPassed();

    [Scenario("Generator emits durable subscriber defaults and host shapes")]
    [Fact]
    public Task Generator_Emits_Durable_Subscriber_Defaults_And_Host_Shapes()
        => Given("durable subscriber declarations using default names and different host shapes", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.Consumers;
            using PatternKit.Messaging.Storage;

            namespace Demo;

            public sealed record Order(string Id);

            [GenerateDurableSubscriber(typeof(Order))]
            internal abstract partial class AbstractSubscriber
            {
                [DurableSubscriberHandler("project")]
                private static DurableSubscriberHandlerResult Project(StoredMessage<Order> message, MessageContext context)
                    => DurableSubscriberHandlerResult.Success("project");
            }

            [GenerateDurableSubscriber(typeof(Order))]
            public sealed partial class SealedSubscriber
            {
                [DurableSubscriberHandler("project")]
                private static DurableSubscriberHandlerResult Project(StoredMessage<Order> message, MessageContext context)
                    => DurableSubscriberHandlerResult.Success("project");
            }

            [GenerateDurableSubscriber(typeof(Order))]
            internal partial struct StructSubscriber
            {
                [DurableSubscriberHandler("project")]
                private static DurableSubscriberHandlerResult Project(StoredMessage<Order> message, MessageContext context)
                    => DurableSubscriberHandlerResult.Success("project");
            }
            """))
        .Then("generated sources preserve host shape and default names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractSubscriber", combined);
            ScenarioExpect.Contains("public sealed partial class SealedSubscriber", combined);
            ScenarioExpect.Contains("internal partial struct StructSubscriber", combined);
            ScenarioExpect.Contains("Create(@\"durable-subscriber\")", combined);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator emits nested durable subscriber host wrappers")]
    [Fact]
    public Task Generator_Emits_Nested_Durable_Subscriber_Host_Wrappers()
        => Given("a nested durable subscriber declaration", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.Consumers;
            using PatternKit.Messaging.Storage;

            namespace Demo;

            public sealed record Order(string Id);

            public partial class SubscriberContainer
            {
                private partial class PrivateHost
                {
                    [GenerateDurableSubscriber(typeof(Order))]
                    protected partial class ProtectedSubscriber
                    {
                        [DurableSubscriberHandler("protected")]
                        private static DurableSubscriberHandlerResult Project(StoredMessage<Order> message, MessageContext context)
                            => DurableSubscriberHandlerResult.Success("protected");
                    }

                    [GenerateDurableSubscriber(typeof(Order))]
                    private protected partial class PrivateProtectedSubscriber
                    {
                        [DurableSubscriberHandler("private-protected")]
                        private static DurableSubscriberHandlerResult Project(StoredMessage<Order> message, MessageContext context)
                            => DurableSubscriberHandlerResult.Success("private-protected");
                    }

                    [GenerateDurableSubscriber(typeof(Order))]
                    protected internal partial class ProtectedInternalSubscriber
                    {
                        [DurableSubscriberHandler("protected-internal")]
                        private static DurableSubscriberHandlerResult Project(StoredMessage<Order> message, MessageContext context)
                            => DurableSubscriberHandlerResult.Success("protected-internal");
                    }
                }
            }
            """))
        .Then("generated source preserves containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var source = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class SubscriberContainer", source);
            ScenarioExpect.Contains("private partial class PrivateHost", source);
            ScenarioExpect.Contains("protected partial class ProtectedSubscriber", source);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedSubscriber", source);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalSubscriber", source);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator skips malformed durable subscriber type arguments")]
    [Fact]
    public Task Generator_Skips_Malformed_Durable_Subscriber_Type_Arguments()
        => Given("a durable subscriber declaration with a null type argument", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.Consumers;
            using PatternKit.Messaging.Storage;

            public sealed record Order(string Id);

            [GenerateDurableSubscriber(null!)]
            public static partial class ShippingSubscriber
            {
                [DurableSubscriberHandler("project")]
                private static DurableSubscriberHandlerResult Project(StoredMessage<Order> message, MessageContext context)
                    => DurableSubscriberHandlerResult.Success("project");
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "DurableSubscriberGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(DurableSubscriber<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new DurableSubscriberGenerator(), out var run, out var updated);
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
