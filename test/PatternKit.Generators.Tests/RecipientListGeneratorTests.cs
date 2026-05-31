using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class RecipientListGeneratorTests
{
    [Scenario("Generates sync recipient-list factory")]
    [Fact]
    public void GeneratesSyncRecipientListFactory()
    {
        var source = """
            using System.Linq;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateRecipientList(typeof(Order), FactoryName = "Build")]
            public static partial class OrderRecipients
            {
                private static bool IsRetail(Message<Order> message, MessageContext context)
                    => message.Payload.Channel == "retail";

                private static bool IsWholesale(Message<Order> message, MessageContext context)
                    => message.Payload.Channel == "wholesale";

                [RecipientListRecipient("wholesale-audit", 20, nameof(IsWholesale))]
                private static void WholesaleAudit(Message<Order> message, MessageContext context) { }

                [RecipientListRecipient("retail-audit", 10, nameof(IsRetail))]
                private static void RetailAudit(Message<Order> message, MessageContext context) { }
            }

            public static class Demo
            {
                public static string[] Run()
                    => OrderRecipients.Build()
                        .Dispatch(Message<Order>.Create(new Order("retail")))
                        .DeliveredRecipients
                        .ToArray();
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesSyncRecipientListFactory));
        var gen = new RecipientListGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderRecipients.RecipientList.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains(".When(\"retail-audit\", IsRetail).Then(RetailAudit)", text);
        ScenarioExpect.Contains(".When(\"wholesale-audit\", IsWholesale).Then(WholesaleAudit)", text);
        ScenarioExpect.True(text.IndexOf("retail-audit", StringComparison.Ordinal) < text.IndexOf("wholesale-audit", StringComparison.Ordinal));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Generates async recipient-list factory")]
    [Fact]
    public void GeneratesAsyncRecipientListFactory()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateRecipientList(typeof(Order), AsyncFactoryName = "BuildAsync")]
            public static partial class OrderRecipients
            {
                private static ValueTask<bool> IsPriority(Message<Order> message, MessageContext context, CancellationToken cancellationToken)
                    => new(message.Payload.Channel == "priority");

                [RecipientListRecipient("priority-audit", 10, nameof(IsPriority))]
                private static ValueTask PriorityAudit(Message<Order> message, MessageContext context, CancellationToken cancellationToken)
                    => ValueTask.CompletedTask;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesAsyncRecipientListFactory));
        var gen = new RecipientListGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Contains("AsyncRecipientList<global::MyApp.Order>", generated.SourceText.ToString());
        ScenarioExpect.Contains(".When(\"priority-audit\", IsPriority).Then(PriorityAudit)", generated.SourceText.ToString());

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Generates recipient-list factories for global struct with sync and async recipients")]
    [Fact]
    public void GeneratesRecipientListFactoriesForGlobalStructWithSyncAndAsyncRecipients()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            public sealed record Order(string Channel);

            [GenerateRecipientList(typeof(Order), FactoryName = "BuildSync", AsyncFactoryName = "BuildAsync")]
            public partial struct OrderRecipients
            {
                private static bool IsRetail(Message<Order> message, MessageContext context)
                    => message.Payload.Channel == "retail";

                private static ValueTask<bool> IsPriority(Message<Order> message, MessageContext context, CancellationToken cancellationToken)
                    => ValueTask.FromResult(message.Payload.Channel == "priority");

                [RecipientListRecipient("retail\"audit", 10, nameof(IsRetail))]
                private static void RetailAudit(Message<Order> message, MessageContext context) { }

                [RecipientListRecipient("priority-audit", 20, nameof(IsPriority))]
                private static ValueTask PriorityAudit(Message<Order> message, MessageContext context, CancellationToken cancellationToken)
                    => ValueTask.CompletedTask;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesRecipientListFactoriesForGlobalStructWithSyncAndAsyncRecipients));
        var gen = new RecipientListGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));

        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("OrderRecipients.RecipientList.g.cs", generated.HintName);
        ScenarioExpect.DoesNotContain("namespace ", text);
        ScenarioExpect.Contains("partial struct OrderRecipients", text);
        ScenarioExpect.Contains("BuildSync()", text);
        ScenarioExpect.Contains("BuildAsync()", text);
        ScenarioExpect.Contains(".When(\"retail\\\"audit\", IsRetail).Then(RetailAudit)", text);
        ScenarioExpect.Contains(".When(\"priority-audit\", IsPriority).Then(PriorityAudit)", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial recipient list")]
    [Fact]
    public void ReportsDiagnosticForNonPartialRecipientList()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateRecipientList(typeof(Order))]
            public static class OrderRecipients
            {
                private static bool IsRetail(Message<Order> message, MessageContext context) => true;

                [RecipientListRecipient("retail", 10, nameof(IsRetail))]
                private static void Retail(Message<Order> message, MessageContext context) { }
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialRecipientList));
        var gen = new RecipientListGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKRL001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for missing recipients")]
    [Fact]
    public void ReportsDiagnosticForMissingRecipients()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateRecipientList(typeof(Order))]
            public static partial class OrderRecipients;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForMissingRecipients));
        var gen = new RecipientListGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKRL002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid recipient signature")]
    [Fact]
    public void ReportsDiagnosticForInvalidRecipientSignature()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateRecipientList(typeof(Order))]
            public static partial class OrderRecipients
            {
                private static bool IsRetail(Message<Order> message, MessageContext context) => true;

                [RecipientListRecipient("retail", 10, nameof(IsRetail))]
                private static int Retail(Message<Order> message, MessageContext context) => 1;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidRecipientSignature));
        var gen = new RecipientListGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKRL003", diagnostic.Id);
    }

    [Scenario("Reports diagnostics for invalid recipient-list recipient shapes")]
    [Fact]
    public void ReportsDiagnosticsForInvalidRecipientListRecipientShapes()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateRecipientList(typeof(Order))]
            public static partial class OrderRecipients
            {
                private static bool IsRetail(Message<Order> message, MessageContext context) => true;
                private static int InvalidPredicate(Message<Order> message, MessageContext context) => 1;

                [RecipientListRecipient(" ", 10, nameof(IsRetail))]
                private static void BlankName(Message<Order> message, MessageContext context) { }

                [RecipientListRecipient("missing-predicate", 20, "Missing")]
                private static void MissingPredicate(Message<Order> message, MessageContext context) { }

                [RecipientListRecipient("invalid-predicate", 30, nameof(InvalidPredicate))]
                private static void InvalidPredicateRecipient(Message<Order> message, MessageContext context) { }

                [RecipientListRecipient("instance", 40, nameof(IsRetail))]
                private void Instance(Message<Order> message, MessageContext context) { }
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticsForInvalidRecipientListRecipientShapes));
        var gen = new RecipientListGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(result => result.Diagnostics).ToArray();
        ScenarioExpect.Equal(4, diagnostics.Count(diagnostic => diagnostic.Id == "PKRL003"));
    }

    [Scenario("Reports diagnostic for duplicate recipient name or order")]
    [Fact]
    public void ReportsDiagnosticForDuplicateRecipientNameOrOrder()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Channel);

            [GenerateRecipientList(typeof(Order))]
            public static partial class OrderRecipients
            {
                private static bool Always(Message<Order> message, MessageContext context) => true;

                [RecipientListRecipient("audit", 10, nameof(Always))]
                private static void Audit(Message<Order> message, MessageContext context) { }

                [RecipientListRecipient("billing", 10, nameof(Always))]
                private static void Billing(Message<Order> message, MessageContext context) { }
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForDuplicateRecipientNameOrOrder));
        var gen = new RecipientListGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKRL004", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Message<>).Assembly.Location));
}
