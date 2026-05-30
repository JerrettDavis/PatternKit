using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.Reliability;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Dead Letter Channel generator")]
public sealed partial class DeadLetterChannelGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates dead letter channel factory")]
    [Fact]
    public Task Generates_Dead_Letter_Channel_Factory()
        => Given("a configured dead letter channel declaration", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging.Reliability;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateDeadLetterChannel(
                typeof(Order),
                FactoryName = "BuildChannel",
                ChannelName = "checkout-dead",
                Source = "checkout.fulfillment",
                IdPrefix = "checkout-dead",
                IncludeExceptionDetails = false)]
            public static partial class CheckoutDeadLetters
            {
                [DeadLetterStoreFactory]
                private static IDeadLetterStore<Order> CreateStore() => new InMemoryDeadLetterStore<Order>();
            }
            """))
        .Then("generated source creates the configured channel", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Equal("CheckoutDeadLetters.DeadLetterChannel.g.cs", source.HintName);
            ScenarioExpect.Contains("public static partial class CheckoutDeadLetters", source.Source);
            ScenarioExpect.Contains("BuildChannel", source.Source);
            ScenarioExpect.Contains(".FromSource(\"checkout.fulfillment\")", source.Source);
            ScenarioExpect.Contains(".UseStore(CreateStore())", source.Source);
            ScenarioExpect.Contains("\"checkout-dead:\"", source.Source);
            ScenarioExpect.Contains(".IncludeExceptionDetails(false)", source.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid dead letter channel declarations")]
    [Theory]
    [InlineData("public static class CheckoutDeadLetters;", "PKDL001")]
    [InlineData("public static partial class CheckoutDeadLetters;", "PKDL002")]
    [InlineData("public static partial class CheckoutDeadLetters { [DeadLetterStoreFactory] private static IDeadLetterStore<Order> One() => new InMemoryDeadLetterStore<Order>(); [DeadLetterStoreFactory] private static IDeadLetterStore<Order> Two() => new InMemoryDeadLetterStore<Order>(); }", "PKDL002")]
    [InlineData("public static partial class CheckoutDeadLetters { [DeadLetterStoreFactory] private static string CreateStore() => \"bad\"; }", "PKDL003")]
    [InlineData("public static partial class CheckoutDeadLetters { [DeadLetterStoreFactory] private IDeadLetterStore<Order> CreateStore() => new InMemoryDeadLetterStore<Order>(); }", "PKDL003")]
    [InlineData("public static partial class CheckoutDeadLetters { [DeadLetterStoreFactory] private static IDeadLetterStore<Order> CreateStore(string name) => new InMemoryDeadLetterStore<Order>(); }", "PKDL003")]
    [InlineData("public static partial class CheckoutDeadLetters { [DeadLetterStoreFactory] private static IDeadLetterStore<string> CreateStore() => new InMemoryDeadLetterStore<string>(); }", "PKDL003")]
    public Task Reports_Diagnostics_For_Invalid_Dead_Letter_Channel_Declarations(string declaration, string diagnosticId)
        => Given("an invalid dead letter channel declaration", () => Compile($$"""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging.Reliability;
            public sealed record Order(string Id);
            [GenerateDeadLetterChannel(typeof(Order))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generates dead letter channel defaults and host shapes")]
    [Fact]
    public Task Generates_Dead_Letter_Channel_Defaults_And_Host_Shapes()
        => Given("dead letter channel declarations with default names and host shapes", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging.Reliability;
            namespace MyApp;
            public sealed record Order(string Id);

            [GenerateDeadLetterChannel(typeof(Order))]
            internal abstract partial class AbstractDeadLetters
            {
                [DeadLetterStoreFactory]
                private static IDeadLetterStore<Order> Store() => new InMemoryDeadLetterStore<Order>();
            }

            [GenerateDeadLetterChannel(typeof(Order), ChannelName = "tenant\\\"dead", Source = "checkout\\\"api", IdPrefix = "tenant\\\"dead")]
            public sealed partial class SealedDeadLetters
            {
                [DeadLetterStoreFactory]
                private static IDeadLetterStore<Order> Store() => new InMemoryDeadLetterStore<Order>();
            }

            [GenerateDeadLetterChannel(typeof(Order))]
            internal partial struct StructDeadLetters
            {
                [DeadLetterStoreFactory]
                private static IDeadLetterStore<Order> Store() => new InMemoryDeadLetterStore<Order>();
            }
            """))
        .Then("generated sources preserve host shape and configured defaults", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources.Select(static source => source.Source));
            ScenarioExpect.Contains("internal abstract partial class AbstractDeadLetters", combined);
            ScenarioExpect.Contains("public sealed partial class SealedDeadLetters", combined);
            ScenarioExpect.Contains("internal partial struct StructDeadLetters", combined);
            ScenarioExpect.Contains("Create(\"dead-letter-channel\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"dead\")", combined);
            ScenarioExpect.Contains(".FromSource(\"application\")", combined);
            ScenarioExpect.Contains(".FromSource(\"checkout\\\\\\\"api\")", combined);
            ScenarioExpect.Contains(".IncludeExceptionDetails(true)", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested dead letter channel host wrappers")]
    [Fact]
    public Task Generates_Nested_Dead_Letter_Channel_Host_Wrappers()
        => Given("nested dead letter channel declarations", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging.Reliability;
            namespace MyApp;
            public sealed record Order(string Id);

            public partial class DeadLetterContainer
            {
                private partial class PrivateHost
                {
                    [GenerateDeadLetterChannel(typeof(Order))]
                    protected partial class ProtectedDeadLetters
                    {
                        [DeadLetterStoreFactory]
                        private static IDeadLetterStore<Order> Store() => new InMemoryDeadLetterStore<Order>();
                    }

                    [GenerateDeadLetterChannel(typeof(Order))]
                    private protected partial class PrivateProtectedDeadLetters
                    {
                        [DeadLetterStoreFactory]
                        private static IDeadLetterStore<Order> Store() => new InMemoryDeadLetterStore<Order>();
                    }

                    [GenerateDeadLetterChannel(typeof(Order))]
                    protected internal partial class ProtectedInternalDeadLetters
                    {
                        [DeadLetterStoreFactory]
                        private static IDeadLetterStore<Order> Store() => new InMemoryDeadLetterStore<Order>();
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources.Select(static source => source.Source));
            ScenarioExpect.Contains("public partial class DeadLetterContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedDeadLetters", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedDeadLetters", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalDeadLetters", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed dead letter channel payload type")]
    [Fact]
    public Task Skips_Malformed_Dead_Letter_Channel_Payload_Type()
        => Given("a dead letter channel declaration with a null payload type", () => Compile("""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging.Reliability;
            [GenerateDeadLetterChannel(null!)]
            public static partial class CheckoutDeadLetters
            {
                [DeadLetterStoreFactory]
                private static IDeadLetterStore<string> Store() => new InMemoryDeadLetterStore<string>();
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = CreateCompilation(source, "DeadLetterChannelGeneratorTests");
        _ = RoslynTestHelpers.Run(compilation, new DeadLetterChannelGenerator(), out var run, out var updated);
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
            extra: MetadataReference.CreateFromFile(typeof(DeadLetterChannel<>).Assembly.Location));

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<GeneratedSource> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);

    private sealed record GeneratedSource(string HintName, string Source);
}
