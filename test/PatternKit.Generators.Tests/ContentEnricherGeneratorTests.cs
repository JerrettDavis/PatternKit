using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ContentEnricherGeneratorTests
{
    [Scenario("Generates content enricher factory")]
    [Fact]
    public void GeneratesContentEnricherFactory()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace Demo;

            public sealed record Customer(string Name, string? Email, string? Tier);

            [GenerateContentEnricher(typeof(Customer), FactoryName = "Build", EnricherName = "customer-enrichment", DefaultPolicy = ContentEnrichmentErrorPolicy.Skip)]
            public static partial class CustomerEnrichment
            {
                [ContentEnrichmentStep("profile", Order = 2)]
                private static ValueTask<Customer> AddProfile(Customer customer, MessageContext context, CancellationToken cancellationToken)
                    => ValueTask.FromResult(customer with { Email = "user@example.com" });

                [ContentEnrichmentStep("tier", Order = 1, Policy = ContentEnrichmentErrorPolicy.UseDefault, DefaultFactoryName = nameof(DefaultTier))]
                private static ValueTask<Customer> AddTier(Customer customer, MessageContext context, CancellationToken cancellationToken)
                    => ValueTask.FromResult(customer with { Tier = "Gold" });

                private static Customer DefaultTier(Customer customer) => customer with { Tier = "Standard" };
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesContentEnricherFactory));
        var gen = new ContentEnricherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("CustomerEnrichment.ContentEnricher.g.cs", generated.HintName);
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("AsyncContentEnricher<global::Demo.Customer>.Create(\"customer-enrichment\")", text);
        ScenarioExpect.Contains(".WithDefaultPolicy(global::PatternKit.Messaging.Transformation.EnrichmentErrorPolicy.Skip)", text);
        ScenarioExpect.Contains("builder.Enrich(\"tier\"", text);
        ScenarioExpect.Contains("static payload => DefaultTier(payload)", text);
        ScenarioExpect.True(text.IndexOf("\"tier\"", StringComparison.Ordinal) < text.IndexOf("\"profile\"", StringComparison.Ordinal));
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Generates content enricher factory for global instance class")]
    [Fact]
    public void GeneratesContentEnricherFactoryForGlobalInstanceClass()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            public sealed record Payload(string Value);

            [GenerateContentEnricher(typeof(Payload), EnricherName = "value-\"enricher\"")]
            public sealed partial class Host
            {
                [ContentEnrichmentStep("trim")]
                private static ValueTask<Payload> Trim(Payload payload, MessageContext context, CancellationToken cancellationToken)
                    => ValueTask.FromResult(payload with { Value = payload.Value.Trim() });
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesContentEnricherFactoryForGlobalInstanceClass));
        var gen = new ContentEnricherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var text = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources)).SourceText.ToString();
        ScenarioExpect.DoesNotContain("namespace", text);
        ScenarioExpect.Contains("public sealed partial class Host", text);
        ScenarioExpect.Contains("Create(\"value-\\\"enricher\\\"\")", text);
        ScenarioExpect.Contains("builder.Enrich(\"trim\", static (payload, context, cancellationToken) => Trim(payload, context, cancellationToken));", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Generates content enricher factory for struct host")]
    [Fact]
    public void GeneratesContentEnricherFactoryForStructHost()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace Demo;

            public readonly record struct Payload(string Value);

            [GenerateContentEnricher(typeof(Payload), DefaultPolicy = ContentEnrichmentErrorPolicy.UseDefault)]
            public partial struct Host
            {
                [ContentEnrichmentStep("fallback", Policy = ContentEnrichmentErrorPolicy.UseDefault, DefaultFactoryName = nameof(Fallback))]
                private static ValueTask<Payload> Add(Payload payload, MessageContext context, CancellationToken cancellationToken)
                    => ValueTask.FromResult(payload);

                private static Payload Fallback(Payload payload) => payload;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesContentEnricherFactoryForStructHost));
        var gen = new ContentEnricherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var text = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources)).SourceText.ToString();
        ScenarioExpect.Contains("public partial struct Host", text);
        ScenarioExpect.Contains(".WithDefaultPolicy(global::PatternKit.Messaging.Transformation.EnrichmentErrorPolicy.UseDefault)", text);
        ScenarioExpect.Contains("global::PatternKit.Messaging.Transformation.EnrichmentErrorPolicy.UseDefault, static payload => Fallback(payload)", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Generates content enricher factory for abstract class host")]
    [Fact]
    public void GeneratesContentEnricherFactoryForAbstractClassHost()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace Demo;

            [GenerateContentEnricher(typeof(string))]
            public abstract partial class Host
            {
                [ContentEnrichmentStep("normalize", Policy = ContentEnrichmentErrorPolicy.Skip)]
                protected static ValueTask<string> Normalize(string payload, MessageContext context, CancellationToken cancellationToken)
                    => ValueTask.FromResult(payload.Trim());
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesContentEnricherFactoryForAbstractClassHost));
        var gen = new ContentEnricherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var text = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources)).SourceText.ToString();
        ScenarioExpect.Contains("public abstract partial class Host", text);
        ScenarioExpect.Contains("global::PatternKit.Messaging.Transformation.EnrichmentErrorPolicy.Skip", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Reports diagnostic for non-partial content enricher host")]
    [Fact]
    public void ReportsDiagnosticForNonPartialContentEnricherHost()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace Demo;

            [GenerateContentEnricher(typeof(string))]
            public static class Host;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForNonPartialContentEnricherHost));

        ScenarioExpect.Equal("PKMCE001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for content enricher without steps")]
    [Fact]
    public void ReportsDiagnosticForContentEnricherWithoutSteps()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace Demo;

            [GenerateContentEnricher(typeof(string))]
            public static partial class Host;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForContentEnricherWithoutSteps));

        ScenarioExpect.Equal("PKMCE002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid content enricher step")]
    [Fact]
    public void ReportsDiagnosticForInvalidContentEnricherStep()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace Demo;

            [GenerateContentEnricher(typeof(string))]
            public static partial class Host
            {
                [ContentEnrichmentStep("invalid")]
                private static string Add(string payload) => payload;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidContentEnricherStep));

        ScenarioExpect.Equal("PKMCE003", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for missing default content enricher factory")]
    [Fact]
    public void ReportsDiagnosticForMissingDefaultContentEnricherFactory()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace Demo;

            [GenerateContentEnricher(typeof(string))]
            public static partial class Host
            {
                [ContentEnrichmentStep("invalid", Policy = ContentEnrichmentErrorPolicy.UseDefault)]
                private static ValueTask<string> Add(string payload, MessageContext context, CancellationToken cancellationToken)
                    => ValueTask.FromResult(payload);
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForMissingDefaultContentEnricherFactory));

        ScenarioExpect.Equal("PKMCE004", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for blank content enricher configuration")]
    [Fact]
    public void ReportsDiagnosticForBlankContentEnricherConfiguration()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace Demo;

            [GenerateContentEnricher(typeof(string), FactoryName = " ")]
            public static partial class Host;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForBlankContentEnricherConfiguration));

        ScenarioExpect.Equal("PKMCE004", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid default content enricher policy")]
    [Fact]
    public void ReportsDiagnosticForInvalidDefaultContentEnricherPolicy()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace Demo;

            [GenerateContentEnricher(typeof(string), DefaultPolicy = (ContentEnrichmentErrorPolicy)99)]
            public static partial class Host;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidDefaultContentEnricherPolicy));

        ScenarioExpect.Equal("PKMCE004", diagnostic.Id);
        ScenarioExpect.Contains("default policy '99' is not supported", diagnostic.GetMessage());
    }

    [Scenario("Reports diagnostic for invalid content enricher step policy")]
    [Fact]
    public void ReportsDiagnosticForInvalidContentEnricherStepPolicy()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace Demo;

            [GenerateContentEnricher(typeof(string))]
            public static partial class Host
            {
                [ContentEnrichmentStep("invalid", Policy = (ContentEnrichmentErrorPolicy)99)]
                private static ValueTask<string> Add(string payload, MessageContext context, CancellationToken cancellationToken)
                    => ValueTask.FromResult(payload);
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidContentEnricherStepPolicy));

        ScenarioExpect.Equal("PKMCE004", diagnostic.Id);
        ScenarioExpect.Contains("step 'invalid' policy '99' is not supported", diagnostic.GetMessage());
    }

    [Scenario("Reports diagnostic for invalid default content enricher factory")]
    [Fact]
    public void ReportsDiagnosticForInvalidDefaultContentEnricherFactory()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace Demo;

            [GenerateContentEnricher(typeof(string))]
            public static partial class Host
            {
                [ContentEnrichmentStep("invalid", Policy = ContentEnrichmentErrorPolicy.UseDefault, DefaultFactoryName = nameof(Default))]
                private static ValueTask<string> Add(string payload, MessageContext context, CancellationToken cancellationToken)
                    => ValueTask.FromResult(payload);

                private static int Default(string payload) => payload.Length;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidDefaultContentEnricherFactory));

        ScenarioExpect.Equal("PKMCE004", diagnostic.Id);
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
            Path.GetDirectoryName(typeof(ContentEnricherGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new ContentEnricherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
    }
}
