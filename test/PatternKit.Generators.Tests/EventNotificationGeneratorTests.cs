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
    [Theory]
    [InlineData("public static class NotificationHost { [EventNotificationKey] private static string Key(OrderAccepted evt) => evt.OrderId; }", "PKEN001")]
    [InlineData("public static partial class NotificationHost;", "PKEN002")]
    [InlineData("public static partial class NotificationHost { [EventNotificationKey] private static string One(OrderAccepted evt) => evt.OrderId; [EventNotificationKey] private static string Two(OrderAccepted evt) => evt.OrderId; }", "PKEN002")]
    [InlineData("public partial class NotificationHost { [EventNotificationKey] private string Key(OrderAccepted evt) => evt.OrderId; }", "PKEN003")]
    [InlineData("public static partial class NotificationHost { [EventNotificationKey] private static int Key(OrderAccepted evt) => evt.OrderId.Length; }", "PKEN003")]
    [InlineData("public static partial class NotificationHost { [EventNotificationKey] private static string Key() => string.Empty; }", "PKEN003")]
    [InlineData("public static partial class NotificationHost { [EventNotificationKey] private static string Key(string evt) => evt; }", "PKEN003")]
    [InlineData("public static partial class NotificationHost { [EventNotificationKey] private static string Key(OrderAccepted evt) => evt.OrderId; [EventNotificationCorrelation] private static int Correlation(OrderAccepted evt) => 1; }", "PKEN003")]
    [InlineData("public static partial class NotificationHost { [EventNotificationKey] private static string Key(OrderAccepted evt) => evt.OrderId; [EventNotificationRule] private static string Rule(OrderAccepted evt) => evt.OrderId; }", "PKEN003")]
    [InlineData("public static partial class NotificationHost { [EventNotificationKey] private static string Key(OrderAccepted evt) => evt.OrderId; [EventNotificationMetadata(\"source\")] private static int Source(OrderAccepted evt) => 1; }", "PKEN003")]
    [InlineData("public static partial class NotificationHost { [EventNotificationKey] private static string Key(OrderAccepted evt) => evt.OrderId; [EventNotificationMetadata(\"source\")] private static string Source(OrderAccepted evt) => evt.Source; [EventNotificationMetadata(\"SOURCE\")] private static string OtherSource(OrderAccepted evt) => evt.Source; }", "PKEN004")]
    public Task Reports_Diagnostics_For_Invalid_Event_Notification_Declarations(string declaration, string diagnosticId)
        => Given("an invalid event notification declaration", () => Compile($$"""
            using PatternKit.Generators.EventNotification;
            public sealed record OrderAccepted(string OrderId, string CorrelationId, string Source, bool NotifySubscribers);
            [GenerateEventNotification(typeof(OrderAccepted), typeof(string))]
            {{declaration}}
            """))
        .Then("diagnostics identify invalid declarations", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generates event notification defaults and host shapes")]
    [Fact]
    public Task Generates_Event_Notification_Defaults_And_Host_Shapes()
        => Given("event notification declarations with default names and different host shapes", () => Compile("""
            using PatternKit.Generators.EventNotification;
            namespace Demo;
            public sealed record OrderAccepted(string OrderId, string CorrelationId, string Source, bool NotifySubscribers);

            [GenerateEventNotification(typeof(OrderAccepted), typeof(string))]
            internal abstract partial class AbstractNotification
            {
                [EventNotificationKey]
                private static string Key(OrderAccepted evt) => evt.OrderId;
            }

            [GenerateEventNotification(typeof(OrderAccepted), typeof(string), NotificationName = "tenant\\\"notification")]
            public sealed partial class SealedNotification
            {
                [EventNotificationKey]
                private static string Key(OrderAccepted evt) => evt.OrderId;
            }

            [GenerateEventNotification(typeof(OrderAccepted), typeof(string))]
            internal partial struct StructNotification
            {
                [EventNotificationKey]
                private static string Key(OrderAccepted evt) => evt.OrderId;
            }
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractNotification", combined);
            ScenarioExpect.Contains("public sealed partial class SealedNotification", combined);
            ScenarioExpect.Contains("internal partial struct StructNotification", combined);
            ScenarioExpect.Contains("Create(\"event-notification\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"notification\")", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested event notification host wrappers")]
    [Fact]
    public Task Generates_Nested_Event_Notification_Host_Wrappers()
        => Given("nested event notification declarations", () => Compile("""
            using PatternKit.Generators.EventNotification;
            namespace Demo;
            public sealed record OrderAccepted(string OrderId, string CorrelationId, string Source, bool NotifySubscribers);

            public partial class NotificationContainer
            {
                private partial class PrivateHost
                {
                    [GenerateEventNotification(typeof(OrderAccepted), typeof(string))]
                    protected partial class ProtectedNotification
                    {
                        [EventNotificationKey]
                        private static string Key(OrderAccepted evt) => evt.OrderId;
                    }

                    [GenerateEventNotification(typeof(OrderAccepted), typeof(string))]
                    private protected partial class PrivateProtectedNotification
                    {
                        [EventNotificationKey]
                        private static string Key(OrderAccepted evt) => evt.OrderId;
                    }

                    [GenerateEventNotification(typeof(OrderAccepted), typeof(string))]
                    protected internal partial class ProtectedInternalNotification
                    {
                        [EventNotificationKey]
                        private static string Key(OrderAccepted evt) => evt.OrderId;
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class NotificationContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedNotification", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedNotification", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalNotification", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed event notification type arguments")]
    [Theory]
    [InlineData("null!", "typeof(string)")]
    [InlineData("typeof(OrderAccepted)", "null!")]
    public Task Skips_Malformed_Event_Notification_Type_Arguments(string eventType, string keyType)
        => Given("an event notification declaration with a null type argument", () => Compile($$"""
            using PatternKit.Generators.EventNotification;
            public sealed record OrderAccepted(string OrderId, string CorrelationId, string Source, bool NotifySubscribers);
            [GenerateEventNotification({{eventType}}, {{keyType}})]
            public static partial class OrderAcceptedNotification
            {
                [EventNotificationKey]
                private static string Key(OrderAccepted evt) => evt.OrderId;
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
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
