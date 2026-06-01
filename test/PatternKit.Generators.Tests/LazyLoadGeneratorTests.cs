using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.LazyLoading;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Lazy Load generator")]
public sealed partial class LazyLoadGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates lazy load factory")]
    [Fact]
    public Task Generates_Lazy_Load_Factory()
        => Given("a configured lazy load declaration", () => Compile("""
            using PatternKit.Generators.LazyLoading;
            using System.Threading;
            using System.Threading.Tasks;
            namespace Demo;
            [GenerateLazyLoad(typeof(string), FactoryMethodName = "Build", LoaderMethodName = "FetchAsync", LazyLoadName = "profile", TimeToLiveMilliseconds = 250)]
            public static partial class ProfileLazyLoad
            {
                public static ValueTask<string> FetchAsync(CancellationToken ct) => new("customer");
            }
            """))
        .Then("generated source creates the configured lazy load", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Equal("ProfileLazyLoad.LazyLoad.g.cs", source.HintName);
            ScenarioExpect.Contains("Build()", source.Source);
            ScenarioExpect.Contains("LazyLoad<string>.Create(\"profile\")", source.Source);
            ScenarioExpect.Contains(".LoadWith(FetchAsync)", source.Source);
            ScenarioExpect.Contains("WithTimeToLive(global::System.TimeSpan.FromMilliseconds(250))", source.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid lazy load declarations")]
    [Theory]
    [InlineData("public static class LazyHost { public static ValueTask<string> LoadAsync(CancellationToken ct) => new(\"x\"); }", "PKLL001")]
    [InlineData("public static partial class LazyHost { public static ValueTask<string> LoadAsync(CancellationToken ct) => new(\"x\"); }", "PKLL002", "TimeToLiveMilliseconds = -1")]
    [InlineData("public static partial class LazyHost { public static ValueTask<string> LoadAsync(CancellationToken ct) => new(\"x\"); }", "PKLL003", "FactoryMethodName = \"class\"")]
    [InlineData("public static partial class LazyHost { public static ValueTask<string> LoadAsync(CancellationToken ct) => new(\"x\"); }", "PKLL003", "LoaderMethodName = \"1bad\"")]
    public Task Reports_Diagnostics_For_Invalid_Lazy_Load_Declarations(string declaration, string diagnosticId, string configuration = "")
        => Given("an invalid lazy load declaration", () => Compile($$"""
            using PatternKit.Generators.LazyLoading;
            using System.Threading;
            using System.Threading.Tasks;
            [GenerateLazyLoad(typeof(string){{(string.IsNullOrWhiteSpace(configuration) ? "" : ", " + configuration)}})]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generates lazy load defaults and host shapes")]
    [Fact]
    public Task Generates_Lazy_Load_Defaults_And_Host_Shapes()
        => Given("lazy load declarations with default names and host shapes", () => Compile("""
            using PatternKit.Generators.LazyLoading;
            using System.Threading;
            using System.Threading.Tasks;
            namespace Demo;

            [GenerateLazyLoad(typeof(string))]
            internal abstract partial class AbstractLazy
            {
                public static ValueTask<string> LoadAsync(CancellationToken ct) => new("a");
            }

            [GenerateLazyLoad(typeof(string), LazyLoadName = "tenant\\\"profile", CacheEnabled = false)]
            public sealed partial class SealedLazy
            {
                public static ValueTask<string> LoadAsync(CancellationToken ct) => new("s");
            }

            [GenerateLazyLoad(typeof(int))]
            internal partial struct StructLazy
            {
                public static ValueTask<int> LoadAsync(CancellationToken ct) => new(1);
            }
            """))
        .Then("generated sources preserve host shape and configured defaults", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);
            var combined = string.Join("\n", result.GeneratedSources.Select(static source => source.Source));
            ScenarioExpect.Contains("internal abstract partial class AbstractLazy", combined);
            ScenarioExpect.Contains("public sealed partial class SealedLazy", combined);
            ScenarioExpect.Contains("internal partial struct StructLazy", combined);
            ScenarioExpect.Contains("Create(\"lazy-load\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"profile\")", combined);
            ScenarioExpect.Contains("builder.DisableCache();", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed lazy load value type")]
    [Fact]
    public Task Skips_Malformed_Lazy_Load_Value_Type()
        => Given("a lazy load declaration with a null value type", () => Compile("""
            using PatternKit.Generators.LazyLoading;
            [GenerateLazyLoad(null!)]
            public static partial class LazyHost;
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    [Scenario("Lazy load attribute exposes generator configuration")]
    [Fact]
    public void Lazy_Load_Attribute_Exposes_Generator_Configuration()
    {
        var attribute = new GenerateLazyLoadAttribute(typeof(string))
        {
            FactoryMethodName = "CreateProfile",
            LoaderMethodName = "LoadProfileAsync",
            LazyLoadName = "profile",
            CacheEnabled = false,
            TimeToLiveMilliseconds = 42
        };

        ScenarioExpect.Equal(typeof(string), attribute.ValueType);
        ScenarioExpect.Equal("CreateProfile", attribute.FactoryMethodName);
        ScenarioExpect.Equal("LoadProfileAsync", attribute.LoaderMethodName);
        ScenarioExpect.Equal("profile", attribute.LazyLoadName);
        ScenarioExpect.False(attribute.CacheEnabled);
        ScenarioExpect.Equal(42, attribute.TimeToLiveMilliseconds);
    }

    private static GeneratorResult Compile(string source)
    {
        var compilation = CreateCompilation(source, "LazyLoadGeneratorTests");
        _ = RoslynTestHelpers.Run(compilation, new LazyLoadGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => new GeneratedSource(source.HintName, source.SourceText.ToString())).ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(PatternKit.Application.LazyLoading.LazyLoad<>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(LazyLoadGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<GeneratedSource> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);

    private sealed record GeneratedSource(string HintName, string Source);
}
