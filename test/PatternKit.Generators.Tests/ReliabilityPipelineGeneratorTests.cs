using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ReliabilityPipelineGeneratorTests
{
    [Scenario("Generates idempotent receiver inbox and outbox factories")]
    [Fact]
    public void GeneratesIdempotentReceiverInboxAndOutboxFactories()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.Reliability;

            namespace MyApp;

            public sealed record AcceptOrder(string Id);
            public sealed record OrderAccepted(string Id);

            [GenerateReliabilityPipeline(
                typeof(AcceptOrder),
                typeof(string),
                typeof(OrderAccepted),
                ReceiverFactoryName = "BuildReceiver",
                InboxFactoryName = "BuildInbox",
                OutboxFactoryName = "BuildOutbox",
                DuplicatePolicy = "ReplayCompleted",
                MissingKeyPolicy = "Process")]
            public static partial class OrderReliability
            {
                [ReliabilityHandler]
                private static ValueTask<string> Handle(Message<AcceptOrder> message, MessageContext context, CancellationToken cancellationToken)
                    => new(message.Payload.Id);

                [ReliabilityKeySelector]
                private static string? SelectKey(Message<AcceptOrder> message, MessageContext context)
                    => message.Headers.IdempotencyKey ?? message.Payload.Id;
            }

            public static class Demo
            {
                public static async ValueTask<string?> RunAsync()
                {
                    var inbox = OrderReliability.BuildInbox(new InMemoryIdempotencyStore());
                    var result = await inbox.ProcessAsync(Message<AcceptOrder>.Create(new AcceptOrder("order-1")));
                    var outbox = OrderReliability.BuildOutbox();
                    return result.Result;
                }
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesIdempotentReceiverInboxAndOutboxFactories));
        var gen = new ReliabilityPipelineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderReliability.ReliabilityPipeline.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("BuildReceiver", text);
        ScenarioExpect.Contains(".KeyBy(SelectKey)", text);
        ScenarioExpect.Contains(".OnDuplicate(global::PatternKit.Messaging.Reliability.DuplicateMessagePolicy.ReplayCompleted)", text);
        ScenarioExpect.Contains(".OnMissingKey(global::PatternKit.Messaging.Reliability.MissingIdempotencyKeyPolicy.Process)", text);
        ScenarioExpect.Contains("BuildInbox", text);
        ScenarioExpect.Contains("BuildOutbox", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial reliability pipeline")]
    [Fact]
    public void ReportsDiagnosticForNonPartialReliabilityPipeline()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record AcceptOrder(string Id);
            public sealed record OrderAccepted(string Id);

            [GenerateReliabilityPipeline(typeof(AcceptOrder), typeof(string), typeof(OrderAccepted))]
            public static class OrderReliability;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialReliabilityPipeline));
        var gen = new ReliabilityPipelineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKRP001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for missing reliability handler")]
    [Fact]
    public void ReportsDiagnosticForMissingReliabilityHandler()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record AcceptOrder(string Id);
            public sealed record OrderAccepted(string Id);

            [GenerateReliabilityPipeline(typeof(AcceptOrder), typeof(string), typeof(OrderAccepted))]
            public static partial class OrderReliability;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForMissingReliabilityHandler));
        var gen = new ReliabilityPipelineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKRP002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid reliability handler")]
    [Fact]
    public void ReportsDiagnosticForInvalidReliabilityHandler()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record AcceptOrder(string Id);
            public sealed record OrderAccepted(string Id);

            [GenerateReliabilityPipeline(typeof(AcceptOrder), typeof(string), typeof(OrderAccepted))]
            public static partial class OrderReliability
            {
                [ReliabilityHandler]
                private static string Handle(AcceptOrder command) => command.Id;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidReliabilityHandler));
        var gen = new ReliabilityPipelineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKRP003", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid reliability policy")]
    [Fact]
    public void ReportsDiagnosticForInvalidReliabilityPolicy()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record AcceptOrder(string Id);
            public sealed record OrderAccepted(string Id);

            [GenerateReliabilityPipeline(typeof(AcceptOrder), typeof(string), typeof(OrderAccepted), DuplicatePolicy = "ReplayForever")]
            public static partial class OrderReliability
            {
                [ReliabilityHandler]
                private static ValueTask<string> Handle(Message<AcceptOrder> message, MessageContext context, CancellationToken cancellationToken)
                    => new(message.Payload.Id);
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidReliabilityPolicy));
        var gen = new ReliabilityPipelineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKRP005", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Reliability.IdempotentReceiver<,>).Assembly.Location));
}
