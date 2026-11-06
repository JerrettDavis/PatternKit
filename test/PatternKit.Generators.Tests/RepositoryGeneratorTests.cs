using Microsoft.CodeAnalysis;
using PatternKit.Application.Repository;
using PatternKit.Generators.Repository;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Repository generator")]
public sealed partial class RepositoryGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits repository factory")]
    [Fact]
    public Task Generator_Emits_Repository_Factory()
        => Given("a valid repository declaration", () => Compile("""
            using PatternKit.Generators.Repository;

            namespace Demo;

            public sealed record Order(string Id);

            [GenerateRepository(typeof(Order), typeof(string), FactoryName = "Build")]
            public static partial class OrderRepositoryFactory
            {
                [RepositoryKeySelector]
                private static string SelectKey(Order order) => order.Id;
            }
            """))
        .Then("generated source creates the repository", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class OrderRepositoryFactory", source);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("InMemoryRepository<global::Demo.Order, string>.Create(SelectKey).Build()", source);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator reports invalid repository declarations")]
    [Theory]
    [InlineData("public static class OrderRepositoryFactory { [RepositoryKeySelector] private static string SelectKey(Order order) => order.Id; }", "PKREP001")]
    [InlineData("public static partial class OrderRepositoryFactory;", "PKREP002")]
    [InlineData("public static partial class OrderRepositoryFactory { [RepositoryKeySelector] private static string One(Order order) => order.Id; [RepositoryKeySelector] private static string Two(Order order) => order.Id; }", "PKREP002")]
    [InlineData("public partial class OrderRepositoryFactory { [RepositoryKeySelector] private string SelectKey(Order order) => order.Id; }", "PKREP003")]
    [InlineData("public static partial class OrderRepositoryFactory { [RepositoryKeySelector] private static T SelectKey<T>(Order order) => default!; }", "PKREP003")]
    [InlineData("public static partial class OrderRepositoryFactory { [RepositoryKeySelector] private static string SelectKey() => string.Empty; }", "PKREP003")]
    [InlineData("public static partial class OrderRepositoryFactory { [RepositoryKeySelector] private static string SelectKey(Order order, string tenant) => order.Id; }", "PKREP003")]
    [InlineData("public static partial class OrderRepositoryFactory { [RepositoryKeySelector] private static string SelectKey(string order) => order; }", "PKREP003")]
    [InlineData("public static partial class OrderRepositoryFactory { [RepositoryKeySelector] private static int SelectKey(Order order) => 1; }", "PKREP003")]
    public Task Generator_Reports_Invalid_Repository_Declarations(string declaration, string diagnosticId)
        => Given("an invalid repository declaration", () => Compile($$"""
            using PatternKit.Generators.Repository;
            public sealed record Order(string Id);
            [GenerateRepository(typeof(Order), typeof(string))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generator emits repository defaults and type shapes")]
    [Fact]
    public Task Generator_Emits_Repository_Defaults_And_Type_Shapes()
        => Given("repository declarations using default names and different host shapes", () => Compile("""
            using PatternKit.Generators.Repository;

            namespace Demo;

            public sealed record Order(string Id);

            [GenerateRepository(typeof(Order), typeof(string))]
            internal abstract partial class AbstractRepositoryFactory
            {
                [RepositoryKeySelector]
                private static string SelectKey(Order order) => order.Id;
            }

            [GenerateRepository(typeof(Order), typeof(string))]
            public sealed partial class SealedRepositoryFactory
            {
                [RepositoryKeySelector]
                private static string SelectKey(Order order) => order.Id;
            }

            [GenerateRepository(typeof(Order), typeof(string))]
            internal partial struct StructRepositoryFactory
            {
                [RepositoryKeySelector]
                private static string SelectKey(Order order) => order.Id;
            }
            """))
        .Then("generated sources preserve host shape and default factory names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractRepositoryFactory", combined);
            ScenarioExpect.Contains("InMemoryRepository<global::Demo.Order, string> Create()", combined);
            ScenarioExpect.Contains("public sealed partial class SealedRepositoryFactory", combined);
            ScenarioExpect.Contains("internal partial struct StructRepositoryFactory", combined);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator emits nested repository host wrappers")]
    [Fact]
    public Task Generator_Emits_Nested_Repository_Host_Wrappers()
        => Given("nested repository declarations with non-public accessibility", () => Compile("""
            using PatternKit.Generators.Repository;

            namespace Demo;

            public sealed record Order(string Id);

            public partial class RepositoryContainer
            {
                private partial class PrivateHost
                {
                    [GenerateRepository(typeof(Order), typeof(string))]
                    protected partial class ProtectedRepository
                    {
                        [RepositoryKeySelector]
                        private static string SelectKey(Order order) => order.Id;
                    }

                    [GenerateRepository(typeof(Order), typeof(string))]
                    private protected partial class PrivateProtectedRepository
                    {
                        [RepositoryKeySelector]
                        private static string SelectKey(Order order) => order.Id;
                    }

                    [GenerateRepository(typeof(Order), typeof(string))]
                    protected internal partial class ProtectedInternalRepository
                    {
                        [RepositoryKeySelector]
                        private static string SelectKey(Order order) => order.Id;
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class RepositoryContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedRepository", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedRepository", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalRepository", combined);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator skips malformed repository type arguments")]
    [Theory]
    [InlineData("null!", "typeof(string)")]
    [InlineData("typeof(Order)", "null!")]
    public Task Generator_Skips_Malformed_Repository_Type_Arguments(string entityType, string keyType)
        => Given("a repository declaration with a null type argument", () => Compile($$"""
            using PatternKit.Generators.Repository;

            public sealed record Order(string Id);

            [GenerateRepository({{entityType}}, {{keyType}})]
            public static partial class OrderRepositoryFactory
            {
                [RepositoryKeySelector]
                private static string SelectKey(Order order) => order.Id;
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "RepositoryGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(InMemoryRepository<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new RepositoryGenerator(), out var run, out var updated);
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
