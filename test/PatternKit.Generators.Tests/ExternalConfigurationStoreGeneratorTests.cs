using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Cloud.ExternalConfigurationStore;
using PatternKit.Generators.Cloud;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ExternalConfigurationStoreGeneratorTests
{
    [Scenario("GeneratesExternalConfigurationStoreFactory")]
    [Fact]
    public void GeneratesExternalConfigurationStoreFactory()
    {
        var source = """
            using PatternKit.Cloud.ExternalConfigurationStore;
            using PatternKit.Generators.Cloud;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace MyApp;

            public sealed record AppSettings(string Endpoint);

            [GenerateExternalConfigurationStore(typeof(AppSettings), FactoryName = "Build", StoreName = "tenant-config", CacheMilliseconds = 1000)]
            public static partial class AppConfigStore
            {
                [ExternalConfigurationLoader]
                private static ValueTask<ExternalConfigurationSnapshot<AppSettings>> Load(CancellationToken cancellationToken)
                    => new(new ExternalConfigurationSnapshot<AppSettings>(new AppSettings("https://api.example.com"), "v1", DateTimeOffset.UtcNow));

                [ExternalConfigurationValidator("Endpoint is required.", 10)]
                private static bool HasEndpoint(AppSettings settings) => !string.IsNullOrWhiteSpace(settings.Endpoint);
            }

            public static class Demo
            {
                public static ValueTask<ExternalConfigurationResult<AppSettings>> Run()
                    => AppConfigStore.Build().GetAsync();
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesExternalConfigurationStoreFactory));
        var gen = new ExternalConfigurationStoreGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("AppConfigStore.ExternalConfigurationStore.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("ExternalConfigurationStore<global::MyApp.AppSettings>", text);
        ScenarioExpect.Contains(".LoadFrom(Load)", text);
        ScenarioExpect.Contains(".ValidateWith(@\"Endpoint is required.\", HasEndpoint)", text);
        ScenarioExpect.Contains(".CacheFor(global::System.TimeSpan.FromMilliseconds(1000))", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ReportsDiagnosticForNonPartialStore")]
    [Fact]
    public void ReportsDiagnosticForNonPartialStore()
    {
        var source = """
            using PatternKit.Cloud.ExternalConfigurationStore;
            using PatternKit.Generators.Cloud;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace MyApp;

            public sealed record AppSettings(string Endpoint);

            [GenerateExternalConfigurationStore(typeof(AppSettings))]
            public static class AppConfigStore
            {
                [ExternalConfigurationLoader]
                private static ValueTask<ExternalConfigurationSnapshot<AppSettings>> Load(CancellationToken cancellationToken)
                    => new(new ExternalConfigurationSnapshot<AppSettings>(new AppSettings("endpoint"), "v1", DateTimeOffset.UtcNow));
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialStore));
        var gen = new ExternalConfigurationStoreGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKECS001", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForMissingOrInvalidLoader")]
    [Fact]
    public void ReportsDiagnosticForMissingOrInvalidLoader()
    {
        var source = """
            using PatternKit.Generators.Cloud;

            namespace MyApp;

            public sealed record AppSettings(string Endpoint);

            [GenerateExternalConfigurationStore(typeof(AppSettings))]
            public static partial class AppConfigStore;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForMissingOrInvalidLoader));
        var gen = new ExternalConfigurationStoreGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKECS002", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForInvalidValidator")]
    [Fact]
    public void ReportsDiagnosticForInvalidValidator()
    {
        var source = """
            using PatternKit.Cloud.ExternalConfigurationStore;
            using PatternKit.Generators.Cloud;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace MyApp;

            public sealed record AppSettings(string Endpoint);

            [GenerateExternalConfigurationStore(typeof(AppSettings))]
            public static partial class AppConfigStore
            {
                [ExternalConfigurationLoader]
                private static ValueTask<ExternalConfigurationSnapshot<AppSettings>> Load(CancellationToken cancellationToken)
                    => new(new ExternalConfigurationSnapshot<AppSettings>(new AppSettings("endpoint"), "v1", DateTimeOffset.UtcNow));

                [ExternalConfigurationValidator("Endpoint is required.", 10)]
                private static string HasEndpoint(AppSettings settings) => settings.Endpoint;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidValidator));
        var gen = new ExternalConfigurationStoreGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKECS003", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForDuplicateValidatorOrder")]
    [Fact]
    public void ReportsDiagnosticForDuplicateValidatorOrder()
    {
        var source = """
            using PatternKit.Cloud.ExternalConfigurationStore;
            using PatternKit.Generators.Cloud;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace MyApp;

            public sealed record AppSettings(string Endpoint);

            [GenerateExternalConfigurationStore(typeof(AppSettings))]
            public static partial class AppConfigStore
            {
                [ExternalConfigurationLoader]
                private static ValueTask<ExternalConfigurationSnapshot<AppSettings>> Load(CancellationToken cancellationToken)
                    => new(new ExternalConfigurationSnapshot<AppSettings>(new AppSettings("endpoint"), "v1", DateTimeOffset.UtcNow));

                [ExternalConfigurationValidator("Endpoint is required.", 10)]
                private static bool HasEndpoint(AppSettings settings) => true;

                [ExternalConfigurationValidator("Endpoint is absolute.", 10)]
                private static bool IsAbsolute(AppSettings settings) => true;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForDuplicateValidatorOrder));
        var gen = new ExternalConfigurationStoreGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKECS004", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(ExternalConfigurationStore<>).Assembly.Location));
}
