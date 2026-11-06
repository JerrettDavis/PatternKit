using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.Mailboxes;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Mailbox generator")]
public sealed partial class MailboxGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates typed mailbox factory")]
    [Fact]
    public Task Generates_Typed_Mailbox_Factory()
        => Given("a configured mailbox declaration", () => Compile("""
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.Mailboxes;

            namespace MyApp;

            public sealed record WorkItem(string Id);

            [GenerateMailbox(typeof(WorkItem), FactoryName = "CreateWorker", Capacity = 8, BackpressurePolicy = "Reject", ErrorPolicy = "Continue")]
            public static partial class WorkMailbox
            {
                public static readonly List<string> Processed = [];
                public static readonly List<string> Errors = [];
                public static readonly List<MailboxEventKind> Events = [];

                [MailboxHandler]
                private static ValueTask Handle(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken)
                {
                    Processed.Add(message.Payload.Id);
                    return default;
                }

                [MailboxErrorHandler]
                private static ValueTask HandleError(Exception exception, Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken)
                {
                    Errors.Add(exception.Message);
                    return default;
                }

                [MailboxEventSink]
                private static void Observe(MailboxEvent evt) => Events.Add(evt.Kind);
            }
            """))
        .Then("generated source creates the configured mailbox", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Equal("WorkMailbox.Mailbox.g.cs", source.HintName);
            ScenarioExpect.Contains("public static partial class WorkMailbox", source.Source);
            ScenarioExpect.Contains("CreateWorker()", source.Source);
            ScenarioExpect.Contains(".Bounded(8, global::PatternKit.Messaging.Mailboxes.MailboxBackpressurePolicy.Reject)", source.Source);
            ScenarioExpect.Contains(".OnError(global::PatternKit.Messaging.Mailboxes.MailboxErrorPolicy.Continue, HandleError)", source.Source);
            ScenarioExpect.Contains(".OnEvent(Observe)", source.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid mailbox declarations")]
    [Theory]
    [InlineData("public static class WorkMailbox;", "PKMB001")]
    [InlineData("public static partial class WorkMailbox;", "PKMB002")]
    [InlineData("public static partial class WorkMailbox { [MailboxHandler] private static ValueTask One(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken) => default; [MailboxHandler] private static ValueTask Two(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken) => default; }", "PKMB002")]
    [InlineData("public static partial class WorkMailbox { [MailboxHandler] private static void Handle(WorkItem item) { } }", "PKMB003")]
    [InlineData("public static partial class WorkMailbox { [MailboxHandler] private ValueTask Handle(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken) => default; }", "PKMB003")]
    [InlineData("public static partial class WorkMailbox { [MailboxHandler] private static ValueTask Handle(Message<string> message, MessageContext context, CancellationToken cancellationToken) => default; }", "PKMB003")]
    [InlineData("public static partial class WorkMailbox { [MailboxHandler] private static ValueTask Handle(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken) => default; [MailboxErrorHandler] private static void HandleError(Exception exception) { } }", "PKMB004")]
    [InlineData("public static partial class WorkMailbox { [MailboxHandler] private static ValueTask Handle(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken) => default; [MailboxEventSink] private static ValueTask Observe(MailboxEvent evt) => default; }", "PKMB004")]
    [InlineData("public static partial class WorkMailbox { [MailboxHandler] private static ValueTask Handle(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken) => default; [MailboxEventSink] private static void One(MailboxEvent evt) { } [MailboxEventSink] private static void Two(MailboxEvent evt) { } }", "PKMB004")]
    public Task Reports_Diagnostics_For_Invalid_Mailbox_Declarations(string declaration, string diagnosticId)
        => Given("an invalid mailbox declaration", () => Compile($$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.Mailboxes;
            public sealed record WorkItem(string Id);
            [GenerateMailbox(typeof(WorkItem))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid mailbox configuration")]
    [Theory]
    [InlineData("Capacity = -1")]
    [InlineData("BackpressurePolicy = \"Overflow\"")]
    [InlineData("ErrorPolicy = \"Ignore\"")]
    public Task Reports_Diagnostics_For_Invalid_Mailbox_Configuration(string configuration)
        => Given("a mailbox declaration with invalid configuration", () => Compile($$"""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            public sealed record WorkItem(string Id);
            [GenerateMailbox(typeof(WorkItem), {{configuration}})]
            public static partial class WorkMailbox
            {
                [MailboxHandler]
                private static ValueTask Handle(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken) => default;
            }
            """))
        .Then("the configuration diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKMB005"))
        .AssertPassed();

    [Scenario("Generates mailbox defaults, policies, and host shapes")]
    [Fact]
    public Task Generates_Mailbox_Defaults_Policies_And_Host_Shapes()
        => Given("mailbox declarations with default names and host shapes", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;
            public sealed record WorkItem(string Id);

            [GenerateMailbox(typeof(WorkItem))]
            internal abstract partial class AbstractMailbox
            {
                [MailboxHandler]
                private static ValueTask Handle(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken) => default;
            }

            [GenerateMailbox(typeof(WorkItem), Capacity = 4, BackpressurePolicy = "dropnewest", ErrorPolicy = "continue")]
            public sealed partial class SealedMailbox
            {
                [MailboxHandler]
                private static ValueTask Handle(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken) => default;
            }

            [GenerateMailbox(typeof(WorkItem), Capacity = 3, BackpressurePolicy = "DropOldest")]
            internal partial struct StructMailbox
            {
                [MailboxHandler]
                private static ValueTask Handle(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken) => default;
            }
            """))
        .Then("generated sources preserve host shape and normalize policy names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources.Select(static source => source.Source));
            ScenarioExpect.Contains("internal abstract partial class AbstractMailbox", combined);
            ScenarioExpect.Contains("public sealed partial class SealedMailbox", combined);
            ScenarioExpect.Contains("internal partial struct StructMailbox", combined);
            ScenarioExpect.Contains(".Unbounded()", combined);
            ScenarioExpect.Contains("MailboxBackpressurePolicy.DropNewest", combined);
            ScenarioExpect.Contains("MailboxBackpressurePolicy.DropOldest", combined);
            ScenarioExpect.Contains("MailboxErrorPolicy.Stop", combined);
            ScenarioExpect.Contains("MailboxErrorPolicy.Continue", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested mailbox host wrappers")]
    [Fact]
    public Task Generates_Nested_Mailbox_Host_Wrappers()
        => Given("nested mailbox declarations", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record WorkItem(string Id);

            public partial class MailboxContainer
            {
                private partial class PrivateHost
                {
                    [GenerateMailbox(typeof(WorkItem))]
                    protected partial class ProtectedMailbox
                    {
                        [MailboxHandler]
                        private static ValueTask Handle(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken) => default;
                    }

                    [GenerateMailbox(typeof(WorkItem))]
                    private protected partial class PrivateProtectedMailbox
                    {
                        [MailboxHandler]
                        private static ValueTask Handle(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken) => default;
                    }

                    [GenerateMailbox(typeof(WorkItem))]
                    protected internal partial class ProtectedInternalMailbox
                    {
                        [MailboxHandler]
                        private static ValueTask Handle(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken) => default;
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources.Select(static source => source.Source));
            ScenarioExpect.Contains("public partial class MailboxContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedMailbox", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedMailbox", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalMailbox", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed mailbox payload type")]
    [Fact]
    public Task Skips_Malformed_Mailbox_Payload_Type()
        => Given("a mailbox declaration with a null payload type", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            [GenerateMailbox(null!)]
            public static partial class WorkMailbox
            {
                [MailboxHandler]
                private static ValueTask Handle(Message<string> message, MessageContext context, CancellationToken cancellationToken) => default;
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = CreateCompilation(source, "MailboxGeneratorTests");
        _ = RoslynTestHelpers.Run(compilation, new MailboxGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources
                .Select(static source => new GeneratedSource(source.HintName, source.SourceText.ToString()))
                .ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(Mailbox<>).Assembly.Location));

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<GeneratedSource> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);

    private sealed record GeneratedSource(string HintName, string Source);
}
