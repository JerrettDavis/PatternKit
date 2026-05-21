using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class MessageStoreGeneratorTests
{
    [Scenario("GeneratesMessageStoreFactory")]
    [Fact]
    public void GeneratesMessageStoreFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.Storage;

            namespace MyApp;

            public sealed record Order(string Id, decimal Total);

            [GenerateMessageStore(typeof(Order), FactoryName = "Build", StoreName = "order-audit")]
            public static partial class OrderMessageStore
            {
                [MessageStoreIdentity]
                private static string Identity(Message<Order> message, MessageContext context)
                    => message.Headers.MessageId!;

                [MessageStoreRetention]
                private static bool Retain(StoredMessage<Order> stored)
                    => stored.Message.Payload.Total <= 500m;
            }

            public static class Demo
            {
                public static bool Run()
                    => OrderMessageStore.Build().Append(Message<Order>.Create(new Order("o-1", 100m)).WithMessageId("m-1")).Stored;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesMessageStoreFactory));
        var gen = new MessageStoreGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderMessageStore.MessageStore.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("MessageStore<global::MyApp.Order>", text);
        ScenarioExpect.Contains(".IdentifyBy(Identity)", text);
        ScenarioExpect.Contains(".RetainWhen(Retain)", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ReportsDiagnosticForNonPartialStore")]
    [Fact]
    public void ReportsDiagnosticForNonPartialStore()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;
            public sealed record Order(string Id);
            [GenerateMessageStore(typeof(Order))]
            public static class OrderStore;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialStore));
        var gen = new MessageStoreGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKMS001", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForInvalidIdentity")]
    [Fact]
    public void ReportsDiagnosticForInvalidIdentity()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;
            public sealed record Order(string Id);
            [GenerateMessageStore(typeof(Order))]
            public static partial class OrderStore
            {
                [MessageStoreIdentity]
                private static int Identity(Message<Order> message, MessageContext context) => 1;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidIdentity));
        var gen = new MessageStoreGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKMS002", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForInvalidRetention")]
    [Fact]
    public void ReportsDiagnosticForInvalidRetention()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging.Storage;

            namespace MyApp;
            public sealed record Order(string Id);
            [GenerateMessageStore(typeof(Order))]
            public static partial class OrderStore
            {
                [MessageStoreRetention]
                private static string Retain(StoredMessage<Order> stored) => "yes";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidRetention));
        var gen = new MessageStoreGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKMS003", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForDuplicateHooks")]
    [Fact]
    public void ReportsDiagnosticForDuplicateHooks()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;
            public sealed record Order(string Id);
            [GenerateMessageStore(typeof(Order))]
            public static partial class OrderStore
            {
                [MessageStoreIdentity]
                private static string One(Message<Order> message, MessageContext context) => "1";
                [MessageStoreIdentity]
                private static string Two(Message<Order> message, MessageContext context) => "2";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForDuplicateHooks));
        var gen = new MessageStoreGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKMS004", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Storage.MessageStore<>).Assembly.Location));
}
