using Microsoft.CodeAnalysis;
using PatternKit.Application.DomainEvents;
using PatternKit.Generators.DomainEvents;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Domain Event dispatcher generator")]
public sealed partial class DomainEventDispatcherGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits domain event dispatcher factory")]
    [Fact]
    public Task Generator_Emits_Domain_Event_Dispatcher_Factory()
        => Given("a valid domain event dispatcher declaration", () => Compile("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Application.DomainEvents;
            using PatternKit.Generators.DomainEvents;
            namespace Demo;
            public abstract record OrderEvent(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;
            public sealed record OrderPlaced(Guid EventId, DateTimeOffset OccurredAt, string OrderId) : OrderEvent(EventId, OccurredAt);
            [GenerateDomainEventDispatcher(typeof(OrderEvent), FactoryName = "Build", DispatcherName = "order-events")]
            public static partial class OrderEventHandlers
            {
                [DomainEventHandler(typeof(OrderPlaced), 2)]
                private static ValueTask Audit(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                [DomainEventHandler(typeof(OrderPlaced), 1)]
                private static ValueTask Project(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }
            """))
        .Then("generated source creates ordered handlers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("Create(\"order-events\")", source);
            ScenarioExpect.Contains("Handle<global::Demo.OrderPlaced>(Project)", source);
            ScenarioExpect.Contains("Handle<global::Demo.OrderPlaced>(Audit)", source);
            ScenarioExpect.True(source.IndexOf("Project", StringComparison.Ordinal) < source.IndexOf("Audit", StringComparison.Ordinal));
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator reports invalid domain event declarations")]
    [Theory]
    [InlineData("public static class OrderEventHandlers { [DomainEventHandler(typeof(OrderPlaced), 1)] private static ValueTask Handle(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask; }", "PKDE001")]
    [InlineData("public static partial class OrderEventHandlers;", "PKDE002")]
    [InlineData("public partial class OrderEventHandlers { [DomainEventHandler(typeof(OrderPlaced), 1)] private ValueTask Handle(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask; }", "PKDE003")]
    [InlineData("public static partial class OrderEventHandlers { [DomainEventHandler(typeof(OrderPlaced), 1)] private static ValueTask Handle<T>(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask; }", "PKDE003")]
    [InlineData("public static partial class OrderEventHandlers { [DomainEventHandler(typeof(OrderPlaced), 1)] private static string Handle(OrderPlaced domainEvent, CancellationToken cancellationToken) => domainEvent.OrderId; }", "PKDE003")]
    [InlineData("public static partial class OrderEventHandlers { [DomainEventHandler(typeof(OrderPlaced), 1)] private static ValueTask Handle() => ValueTask.CompletedTask; }", "PKDE003")]
    [InlineData("public static partial class OrderEventHandlers { [DomainEventHandler(typeof(OrderPlaced), 1)] private static ValueTask Handle(OrderEvent domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask; }", "PKDE003")]
    [InlineData("public static partial class OrderEventHandlers { [DomainEventHandler(typeof(OrderPlaced), 1)] private static ValueTask Handle(OrderPlaced domainEvent, string cancellationToken) => ValueTask.CompletedTask; }", "PKDE003")]
    [InlineData("public static partial class OrderEventHandlers { [DomainEventHandler(typeof(NotAnOrderEvent), 1)] private static ValueTask Handle(NotAnOrderEvent domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask; }", "PKDE003")]
    [InlineData("public static partial class OrderEventHandlers { [DomainEventHandler(typeof(OrderPlaced), 1)] private static ValueTask One(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask; [DomainEventHandler(typeof(OrderPlaced), 1)] private static ValueTask Two(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask; }", "PKDE004")]
    public Task Generator_Reports_Invalid_Domain_Event_Declarations(string declaration, string diagnosticId)
        => Given("an invalid domain event dispatcher declaration", () => Compile($$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Application.DomainEvents;
            using PatternKit.Generators.DomainEvents;
            public abstract record OrderEvent(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;
            public sealed record OrderPlaced(Guid EventId, DateTimeOffset OccurredAt, string OrderId) : OrderEvent(EventId, OccurredAt);
            public sealed record NotAnOrderEvent(string Id);
            [GenerateDomainEventDispatcher(typeof(OrderEvent))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generator emits domain event defaults and host shapes")]
    [Fact]
    public Task Generator_Emits_Domain_Event_Defaults_And_Host_Shapes()
        => Given("domain event dispatcher declarations with default names and different host shapes", () => Compile("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Application.DomainEvents;
            using PatternKit.Generators.DomainEvents;
            namespace Demo;
            public abstract record OrderEvent(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;
            public sealed record OrderPlaced(Guid EventId, DateTimeOffset OccurredAt, string OrderId) : OrderEvent(EventId, OccurredAt);

            [GenerateDomainEventDispatcher(typeof(OrderEvent))]
            internal abstract partial class AbstractDispatcher
            {
                [DomainEventHandler(typeof(OrderPlaced), 1)]
                private static ValueTask Handle(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }

            [GenerateDomainEventDispatcher(typeof(OrderEvent), DispatcherName = "tenant\\\"events")]
            public sealed partial class SealedDispatcher
            {
                [DomainEventHandler(typeof(OrderPlaced), 1)]
                private static ValueTask Handle(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }

            [GenerateDomainEventDispatcher(typeof(OrderEvent))]
            internal partial struct StructDispatcher
            {
                [DomainEventHandler(typeof(OrderPlaced), 1)]
                private static ValueTask Handle(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractDispatcher", combined);
            ScenarioExpect.Contains("public sealed partial class SealedDispatcher", combined);
            ScenarioExpect.Contains("internal partial struct StructDispatcher", combined);
            ScenarioExpect.Contains("Create(\"AbstractDispatcher\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"events\")", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator emits nested domain event host wrappers")]
    [Fact]
    public Task Generator_Emits_Nested_Domain_Event_Host_Wrappers()
        => Given("nested domain event dispatcher declarations", () => Compile("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Application.DomainEvents;
            using PatternKit.Generators.DomainEvents;
            namespace Demo;
            public abstract record OrderEvent(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;
            public sealed record OrderPlaced(Guid EventId, DateTimeOffset OccurredAt, string OrderId) : OrderEvent(EventId, OccurredAt);

            public partial class DispatcherContainer
            {
                private partial class PrivateHost
                {
                    [GenerateDomainEventDispatcher(typeof(OrderEvent))]
                    protected partial class ProtectedDispatcher
                    {
                        [DomainEventHandler(typeof(OrderPlaced), 1)]
                        private static ValueTask Handle(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                    }

                    [GenerateDomainEventDispatcher(typeof(OrderEvent))]
                    private protected partial class PrivateProtectedDispatcher
                    {
                        [DomainEventHandler(typeof(OrderPlaced), 1)]
                        private static ValueTask Handle(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                    }

                    [GenerateDomainEventDispatcher(typeof(OrderEvent))]
                    protected internal partial class ProtectedInternalDispatcher
                    {
                        [DomainEventHandler(typeof(OrderPlaced), 1)]
                        private static ValueTask Handle(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class DispatcherContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedDispatcher", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedDispatcher", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalDispatcher", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator skips malformed domain event type arguments")]
    [Fact]
    public Task Generator_Skips_Malformed_Domain_Event_Type_Arguments()
        => Given("a domain event dispatcher declaration with a null type argument", () => Compile("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Application.DomainEvents;
            using PatternKit.Generators.DomainEvents;
            public abstract record OrderEvent(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;
            public sealed record OrderPlaced(Guid EventId, DateTimeOffset OccurredAt, string OrderId) : OrderEvent(EventId, OccurredAt);
            [GenerateDomainEventDispatcher(null!)]
            public static partial class OrderEventHandlers
            {
                [DomainEventHandler(typeof(OrderPlaced), 1)]
                private static ValueTask Handle(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "DomainEventDispatcherGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(DomainEventDispatcher<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new DomainEventDispatcherGenerator(), out var run, out var updated);
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
