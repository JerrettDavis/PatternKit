using Microsoft.CodeAnalysis;
using PatternKit.Application.UnitOfWork;
using PatternKit.Generators.UnitOfWork;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Unit of Work generator")]
public sealed partial class UnitOfWorkGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates unit of work factory")]
    [Fact]
    public Task Generates_Unit_Of_Work_Factory()
        => Given("a unit of work declaration with ordered commit and rollback steps", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.UnitOfWork;

            [GenerateUnitOfWork(FactoryName = "Build")]
            public static partial class CheckoutWork
            {
                [UnitOfWorkStep("charge", 20)]
                private static ValueTask Charge(CancellationToken ct) => default;

                [UnitOfWorkStep("reserve", 10, RollbackMethodName = nameof(UndoReserve))]
                private static ValueTask Reserve(CancellationToken ct) => default;

                private static ValueTask UndoReserve(CancellationToken ct) => default;
            }
            """))
        .Then("the generated factory enlists steps in order", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class CheckoutWork", source);
            ScenarioExpect.Contains("UnitOfWork Build()", source);
            ScenarioExpect.Contains("builder.Enlist(\"reserve\", Reserve, UndoReserve);", source);
            ScenarioExpect.Contains("builder.Enlist(\"charge\", Charge);", source);
            ScenarioExpect.True(source.IndexOf("\"reserve\"", StringComparison.Ordinal) < source.IndexOf("\"charge\"", StringComparison.Ordinal));
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostic for non-partial unit of work declarations")]
    [Fact]
    public Task Reports_Diagnostic_For_Non_Partial_Unit_Of_Work_Declarations()
        => Given("a non-partial unit of work declaration", () => Compile("""
            using PatternKit.Generators.UnitOfWork;

            [GenerateUnitOfWork]
            public static class CheckoutWork;
            """))
        .Then("the diagnostic identifies the host", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKUOW001"))
        .AssertPassed();

    [Scenario("Reports diagnostic for missing unit of work steps")]
    [Fact]
    public Task Reports_Diagnostic_For_Missing_Unit_Of_Work_Steps()
        => Given("a unit of work declaration without steps", () => Compile("""
            using PatternKit.Generators.UnitOfWork;

            [GenerateUnitOfWork]
            public static partial class CheckoutWork;
            """))
        .Then("the diagnostic identifies the missing steps", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKUOW002"))
        .AssertPassed();

    [Scenario("Reports diagnostic for duplicate unit of work step identity")]
    [Theory]
    [InlineData("""
        [UnitOfWorkStep("reserve", 10)]
        private static ValueTask Reserve(CancellationToken ct) => default;
        [UnitOfWorkStep("reserve", 20)]
        private static ValueTask Charge(CancellationToken ct) => default;
        """)]
    [InlineData("""
        [UnitOfWorkStep("reserve", 10)]
        private static ValueTask Reserve(CancellationToken ct) => default;
        [UnitOfWorkStep("charge", 10)]
        private static ValueTask Charge(CancellationToken ct) => default;
        """)]
    public Task Reports_Diagnostic_For_Duplicate_Unit_Of_Work_Step_Identity(string steps)
        => Given("a unit of work declaration with duplicate step names or orders", () => Compile($$"""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.UnitOfWork;

            [GenerateUnitOfWork]
            public static partial class CheckoutWork
            {
                {{steps}}
            }
            """))
        .Then("the duplicate diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKUOW004"))
        .AssertPassed();

    [Scenario("Reports diagnostic for invalid unit of work step signatures")]
    [Theory]
    [InlineData("[UnitOfWorkStep(\"reserve\", 10)] private ValueTask Reserve(CancellationToken ct) => default;")]
    [InlineData("[UnitOfWorkStep(\"reserve\", 10)] private static ValueTask Reserve<T>(CancellationToken ct) => default;")]
    [InlineData("[UnitOfWorkStep(\"reserve\", 10)] private static Task Reserve(CancellationToken ct) => Task.CompletedTask;")]
    [InlineData("[UnitOfWorkStep(\"reserve\", 10)] private static ValueTask Reserve() => default;")]
    [InlineData("[UnitOfWorkStep(\"reserve\", 10)] private static ValueTask Reserve(CancellationToken ct, string tenant) => default;")]
    [InlineData("[UnitOfWorkStep(\"reserve\", 10)] private static ValueTask Reserve(string ct) => default;")]
    public Task Reports_Diagnostic_For_Invalid_Unit_Of_Work_Step_Signatures(string step)
        => Given("a unit of work declaration with an invalid commit step", () => Compile($$"""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.UnitOfWork;

            [GenerateUnitOfWork]
            public static partial class CheckoutWork
            {
                {{step}}
            }
            """))
        .Then("the invalid step diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKUOW003"))
        .AssertPassed();

    [Scenario("Reports diagnostic for invalid unit of work rollback signatures")]
    [Fact]
    public Task Reports_Diagnostic_For_Invalid_Unit_Of_Work_Rollback_Signatures()
        => Given("a unit of work declaration with an invalid rollback step", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.UnitOfWork;

            [GenerateUnitOfWork]
            public static partial class CheckoutWork
            {
                [UnitOfWorkStep("reserve", 10, RollbackMethodName = nameof(UndoReserve))]
                private static ValueTask Reserve(CancellationToken ct) => default;

                private static void UndoReserve() { }
            }
            """))
        .Then("the invalid step diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKUOW003"))
        .AssertPassed();

    [Scenario("Generates unit of work defaults and type shapes")]
    [Fact]
    public Task Generates_Unit_Of_Work_Defaults_And_Type_Shapes()
        => Given("unit of work declarations using default names and different host shapes", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.UnitOfWork;

            namespace Demo;

            [GenerateUnitOfWork]
            internal abstract partial class AbstractWork
            {
                [UnitOfWorkStep("abstract\\\"step", 10)]
                private static ValueTask Execute(CancellationToken ct) => default;
            }

            [GenerateUnitOfWork]
            public sealed partial class SealedWork
            {
                [UnitOfWorkStep("sealed", 10)]
                private static ValueTask Execute(CancellationToken ct) => default;
            }

            [GenerateUnitOfWork]
            internal partial struct StructWork
            {
                [UnitOfWorkStep("struct", 10)]
                private static ValueTask Execute(CancellationToken ct) => default;
            }
            """))
        .Then("generated sources preserve host shape and default factory names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractWork", combined);
            ScenarioExpect.Contains("UnitOfWork Create()", combined);
            ScenarioExpect.Contains("builder.Enlist(\"abstract\\\\\\\"step\", Execute);", combined);
            ScenarioExpect.Contains("public sealed partial class SealedWork", combined);
            ScenarioExpect.Contains("internal partial struct StructWork", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested unit of work host wrappers")]
    [Fact]
    public Task Generates_Nested_Unit_Of_Work_Host_Wrappers()
        => Given("nested unit of work declarations with non-public accessibility", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.UnitOfWork;

            namespace Demo;

            public partial class WorkContainer
            {
                private partial class PrivateHost
                {
                    [GenerateUnitOfWork]
                    protected partial class ProtectedWork
                    {
                        [UnitOfWorkStep("protected", 10)]
                        private static ValueTask Execute(CancellationToken ct) => default;
                    }

                    [GenerateUnitOfWork]
                    private protected partial class PrivateProtectedWork
                    {
                        [UnitOfWorkStep("private-protected", 10)]
                        private static ValueTask Execute(CancellationToken ct) => default;
                    }

                    [GenerateUnitOfWork]
                    protected internal partial class ProtectedInternalWork
                    {
                        [UnitOfWorkStep("protected-internal", 10)]
                        private static ValueTask Execute(CancellationToken ct) => default;
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class WorkContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedWork", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedWork", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalWork", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "UnitOfWorkGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(global::PatternKit.Application.UnitOfWork.UnitOfWork).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new UnitOfWorkGenerator(), out var run, out var updated);
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
