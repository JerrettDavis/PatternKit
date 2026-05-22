using Microsoft.CodeAnalysis;
using PatternKit.EnterpriseIntegration.EventNotification;
using PatternKit.Generators.EventNotification;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Event Notification generator")]
public sealed partial class EventNotificationGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates event notification factory")]
    [Fact]
    public Task Generates_Event_Notification_Factory()
        => Given("an event notification declaration", () => Compile("""
            using PatternKit.Generators.EventNotification;
            namespace Demo;
            public sealed record OrderAccepted(string OrderId, string CorrelationId, string Source, bool NotifySubscribers);
            [GenerateEventNotification(typeof(OrderAccepted), typeof(string), FactoryMethodName = "Build", NotificationName = "order-accepted")]
            public static partial class OrderAcceptedNotification
            {
                [EventNotificationRule]
                private static bool ShouldNotify(OrderAccepted evt) => evt.NotifySubscribers;
                [EventNotificationKey]
                private static string Key(OrderAccepted evt) => evt.OrderId;
                [EventNotificationCorrelation]
                private static string Correlation(OrderAccepted evt) => evt.CorrelationId;
                [EventNotificationMetadata("source")]
                private static string Source(OrderAccepted evt) => evt.Source;
            }
            """))
        .Then("the generated source creates the configured notification", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("EventNotification<global::Demo.OrderAccepted, string>.Create(\"order-accepted\")", source);
            ScenarioExpect.Contains(".When(ShouldNotify)", source);
            ScenarioExpect.Contains(".WithKey(Key)", source);
            ScenarioExpect.Contains(".WithCorrelation(Correlation)", source);
            ScenarioExpect.Contains(".WithMetadata(\"source\", Source)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid event notification declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Event_Notification_Declarations()
        => Given("invalid event notification declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.EventNotification;
                [GenerateEventNotification(typeof(string), typeof(string))]
                public static class NotificationHost;
                """),
            Compile("""
                using PatternKit.Generators.EventNotification;
                [GenerateEventNotification(typeof(string), typeof(string))]
                public static partial class NotificationHost;
                """),
            Compile("""
                using PatternKit.Generators.EventNotification;
                [GenerateEventNotification(typeof(string), typeof(string))]
                public static partial class NotificationHost
                {
                    [EventNotificationKey]
                    private static int Key(string value) => value.Length;
                }
                """),
            Compile("""
                using PatternKit.Generators.EventNotification;
                [GenerateEventNotification(typeof(string), typeof(string))]
                public static partial class NotificationHost
                {
                    [EventNotificationKey]
                    private static string Key(string value) => value;
                    [EventNotificationMetadata("source")]
                    private static string Source(string value) => value;
                    [EventNotificationMetadata("SOURCE")]
                    private static string OtherSource(string value) => value;
                }
                """)
        })
        .Then("diagnostics identify invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKEN001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKEN002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKEN003");
            ScenarioExpect.Contains(results[3].Diagnostics, diagnostic => diagnostic.Id == "PKEN004");
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "EventNotificationGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(EventNotification<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new EventNotificationGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new(result.Diagnostics.ToArray(), result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(), emit.Success, emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources, bool EmitSuccess, IReadOnlyList<string> EmitDiagnostics);
}
