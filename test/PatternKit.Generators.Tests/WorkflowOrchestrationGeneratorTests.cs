using Microsoft.CodeAnalysis;
using PatternKit.Generators.WorkflowOrchestration;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Workflow Orchestration generator")]
public sealed partial class WorkflowOrchestrationGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates workflow orchestration factory from annotated methods")]
    [Fact]
    public Task Generates_Workflow_Orchestration_Factory_From_Annotated_Methods()
        => Given("a workflow orchestration declaration", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.WorkflowOrchestration;
            namespace Demo;
            public sealed class FulfillmentContext
            {
                public bool RequiresFraudReview { get; set; }
            }

            [WorkflowOrchestration(FactoryMethodName = "Build", WorkflowName = "fulfillment")]
            public static partial class FulfillmentWorkflow
            {
                [WorkflowStep("reserve-inventory", 1, Compensation = nameof(ReleaseInventory))]
                private static ValueTask ReserveInventory(FulfillmentContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

                [WorkflowStep("review-fraud", 2, Condition = nameof(RequiresReview))]
                private static ValueTask ReviewFraud(FulfillmentContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

                [WorkflowStep("capture-payment", 3, MaxAttempts = 3)]
                private static ValueTask CapturePayment(FulfillmentContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

                private static bool RequiresReview(FulfillmentContext context) => context.RequiresFraudReview;

                private static ValueTask ReleaseInventory(FulfillmentContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }
            """))
        .Then("the generated source builds the workflow with retries conditions and compensation", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("WorkflowOrchestrator<global::Demo.FulfillmentContext> Build()", source);
            ScenarioExpect.Contains("WorkflowOrchestrator<global::Demo.FulfillmentContext>.Create(\"fulfillment\")", source);
            ScenarioExpect.Contains(".AddStep(\"reserve-inventory\"", source);
            ScenarioExpect.Contains(".Compensate(static (context, cancellationToken) => ReleaseInventory(context, cancellationToken))", source);
            ScenarioExpect.Contains(".When(static context => RequiresReview(context))", source);
            ScenarioExpect.Contains(".WithMaxAttempts(3)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid workflow orchestration declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Workflow_Orchestration_Declarations()
        => Given("invalid workflow declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.WorkflowOrchestration;
                [WorkflowOrchestration]
                public static class Workflow;
                """),
            Compile("""
                using PatternKit.Generators.WorkflowOrchestration;
                [WorkflowOrchestration]
                public static partial class Workflow;
                """),
            Compile("""
                using System.Threading;
                using System.Threading.Tasks;
                using PatternKit.Generators.WorkflowOrchestration;
                public sealed class Ctx;
                [WorkflowOrchestration]
                public static partial class Workflow
                {
                    [WorkflowStep("one", 1)]
                    private static Task Step(Ctx context, CancellationToken cancellationToken) => Task.CompletedTask;
                }
                """),
            Compile("""
                using System.Threading;
                using System.Threading.Tasks;
                using PatternKit.Generators.WorkflowOrchestration;
                public sealed class Ctx;
                [WorkflowOrchestration]
                public static partial class Workflow
                {
                    [WorkflowStep("one", 1)]
                    private static ValueTask One(Ctx context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                    [WorkflowStep("one", 2)]
                    private static ValueTask Two(Ctx context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                }
                """),
            Compile("""
                using System.Threading;
                using System.Threading.Tasks;
                using PatternKit.Generators.WorkflowOrchestration;
                public sealed class Ctx;
                [WorkflowOrchestration(FactoryMethodName = "")]
                public static partial class Workflow
                {
                    [WorkflowStep("one", 1)]
                    private static ValueTask One(Ctx context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                }
                """),
            Compile("""
                using System.Threading;
                using System.Threading.Tasks;
                using PatternKit.Generators.WorkflowOrchestration;
                public sealed class Ctx;
                [WorkflowOrchestration(WorkflowName = " ")]
                public static partial class Workflow
                {
                    [WorkflowStep("one", 1)]
                    private static ValueTask One(Ctx context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                }
                """)
        })
        .Then("diagnostics identify invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, static diagnostic => diagnostic.Id == "PKWO001");
            ScenarioExpect.Contains(results[1].Diagnostics, static diagnostic => diagnostic.Id == "PKWO002");
            ScenarioExpect.Contains(results[2].Diagnostics, static diagnostic => diagnostic.Id == "PKWO003");
            ScenarioExpect.Contains(results[3].Diagnostics, static diagnostic => diagnostic.Id == "PKWO004");
            ScenarioExpect.Contains(results[4].Diagnostics, static diagnostic => diagnostic.Id == "PKWO005");
            ScenarioExpect.Contains(results[5].Diagnostics, static diagnostic => diagnostic.Id == "PKWO005");
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid workflow condition and compensation")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Workflow_Condition_And_Compensation()
        => Given("workflow declarations with invalid hooks", () => new[]
        {
            Compile("""
                using System.Threading;
                using System.Threading.Tasks;
                using PatternKit.Generators.WorkflowOrchestration;
                public sealed class Ctx;
                [WorkflowOrchestration]
                public static partial class Workflow
                {
                    [WorkflowStep("one", 1, Condition = nameof(ShouldRun))]
                    private static ValueTask One(Ctx context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                    private static string ShouldRun(Ctx context) => "yes";
                }
                """),
            Compile("""
                using System.Threading;
                using System.Threading.Tasks;
                using PatternKit.Generators.WorkflowOrchestration;
                public sealed class Ctx;
                [WorkflowOrchestration]
                public static partial class Workflow
                {
                    [WorkflowStep("one", 1, Compensation = nameof(Undo))]
                    private static ValueTask One(Ctx context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                    private static ValueTask Undo(string context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                }
                """),
            Compile("""
                using System.Threading;
                using System.Threading.Tasks;
                using PatternKit.Generators.WorkflowOrchestration;
                public sealed class Ctx;
                public sealed class OtherCtx;
                [WorkflowOrchestration]
                public static partial class Workflow
                {
                    [WorkflowStep("one", 1)]
                    private static ValueTask One(Ctx context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                    [WorkflowStep("two", 2)]
                    private static ValueTask Two(OtherCtx context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                }
                """)
        })
        .Then("diagnostics identify the invalid hooks", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, static diagnostic => diagnostic.Id == "PKWO003");
            ScenarioExpect.Contains(results[1].Diagnostics, static diagnostic => diagnostic.Id == "PKWO003");
            ScenarioExpect.Contains(results[2].Diagnostics, static diagnostic => diagnostic.Id == "PKWO003");
        })
        .AssertPassed();

    [Scenario("Skips workflow orchestration generation for malformed context type")]
    [Fact]
    public Task Skips_Workflow_Orchestration_Generation_For_Malformed_Context_Type()
        => Given("a workflow declaration with an unresolved context type", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.WorkflowOrchestration;
            [WorkflowOrchestration]
            public static partial class Workflow
            {
                [WorkflowStep("one", 1)]
                private static ValueTask One(MissingContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }
            """))
        .Then("the generator reports the invalid step and produces no generated source", result =>
        {
            ScenarioExpect.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PKWO003");
            ScenarioExpect.Empty(result.GeneratedSources);
            ScenarioExpect.False(result.EmitSuccess);
        })
        .AssertPassed();

    [Scenario("Generates workflow orchestration defaults and nested host wrappers")]
    [Fact]
    public Task Generates_Workflow_Orchestration_Defaults_And_Nested_Host_Wrappers()
        => Given("a nested workflow orchestration host", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.WorkflowOrchestration;
            namespace Demo;
            public sealed class Ctx;
            public static partial class Module
            {
                internal abstract partial class Workflows
                {
                    [WorkflowOrchestration(WorkflowName = "fulfillment\\\"workflow")]
                    private sealed partial class Fulfillment
                    {
                        [WorkflowStep("one", 1)]
                        private static ValueTask One(Ctx context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                    }
                }
            }
            """))
        .Then("the generated source preserves containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class Module", source);
            ScenarioExpect.Contains("internal abstract partial class Workflows", source);
            ScenarioExpect.Contains("private sealed partial class Fulfillment", source);
            ScenarioExpect.Contains("WorkflowOrchestrator<global::Demo.Ctx> Create()", source);
            ScenarioExpect.Contains("Create(\"fulfillment\\\\\\\"workflow\")", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "WorkflowOrchestrationGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Application.WorkflowOrchestration.WorkflowOrchestrator<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new WorkflowOrchestrationGenerator(), out var run, out var updated);
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
