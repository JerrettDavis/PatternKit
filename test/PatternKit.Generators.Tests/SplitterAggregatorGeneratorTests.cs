using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class SplitterAggregatorGeneratorTests
{
    [Scenario("Generates typed splitter factory")]
    [Fact]
    public void GeneratesTypedSplitterFactory()
    {
        var source = """
            using System.Collections.Generic;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Id, IReadOnlyList<Line> Lines);
            public sealed record Line(string OrderId, decimal Amount);

            [GenerateSplitter(typeof(Order), typeof(Line), FactoryName = "CreateLineSplitter")]
            public static partial class OrderLineSplitter
            {
                [SplitterProjection]
                private static IEnumerable<Line> ProjectLines(Message<Order> message, MessageContext context)
                    => message.Payload.Lines;
            }

            public static class Demo
            {
                public static int Run()
                {
                    var splitter = OrderLineSplitter.CreateLineSplitter();
                    var parts = splitter.Split(Message<Order>.Create(new Order("order-1", [new Line("order-1", 12m)])));
                    return parts.Count;
                }
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesTypedSplitterFactory));
        var gen = new SplitterAggregatorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderLineSplitter.Splitter.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("CreateLineSplitter()", text);
        ScenarioExpect.Contains(".Use(ProjectLines)", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Generates typed aggregator factory")]
    [Fact]
    public void GeneratesTypedAggregatorFactory()
    {
        var source = """
            using System.Collections.Generic;
            using System.Linq;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Line(string OrderId, decimal Amount);

            [GenerateAggregator(typeof(string), typeof(Line), typeof(decimal), FactoryName = "CreateLineTotal", DuplicatePolicy = "Replace")]
            public static partial class OrderLineAggregator
            {
                [AggregatorCorrelation]
                private static string Correlate(Message<Line> message, MessageContext context)
                    => message.Payload.OrderId;

                [AggregatorCompletion]
                private static bool Complete(string key, IReadOnlyList<Message<Line>> messages, MessageContext context)
                    => messages.Count == 2;

                [AggregatorProjection]
                private static decimal Project(string key, IReadOnlyList<Message<Line>> messages, MessageContext context)
                    => messages.Sum(message => message.Payload.Amount);
            }

            public static class Demo
            {
                public static decimal Run()
                {
                    var aggregator = OrderLineAggregator.CreateLineTotal();
                    aggregator.Add(Message<Line>.Create(new Line("order-1", 12m)).WithMessageId("line-1"));
                    var result = aggregator.Add(Message<Line>.Create(new Line("order-1", 15m)).WithMessageId("line-2"));
                    return result.Result;
                }
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesTypedAggregatorFactory));
        var gen = new SplitterAggregatorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderLineAggregator.Aggregator.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("CreateLineTotal()", text);
        ScenarioExpect.Contains(".KeyBy(Correlate)", text);
        ScenarioExpect.Contains(".CompleteWhen(Complete)", text);
        ScenarioExpect.Contains(".Project(Project)", text);
        ScenarioExpect.Contains("DuplicateMessagePolicy.Replace", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial splitter contract")]
    [Fact]
    public void ReportsDiagnosticForNonPartialSplitterContract()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order;
            public sealed record Line;

            [GenerateSplitter(typeof(Order), typeof(Line))]
            public static class OrderLineSplitter;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialSplitterContract));
        var gen = new SplitterAggregatorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKSA001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for missing splitter projection")]
    [Fact]
    public void ReportsDiagnosticForMissingSplitterProjection()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order;
            public sealed record Line;

            [GenerateSplitter(typeof(Order), typeof(Line))]
            public static partial class OrderLineSplitter;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForMissingSplitterProjection));
        var gen = new SplitterAggregatorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKSA002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid aggregator projection")]
    [Fact]
    public void ReportsDiagnosticForInvalidAggregatorProjection()
    {
        var source = """
            using System.Collections.Generic;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Line(string OrderId, decimal Amount);

            [GenerateAggregator(typeof(string), typeof(Line), typeof(decimal))]
            public static partial class OrderLineAggregator
            {
                [AggregatorCorrelation]
                private static string Correlate(Message<Line> message, MessageContext context) => message.Payload.OrderId;

                [AggregatorCompletion]
                private static bool Complete(string key, IReadOnlyList<Message<Line>> messages, MessageContext context) => true;

                [AggregatorProjection]
                private static string Project(string key, IReadOnlyList<Message<Line>> messages, MessageContext context) => "wrong";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidAggregatorProjection));
        var gen = new SplitterAggregatorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKSA005", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid duplicate policy")]
    [Fact]
    public void ReportsDiagnosticForInvalidDuplicatePolicy()
    {
        var source = """
            using System.Collections.Generic;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Line(string OrderId, decimal Amount);

            [GenerateAggregator(typeof(string), typeof(Line), typeof(decimal), DuplicatePolicy = "Drop")]
            public static partial class OrderLineAggregator
            {
                [AggregatorCorrelation]
                private static string Correlate(Message<Line> message, MessageContext context) => message.Payload.OrderId;

                [AggregatorCompletion]
                private static bool Complete(string key, IReadOnlyList<Message<Line>> messages, MessageContext context) => true;

                [AggregatorProjection]
                private static decimal Project(string key, IReadOnlyList<Message<Line>> messages, MessageContext context) => 0m;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidDuplicatePolicy));
        var gen = new SplitterAggregatorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKSA006", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid splitter projection")]
    [Fact]
    public void ReportsDiagnosticForInvalidSplitterProjection()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order;
            public sealed record Line;

            [GenerateSplitter(typeof(Order), typeof(Line))]
            public static partial class OrderLineSplitter
            {
                [SplitterProjection]
                private static string ProjectLines(Message<Order> message, MessageContext context) => "wrong";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidSplitterProjection));
        var gen = new SplitterAggregatorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKSA003", diagnostic.Id);
    }

    [Scenario("Generates splitter factory from concrete enumerable projection")]
    [Fact]
    public void GeneratesSplitterFactoryFromConcreteEnumerableProjection()
    {
        var source = """
            using System.Collections.Generic;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);
            public sealed record Line(string OrderId);

            [GenerateSplitter(typeof(Order), typeof(Line))]
            public static partial class OrderLineSplitter
            {
                [SplitterProjection]
                private static List<Line> ProjectLines(Message<Order> message, MessageContext context)
                    => [new(message.Payload.Id)];
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesSplitterFactoryFromConcreteEnumerableProjection));
        var gen = new SplitterAggregatorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderLineSplitter.Splitter.g.cs", generated.HintName);
        ScenarioExpect.Contains(".Use(ProjectLines)", generated.SourceText.ToString());

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial aggregator contract")]
    [Fact]
    public void ReportsDiagnosticForNonPartialAggregatorContract()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Line;

            [GenerateAggregator(typeof(string), typeof(Line), typeof(decimal))]
            public static class OrderLineAggregator;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialAggregatorContract));
        var gen = new SplitterAggregatorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKSA001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for missing aggregator methods")]
    [Fact]
    public void ReportsDiagnosticForMissingAggregatorMethods()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Line;

            [GenerateAggregator(typeof(string), typeof(Line), typeof(decimal))]
            public static partial class OrderLineAggregator;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForMissingAggregatorMethods));
        var gen = new SplitterAggregatorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKSA004", diagnostic.Id);
    }

    [Scenario("Reports diagnostics for invalid aggregator correlation and completion")]
    [Fact]
    public void ReportsDiagnosticsForInvalidAggregatorCorrelationAndCompletion()
    {
        var source = """
            using System.Collections.Generic;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Line(string OrderId, decimal Amount);

            [GenerateAggregator(typeof(string), typeof(Line), typeof(decimal))]
            public static partial class OrderLineAggregator
            {
                [AggregatorCorrelation]
                private static int Correlate(Message<Line> message, MessageContext context) => 1;

                [AggregatorCompletion]
                private static bool Complete(string key, Message<Line>[] messages, MessageContext context) => true;

                [AggregatorProjection]
                private static decimal Project(string key, IReadOnlyList<Message<Line>> messages, MessageContext context) => 0m;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticsForInvalidAggregatorCorrelationAndCompletion));
        var gen = new SplitterAggregatorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(result => result.Diagnostics).ToArray();
        ScenarioExpect.Equal(2, diagnostics.Length);
        ScenarioExpect.All(diagnostics, diagnostic => ScenarioExpect.Equal("PKSA005", diagnostic.Id));
    }

    [Scenario("Generates aggregator factory with case-insensitive duplicate policies")]
    [Theory]
    [InlineData("ignore", "DuplicateMessagePolicy.Ignore")]
    [InlineData("INCLUDE", "DuplicateMessagePolicy.Include")]
    public void GeneratesAggregatorFactoryWithCaseInsensitiveDuplicatePolicies(string policy, string expected)
    {
        var source = $$"""
            using System.Collections.Generic;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Line(string OrderId, decimal Amount);

            [GenerateAggregator(typeof(string), typeof(Line), typeof(decimal), DuplicatePolicy = "{{policy}}")]
            public static partial class OrderLineAggregator
            {
                [AggregatorCorrelation]
                private static string Correlate(Message<Line> message, MessageContext context) => message.Payload.OrderId;

                [AggregatorCompletion]
                private static bool Complete(string key, IReadOnlyList<Message<Line>> messages, MessageContext context) => true;

                [AggregatorProjection]
                private static decimal Project(string key, IReadOnlyList<Message<Line>> messages, MessageContext context) => 0m;
            }
            """;

        var comp = CreateCompilation(source, $"{nameof(GeneratesAggregatorFactoryWithCaseInsensitiveDuplicatePolicies)}_{policy}");
        var gen = new SplitterAggregatorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Contains(expected, generated.SourceText.ToString());

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Message<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
            ]);
}
