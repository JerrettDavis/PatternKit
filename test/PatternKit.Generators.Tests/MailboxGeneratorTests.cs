using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class MailboxGeneratorTests
{
    [Scenario("Generates typed mailbox factory")]
    [Fact]
    public void GeneratesTypedMailboxFactory()
    {
        var source = """
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

            public static class Demo
            {
                public static int Capacity()
                {
                    using var mailbox = WorkMailbox.CreateWorker();
                    return mailbox.Capacity ?? 0;
                }
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesTypedMailboxFactory));
        var gen = new MailboxGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("WorkMailbox.Mailbox.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("CreateWorker()", text);
        ScenarioExpect.Contains(".Bounded(8, global::PatternKit.Messaging.Mailboxes.MailboxBackpressurePolicy.Reject)", text);
        ScenarioExpect.Contains(".OnError(global::PatternKit.Messaging.Mailboxes.MailboxErrorPolicy.Continue, HandleError)", text);
        ScenarioExpect.Contains(".OnEvent(Observe)", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial mailbox")]
    [Fact]
    public void ReportsDiagnosticForNonPartialMailbox()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record WorkItem(string Id);

            [GenerateMailbox(typeof(WorkItem))]
            public static class WorkMailbox;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialMailbox));
        var gen = new MailboxGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKMB001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for missing mailbox handler")]
    [Fact]
    public void ReportsDiagnosticForMissingMailboxHandler()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record WorkItem(string Id);

            [GenerateMailbox(typeof(WorkItem))]
            public static partial class WorkMailbox;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForMissingMailboxHandler));
        var gen = new MailboxGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKMB002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid mailbox handler")]
    [Fact]
    public void ReportsDiagnosticForInvalidMailboxHandler()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record WorkItem(string Id);

            [GenerateMailbox(typeof(WorkItem))]
            public static partial class WorkMailbox
            {
                [MailboxHandler]
                private static void Handle(WorkItem item) { }
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidMailboxHandler));
        var gen = new MailboxGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKMB003", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid mailbox policy")]
    [Fact]
    public void ReportsDiagnosticForInvalidMailboxPolicy()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record WorkItem(string Id);

            [GenerateMailbox(typeof(WorkItem), BackpressurePolicy = "Overflow")]
            public static partial class WorkMailbox
            {
                [MailboxHandler]
                private static ValueTask Handle(Message<WorkItem> message, MessageContext context, CancellationToken cancellationToken) => default;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidMailboxPolicy));
        var gen = new MailboxGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKMB005", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Mailboxes.Mailbox<>).Assembly.Location));
}
