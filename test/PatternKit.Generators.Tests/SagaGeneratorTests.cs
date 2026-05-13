using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;

namespace PatternKit.Generators.Tests;

public sealed class SagaGeneratorTests
{
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

        Assert.All(run.Results, result => Assert.Empty(result.Diagnostics));
        var generated = Assert.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        Assert.Equal("OrderSaga.Saga.g.cs", generated.HintName);
        Assert.Contains(".On<global::MyApp.Started>().Then(Start)", text);
        Assert.Contains(".On<global::MyApp.Paid>().Then(Pay)", text);
        Assert.Contains(".CompleteWhen(IsComplete)", text);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

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

        Assert.All(run.Results, result => Assert.Empty(result.Diagnostics));
        Assert.Contains("AsyncSaga<global::MyApp.OrderState>", Assert.Single(run.Results.SelectMany(result => result.GeneratedSources)).SourceText.ToString());

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

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

        Assert.Equal("PKSG001", Assert.Single(run.Results.SelectMany(result => result.Diagnostics)).Id);
    }

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

        Assert.Equal("PKSG002", Assert.Single(run.Results.SelectMany(result => result.Diagnostics)).Id);
    }

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

        Assert.Equal("PKSG003", Assert.Single(run.Results.SelectMany(result => result.Diagnostics)).Id);
    }

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

        Assert.Equal("PKSG004", Assert.Single(run.Results.SelectMany(result => result.Diagnostics)).Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Message<>).Assembly.Location));
}
