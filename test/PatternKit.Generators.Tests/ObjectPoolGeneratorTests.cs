using Microsoft.CodeAnalysis;
using PatternKit.Generators.ObjectPool;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Object Pool generator")]
public sealed partial class ObjectPoolGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates object pool factory")]
    [Fact]
    public Task Generates_Object_Pool_Factory()
        => Given("an object pool declaration", () => Compile("""
            using PatternKit.Generators.ObjectPool;
            namespace Demo;

            public sealed class Buffer
            {
                public Buffer() { }
                public void Reset() { }
            }

            [GenerateObjectPool(typeof(Buffer), FactoryMethodName = "Build", MaxRetained = 4, ResetMethodName = nameof(Buffer.Reset))]
            public static partial class BufferPools;
            """))
        .Then("the generated source creates the configured pool", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class BufferPools", source);
            ScenarioExpect.Contains("ObjectPool<global::Demo.Buffer> Build()", source);
            ScenarioExpect.Contains(".WithFactory(static () => new global::Demo.Buffer())", source);
            ScenarioExpect.Contains("builder.OnReturn(static item => item.Reset());", source);
            ScenarioExpect.Contains("builder.WithMaxRetained(4);", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid object pool declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Object_Pool_Declarations()
        => Given("invalid object pool declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.ObjectPool;
                public sealed class Buffer { public Buffer() { } }
                [GenerateObjectPool(typeof(Buffer))]
                public static class BufferPools;
                """),
            Compile("""
                using PatternKit.Generators.ObjectPool;
                public sealed class Buffer { public Buffer() { } }
                [GenerateObjectPool(typeof(Buffer), FactoryMethodName = "")]
                public static partial class BufferPools;
                """),
            Compile("""
                using PatternKit.Generators.ObjectPool;
                public sealed class Buffer { public Buffer() { } }
                [GenerateObjectPool(typeof(Buffer), MaxRetained = -2)]
                public static partial class BufferPools;
                """),
            Compile("""
                using PatternKit.Generators.ObjectPool;
                public sealed class Buffer
                {
                    public Buffer(string value) { }
                }
                [GenerateObjectPool(typeof(Buffer))]
                public static partial class BufferPools;
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKOP001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKOP002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKOP002");
            ScenarioExpect.Contains(results[3].Diagnostics, diagnostic => diagnostic.Id == "PKOP003");
        })
        .AssertPassed();

    [Scenario("Generates object pool defaults for nested value type hosts")]
    [Fact]
    public Task Generates_Object_Pool_Defaults_For_Nested_Value_Type_Hosts()
        => Given("a nested object pool declaration", () => Compile("""
            using PatternKit.Generators.ObjectPool;
            namespace Demo;

            public static partial class Modules
            {
                internal abstract partial class Pools
                {
                    [GenerateObjectPool(typeof(System.Guid))]
                    private sealed partial class GuidPool;
                }
            }
            """))
        .Then("generated sources preserve containing partial wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class Modules", source);
            ScenarioExpect.Contains("internal abstract partial class Pools", source);
            ScenarioExpect.Contains("private sealed partial class GuidPool", source);
            ScenarioExpect.Contains("ObjectPool<global::System.Guid> Create()", source);
            ScenarioExpect.DoesNotContain("WithMaxRetained", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "ObjectPoolGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Creational.ObjectPool.ObjectPool<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new ObjectPoolGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);
}
