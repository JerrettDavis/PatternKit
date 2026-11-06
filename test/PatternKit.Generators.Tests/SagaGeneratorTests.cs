using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class SagaGeneratorTests
{
    [Scenario("GeneratesSyncSagaFactory")]
    [Fact]
    public void GeneratesSyncSagaFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record OrderState(string? Id, bool Started, bool Paid);
            public sealed record Started(string OrderId);
            public sealed record Paid(string OrderId);

            [GenerateSaga(typeof(OrderState), FactoryName = "Build")]
            public static partial class OrderSaga
            {
                [SagaStep(typeof(Paid), 20)]
                private static OrderState Pay(OrderState state, Message<Paid> message, MessageContext context)
                    => state with { Paid = true };

                [SagaStep(typeof(Started), 10)]
                private static OrderState Start(OrderState state, Message<Started> message, MessageContext context)
                    => state with { Id = message.Payload.OrderId, Started = true };

                [SagaCompleteWhen]
                private static bool IsComplete(OrderState state) => state.Started && state.Paid;
            }

            public static class Demo
            {
                public static bool Run()
                {
                    var saga = OrderSaga.Build();
                    var started = saga.Handle(new OrderState(null, false, false), Message<Started>.Create(new Started("order-1")));
                    var paid = saga.Handle(started.State, Message<Paid>.Create(new Paid("order-1")));
                    return paid.Completed;
                }
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesSyncSagaFactory));
        var gen = new SagaGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("OrderSaga.Saga.g.cs", generated.HintName);
        ScenarioExpect.Contains(".On<global::MyApp.Started>().Then(Start)", text);
        ScenarioExpect.Contains(".On<global::MyApp.Paid>().Then(Pay)", text);
        ScenarioExpect.Contains(".CompleteWhen(IsComplete)", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GeneratesAsyncSagaFactory")]
    [Fact]
    public void GeneratesAsyncSagaFactory()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record OrderState(bool Started);
            public sealed record Started(string OrderId);

            [GenerateSaga(typeof(OrderState), AsyncFactoryName = "BuildAsync")]
            public static partial class OrderSaga
            {
                [SagaStep(typeof(Started), 10)]
                private static ValueTask<OrderState> StartAsync(OrderState state, Message<Started> message, MessageContext context, CancellationToken cancellationToken)
                    => new ValueTask<OrderState>(state with { Started = true });
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesAsyncSagaFactory));
        var gen = new SagaGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        ScenarioExpect.Contains("AsyncSaga<global::MyApp.OrderState>", ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources)).SourceText.ToString());

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GeneratesSagaFactoriesForGlobalStructHostWithSyncAndAsyncSteps")]
    [Fact]
    public void GeneratesSagaFactoriesForGlobalStructHostWithSyncAndAsyncSteps()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            public readonly record struct OrderState(bool Started, bool Paid);
            public sealed record Started(string OrderId);
            public sealed record Paid(string OrderId);

            [GenerateSaga(typeof(OrderState), FactoryName = "BuildSync", AsyncFactoryName = "BuildAsync")]
            public partial struct OrderSaga
            {
                [SagaStep(typeof(Started), 10)]
                private static OrderState Start(OrderState state, Message<Started> message, MessageContext context)
                    => state with { Started = true };

                [SagaStep(typeof(Paid), 20)]
                private static ValueTask<OrderState> PayAsync(OrderState state, Message<Paid> message, MessageContext context, CancellationToken cancellationToken)
                    => ValueTask.FromResult(state with { Paid = true });

                [SagaCompleteWhen]
                private static bool IsComplete(OrderState state) => state.Started && state.Paid;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesSagaFactoriesForGlobalStructHostWithSyncAndAsyncSteps));
        var gen = new SagaGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));

        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("OrderSaga.Saga.g.cs", generated.HintName);
        ScenarioExpect.DoesNotContain("namespace ", text);
        ScenarioExpect.Contains("partial struct OrderSaga", text);
        ScenarioExpect.Contains("BuildSync()", text);
        ScenarioExpect.Contains("BuildAsync()", text);
        ScenarioExpect.Contains(".On<global::Started>().Then(Start)", text);
        ScenarioExpect.Contains(".On<global::Paid>().Then(PayAsync)", text);
        ScenarioExpect.Equal(2, CountOccurrences(text, ".CompleteWhen(IsComplete)"));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ReportsDiagnosticForNonPartialSaga")]
    [Fact]
    public void ReportsDiagnosticForNonPartialSaga()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record OrderState(bool Started);
            public sealed record Started(string OrderId);

            [GenerateSaga(typeof(OrderState))]
            public static class OrderSaga
            {
                [SagaStep(typeof(Started), 10)]
                private static OrderState Start(OrderState state, Message<Started> message, MessageContext context) => state;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialSaga));
        var gen = new SagaGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        ScenarioExpect.Equal("PKSG001", ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics)).Id);
    }

    [Scenario("ReportsDiagnosticForMissingSteps")]
    [Fact]
    public void ReportsDiagnosticForMissingSteps()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record OrderState(bool Started);

            [GenerateSaga(typeof(OrderState))]
            public static partial class OrderSaga;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForMissingSteps));
        var gen = new SagaGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        ScenarioExpect.Equal("PKSG002", ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics)).Id);
    }

    [Scenario("ReportsDiagnosticForInvalidStepSignature")]
    [Fact]
    public void ReportsDiagnosticForInvalidStepSignature()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record OrderState(bool Started);
            public sealed record Started(string OrderId);

            [GenerateSaga(typeof(OrderState))]
            public static partial class OrderSaga
            {
                [SagaStep(typeof(Started), 10)]
                private static Started Start(OrderState state, Message<Started> message, MessageContext context) => message.Payload;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidStepSignature));
        var gen = new SagaGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        ScenarioExpect.Equal("PKSG003", ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics)).Id);
    }

    [Scenario("ReportsDiagnosticForInvalidSagaStepShapes")]
    [Fact]
    public void ReportsDiagnosticForInvalidSagaStepShapes()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record OrderState(bool Started);
            public sealed record Started(string OrderId);

            [GenerateSaga(typeof(OrderState))]
            public static partial class OrderSaga
            {
                [SagaStep(typeof(Started), 10)]
                private static OrderState MissingContext(OrderState state, Message<Started> message) => state;

                [SagaStep(typeof(Started), 20)]
                private OrderState InstanceStep(OrderState state, Message<Started> message, MessageContext context) => state;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidSagaStepShapes));
        var gen = new SagaGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(result => result.Diagnostics).ToArray();
        ScenarioExpect.Equal(2, diagnostics.Count(diagnostic => diagnostic.Id == "PKSG003"));
        ScenarioExpect.Equal(2, diagnostics.Length);
        ScenarioExpect.All(diagnostics, diagnostic => ScenarioExpect.Equal("PKSG003", diagnostic.Id));
    }

    [Scenario("ReportsDiagnosticForInvalidCompletionSignature")]
    [Fact]
    public void ReportsDiagnosticForInvalidCompletionSignature()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record OrderState(bool Started);
            public sealed record Started(string OrderId);

            [GenerateSaga(typeof(OrderState))]
            public static partial class OrderSaga
            {
                [SagaStep(typeof(Started), 10)]
                private static OrderState Start(OrderState state, Message<Started> message, MessageContext context) => state;

                [SagaCompleteWhen]
                private static OrderState IsComplete(OrderState state) => state;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidCompletionSignature));
        var gen = new SagaGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        ScenarioExpect.Equal("PKSG004", ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics)).Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Message<>).Assembly.Location));

    private static int CountOccurrences(string value, string match)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(match, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += match.Length;
        }

        return count;
    }
}
