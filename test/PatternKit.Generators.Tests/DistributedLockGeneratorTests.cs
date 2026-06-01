using Microsoft.CodeAnalysis;
using PatternKit.Cloud.DistributedLocks;
using PatternKit.Generators.DistributedLocks;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Distributed Lock generator")]
public sealed partial class DistributedLockGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates distributed lock factory")]
    [Fact]
    public Task Generates_Distributed_Lock_Factory()
        => Given("a distributed lock declaration", () => Compile("""
            using PatternKit.Generators.DistributedLocks;
            namespace Demo;
            [GenerateDistributedLock(typeof(string), FactoryMethodName = "Build", LockName = "orders-lock", LeaseDurationMilliseconds = 5000)]
            public static partial class OrderLocks;
            """))
        .Then("the generated source creates the configured lock", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class OrderLocks", source);
            ScenarioExpect.Contains("DistributedLock<string> Build()", source);
            ScenarioExpect.Contains("DistributedLock<string>.Create(\"orders-lock\")", source);
            ScenarioExpect.Contains(".LeaseDuration(global::System.TimeSpan.FromMilliseconds(5000))", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid distributed lock declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Distributed_Lock_Declarations()
        => Given("invalid distributed lock declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.DistributedLocks;
                [GenerateDistributedLock(typeof(string))]
                public static class OrderLocks;
                """),
            Compile("""
                using PatternKit.Generators.DistributedLocks;
                [GenerateDistributedLock(typeof(string), FactoryMethodName = "")]
                public static partial class OrderLocks;
                """),
            Compile("""
                using PatternKit.Generators.DistributedLocks;
                [GenerateDistributedLock(typeof(string), FactoryMethodName = "1x")]
                public static partial class OrderLocks;
                """),
            Compile("""
                using PatternKit.Generators.DistributedLocks;
                [GenerateDistributedLock(typeof(string), FactoryMethodName = "class")]
                public static partial class OrderLocks;
                """),
            Compile("""
                using PatternKit.Generators.DistributedLocks;
                [GenerateDistributedLock(typeof(string), LockName = "   ")]
                public static partial class OrderLocks;
                """),
            Compile("""
                using PatternKit.Generators.DistributedLocks;
                [GenerateDistributedLock(typeof(string), LeaseDurationMilliseconds = 0)]
                public static partial class OrderLocks;
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKDLOCK001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKDLOCK002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKDLOCK002");
            ScenarioExpect.Contains(results[3].Diagnostics, diagnostic => diagnostic.Id == "PKDLOCK002");
            ScenarioExpect.Contains(results[4].Diagnostics, diagnostic => diagnostic.Id == "PKDLOCK002");
            ScenarioExpect.Contains(results[5].Diagnostics, diagnostic => diagnostic.Id == "PKDLOCK002");
        })
        .AssertPassed();

    [Scenario("Generates distributed lock defaults and nested host wrappers")]
    [Fact]
    public Task Generates_Distributed_Lock_Defaults_And_Nested_Host_Wrappers()
        => Given("nested distributed lock declarations", () => Compile("""
            using PatternKit.Generators.DistributedLocks;
            namespace Demo;
            public partial class FulfillmentModule
            {
                private partial class Locks
                {
                    [GenerateDistributedLock(typeof(System.Guid), LockName = "order\\\"lock")]
                    private sealed partial class OrderLocks;

                    [GenerateDistributedLock(typeof(System.Guid))]
                    protected partial class ProtectedLocks;

                    [GenerateDistributedLock(typeof(System.Guid))]
                    private protected partial class PrivateProtectedLocks;

                    [GenerateDistributedLock(typeof(System.Guid))]
                    protected internal partial class ProtectedInternalLocks;
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(4, result.GeneratedSources.Count);
            var source = string.Join(Environment.NewLine, result.GeneratedSources);
            ScenarioExpect.Contains("public partial class FulfillmentModule", source);
            ScenarioExpect.Contains("private partial class Locks", source);
            ScenarioExpect.Contains("private sealed partial class OrderLocks", source);
            ScenarioExpect.Contains("protected partial class ProtectedLocks", source);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedLocks", source);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalLocks", source);
            ScenarioExpect.Contains("DistributedLock<global::System.Guid>.Create(\"order\\\\\\\"lock\")", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips distributed lock generation for malformed key type")]
    [Fact]
    public Task Skips_Distributed_Lock_Generation_For_Malformed_Key_Type()
        => Given("a distributed lock declaration with an unresolved key type", () => Compile("""
            using PatternKit.Generators.DistributedLocks;
            [GenerateDistributedLock(typeof(MissingKey))]
            public static partial class MissingLocks;
            """))
        .Then("no generated source is produced by the generator", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Empty(result.GeneratedSources);
            ScenarioExpect.False(result.EmitSuccess);
        })
        .AssertPassed();

    [Scenario("Generates distributed lock factory for abstract internal hosts")]
    [Fact]
    public Task Generates_Distributed_Lock_Factory_For_Abstract_Internal_Hosts()
        => Given("an abstract internal distributed lock host", () => Compile("""
            using PatternKit.Generators.DistributedLocks;
            [GenerateDistributedLock(typeof(int))]
            internal abstract partial class InternalOrderLocks;
            """))
        .Then("the generated source preserves the host shape", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class InternalOrderLocks", source);
            ScenarioExpect.Contains("DistributedLock<int>.Create(\"distributed-lock\")", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates distributed lock factory for record hosts")]
    [Fact]
    public Task Generates_Distributed_Lock_Factory_For_Record_Hosts()
        => Given("record distributed lock hosts", () => Compile("""
            using PatternKit.Generators.DistributedLocks;
            public partial record class RecordOrderLocks
            {
                [GenerateDistributedLock(typeof(int))]
                public partial record struct StructOrderLocks;
            }
            """))
        .Then("the generated source preserves record host shapes", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public partial record class RecordOrderLocks", source);
            ScenarioExpect.Contains("public partial record struct StructOrderLocks", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "DistributedLockGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(DistributedLock<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new DistributedLockGenerator(), out var run, out var updated);
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
