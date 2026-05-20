using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class BackplaneTopologyGeneratorTests
{
    [Scenario("Generates backplane request-reply and subscription topology")]
    [Fact]
    public void GeneratesBackplaneRequestReplyAndSubscriptionTopology()
    {
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace PatternKit.Examples.Messaging;

            public sealed class BackplaneHostBuilder
            {
                public BackplaneHostBuilder MapCommand<TRequest, TResponse>(Func<Message<TRequest>, MessageContext, bool> predicate, string endpointName) => this;
                public BackplaneHostBuilder MapDefaultCommand<TRequest, TResponse>(string endpointName) => this;
                public BackplaneHostBuilder ReceiveEndpoint(string endpointName, Action<BackplaneEndpointBuilder> configure)
                {
                    configure(new BackplaneEndpointBuilder());
                    return this;
                }
            }

            public sealed class BackplaneEndpointBuilder
            {
                public void HandleCommand<TRequest, TResponse>(Func<Message<TRequest>, MessageContext, CancellationToken, ValueTask<TResponse>> handler) { }
                public void Subscribe<TEvent>(string topic, Func<Message<TEvent>, MessageContext, CancellationToken, ValueTask> handler) { }
            }

            public sealed record SubmitOrder(string Id, bool Priority);
            public sealed record OrderAccepted(string Id);
            public sealed record OrderSubmitted(string Id);

            public sealed class OrderServices
            {
                public ValueTask<OrderAccepted> AcceptPriorityAsync(Message<SubmitOrder> message, MessageContext context, CancellationToken cancellationToken)
                    => new(new OrderAccepted(message.Payload.Id));

                public ValueTask<OrderAccepted> AcceptStandardAsync(Message<SubmitOrder> message, MessageContext context, CancellationToken cancellationToken)
                    => new(new OrderAccepted(message.Payload.Id));

                public ValueTask AuditAsync(Message<OrderSubmitted> message, MessageContext context, CancellationToken cancellationToken)
                    => default;
            }

            [GenerateBackplaneTopology(typeof(OrderServices), ConfigureMethodName = "Apply", HostBuilderType = typeof(BackplaneHostBuilder))]
            [BackplaneRequestReply(typeof(SubmitOrder), typeof(OrderAccepted), "orders.priority", nameof(OrderServices.AcceptPriorityAsync), PredicateMethodName = nameof(IsPriority))]
            [BackplaneRequestReply(typeof(SubmitOrder), typeof(OrderAccepted), "orders.standard", nameof(OrderServices.AcceptStandardAsync))]
            [BackplaneSubscription(typeof(OrderSubmitted), "orders.submitted", "audit-service", nameof(OrderServices.AuditAsync))]
            public static partial class OrderBackplane
            {
                private static bool IsPriority(Message<SubmitOrder> message, MessageContext context)
                    => message.Payload.Priority;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesBackplaneRequestReplyAndSubscriptionTopology));
        var gen = new BackplaneTopologyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderBackplane.BackplaneTopology.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("public static global::PatternKit.Examples.Messaging.BackplaneHostBuilder Apply(global::PatternKit.Examples.Messaging.BackplaneHostBuilder builder", text);
        ScenarioExpect.Contains("Apply(", text);
        ScenarioExpect.Contains("MapCommand<global::PatternKit.Examples.Messaging.SubmitOrder, global::PatternKit.Examples.Messaging.OrderAccepted>(IsPriority, \"orders.priority\")", text);
        ScenarioExpect.Contains("MapDefaultCommand<global::PatternKit.Examples.Messaging.SubmitOrder, global::PatternKit.Examples.Messaging.OrderAccepted>(\"orders.standard\")", text);
        ScenarioExpect.Contains("endpoint.HandleCommand<global::PatternKit.Examples.Messaging.SubmitOrder, global::PatternKit.Examples.Messaging.OrderAccepted>(services.AcceptPriorityAsync)", text);
        ScenarioExpect.Contains("endpoint.Subscribe<global::PatternKit.Examples.Messaging.OrderSubmitted>(\"orders.submitted\", services.AuditAsync)", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial backplane topology")]
    [Fact]
    public void ReportsDiagnosticForNonPartialBackplaneTopology()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace PatternKit.Examples.Messaging;

            public sealed class OrderServices { }

            [GenerateBackplaneTopology(typeof(OrderServices))]
            public static class OrderBackplane;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialBackplaneTopology));
        var gen = new BackplaneTopologyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKBT001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid request-reply handler")]
    [Fact]
    public void ReportsDiagnosticForInvalidRequestReplyHandler()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace PatternKit.Examples.Messaging;

            public sealed record SubmitOrder(string Id);
            public sealed record OrderAccepted(string Id);
            public sealed class OrderServices { public string Accept(SubmitOrder order) => order.Id; }

            [GenerateBackplaneTopology(typeof(OrderServices))]
            [BackplaneRequestReply(typeof(SubmitOrder), typeof(OrderAccepted), "orders", nameof(OrderServices.Accept))]
            public static partial class OrderBackplane;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidRequestReplyHandler));
        var gen = new BackplaneTopologyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKBT003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Message<>).Assembly.Location));
}
