using Microsoft.CodeAnalysis;
using PatternKit.Application.CompensatingTransactions;
using PatternKit.Generators.CompensatingTransactions;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Compensating Transaction generator")]
public sealed partial class CompensatingTransactionGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates compensating transaction factory")]
    [Fact]
    public Task Generates_Compensating_Transaction_Factory()
        => Given("an attributed transaction with ordered steps", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.CompensatingTransactions;

            public sealed record CheckoutContext(bool RequiresReview);

            [GenerateCompensatingTransaction(FactoryMethodName = "Build", TransactionName = "checkout")]
            public static partial class CheckoutTransaction
            {
                [CompensatingTransactionStep("capture", 20, Compensation = nameof(Refund))]
                private static ValueTask Capture(CheckoutContext context, CancellationToken cancellationToken) => default;

                private static ValueTask Refund(CheckoutContext context, CancellationToken cancellationToken) => default;

                [CompensatingTransactionStep("reserve", 10, Compensation = nameof(Release), Condition = nameof(ShouldReserve))]
                private static ValueTask Reserve(CheckoutContext context, CancellationToken cancellationToken) => default;

                private static bool ShouldReserve(CheckoutContext context) => true;

                private static ValueTask Release(CheckoutContext context, CancellationToken cancellationToken) => default;
            }
            """))
            .Then("the generated factory wires steps in order", result =>
            {
                ScenarioExpect.Empty(result.Diagnostics);
                var source = ScenarioExpect.Single(result.GeneratedSources);
                ScenarioExpect.Contains("CompensatingTransaction<global::CheckoutContext> Build()", source);
                ScenarioExpect.Contains("CompensatingTransaction<global::CheckoutContext>.Create(\"checkout\")", source);
                ScenarioExpect.Contains(".AddStep(\"reserve\"", source);
                ScenarioExpect.Contains(".When(static context => ShouldReserve(context))", source);
                ScenarioExpect.True(source.IndexOf("\"reserve\"", StringComparison.Ordinal) < source.IndexOf("\"capture\"", StringComparison.Ordinal));
                ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
            })
            .AssertPassed();

    [Scenario("Reports diagnostics for invalid compensating transaction declarations")]
    [Theory]
    [InlineData("[GenerateCompensatingTransaction] public static class Host;", "PKCOMP001")]
    [InlineData("[GenerateCompensatingTransaction] public static partial class Host;", "PKCOMP002")]
    [InlineData("[GenerateCompensatingTransaction(FactoryMethodName = \"class\")] public static partial class Host { [CompensatingTransactionStep(\"x\", 1, Compensation = nameof(Undo))] private static ValueTask Do(Ctx c, CancellationToken ct) => default; private static ValueTask Undo(Ctx c, CancellationToken ct) => default; }", "PKCOMP005")]
    public Task Reports_Diagnostics_For_Invalid_Compensating_Transaction_Declarations(string declaration, string diagnosticId)
        => Given("an invalid compensating transaction declaration", () => Compile($$"""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.CompensatingTransactions;
            public sealed class Ctx;
            {{declaration}}
            """))
            .Then("the matching diagnostic is reported", result =>
                ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
            .AssertPassed();

    [Scenario("Reports diagnostics for invalid compensating transaction steps")]
    [Theory]
    [InlineData("[CompensatingTransactionStep(\"x\", 1, Compensation = nameof(Undo))] private static Task Do(Ctx c, CancellationToken ct) => Task.CompletedTask; private static ValueTask Undo(Ctx c, CancellationToken ct) => default;")]
    [InlineData("[CompensatingTransactionStep(\"x\", 1, Compensation = nameof(Undo))] private ValueTask Do(Ctx c, CancellationToken ct) => default; private static ValueTask Undo(Ctx c, CancellationToken ct) => default;")]
    [InlineData("[CompensatingTransactionStep(\"x\", 1)] private static ValueTask Do(Ctx c, CancellationToken ct) => default;")]
    [InlineData("[CompensatingTransactionStep(\"x\", 1, Compensation = nameof(Missing))] private static ValueTask Do(Ctx c, CancellationToken ct) => default;")]
    [InlineData("[CompensatingTransactionStep(\"x\", 1, Compensation = nameof(Undo), Condition = nameof(BadWhen))] private static ValueTask Do(Ctx c, CancellationToken ct) => default; private static ValueTask Undo(Ctx c, CancellationToken ct) => default; private static bool BadWhen() => true;")]
    [InlineData("[CompensatingTransactionStep(\"x\", 1, Compensation = nameof(Undo), Condition = nameof(BadWhen))] private static ValueTask Do(Ctx c, CancellationToken ct) => default; private static ValueTask Undo(Ctx c, CancellationToken ct) => default; private bool BadWhen(Ctx c) => true;")]
    public Task Reports_Diagnostics_For_Invalid_Compensating_Transaction_Steps(string members)
        => Given("a compensating transaction with invalid steps", () => Compile($$"""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.CompensatingTransactions;
            public sealed class Ctx;
            [GenerateCompensatingTransaction]
            public static partial class Host { {{members}} }
            """))
            .Then("the invalid step diagnostic is reported", result =>
                ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKCOMP003"))
            .AssertPassed();

    [Scenario("Reports diagnostics for duplicate compensating transaction step identity")]
    [Fact]
    public Task Reports_Diagnostics_For_Duplicate_Compensating_Transaction_Step_Identity()
        => Given("a compensating transaction with duplicate step names", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.CompensatingTransactions;
            public sealed class Ctx;
            [GenerateCompensatingTransaction]
            public static partial class Host
            {
                [CompensatingTransactionStep("x", 1, Compensation = nameof(Undo))]
                private static ValueTask Do(Ctx c, CancellationToken ct) => default;
                [CompensatingTransactionStep("x", 2, Compensation = nameof(Undo))]
                private static ValueTask DoAgain(Ctx c, CancellationToken ct) => default;
                private static ValueTask Undo(Ctx c, CancellationToken ct) => default;
            }
            """))
            .Then("the duplicate diagnostic is reported", result =>
                ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKCOMP004"))
            .AssertPassed();

    [Scenario("Generates nested record host wrappers")]
    [Fact]
    public Task Generates_Nested_Record_Host_Wrappers()
        => Given("a nested record host", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.CompensatingTransactions;
            namespace Demo;
            public sealed class Ctx;
            public partial record class Container
            {
                [GenerateCompensatingTransaction]
                protected partial record class Host
                {
                    [CompensatingTransactionStep("x", 1, Compensation = nameof(Undo))]
                    private static ValueTask Do(Ctx c, CancellationToken ct) => default;
                    private static ValueTask Undo(Ctx c, CancellationToken ct) => default;
                }
            }
            """))
            .Then("the generated source preserves record wrappers", result =>
            {
                ScenarioExpect.Empty(result.Diagnostics);
                var combined = string.Join("\n", result.GeneratedSources);
                ScenarioExpect.Contains("public partial record class Container", combined);
                ScenarioExpect.Contains("protected partial record class Host", combined);
                ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
            })
            .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "CompensatingTransactionGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(CompensatingTransaction<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new CompensatingTransactionGenerator(), out var run, out var updated);
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
