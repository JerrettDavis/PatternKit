using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ScatterGatherGeneratorTests
{
    [Scenario("GeneratesScatterGatherFactory")]
    [Fact]
    public void GeneratesScatterGatherFactory()
    {
        var source = """
            using System.Collections.Generic;
            using System.Linq;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.Routing;

            namespace MyApp;
            public sealed record Request(bool IncludeSecondary);
            public sealed record Quote(decimal Price);
            public sealed record Summary(decimal BestPrice);

            [GenerateScatterGather(typeof(Request), typeof(Quote), typeof(Summary), FactoryName = "Build", Name = "quotes")]
            public static partial class QuoteScatterGather
            {
                [ScatterGatherRecipient("primary", 20)]
                private static ScatterGatherReply<Quote> Primary(Message<Request> message, MessageContext context) => ScatterGatherReply<Quote>.Success(new Quote(12m));

                [ScatterGatherRecipient("secondary", 10, "IncludeSecondary")]
                private static ScatterGatherReply<Quote> Secondary(Message<Request> message, MessageContext context) => ScatterGatherReply<Quote>.Success(new Quote(10m));

                private static bool IncludeSecondary(Message<Request> message, MessageContext context) => message.Payload.IncludeSecondary;

                [ScatterGatherAggregator]
                private static Summary Aggregate(IReadOnlyList<ScatterGatherReply<Quote>> replies, Message<Request> message, MessageContext context)
                    => new Summary(replies.Where(reply => reply.Accepted).Min(reply => reply.Response!.Price));
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesScatterGatherFactory));
        _ = RoslynTestHelpers.Run(comp, new ScatterGatherGenerator(), out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("QuoteScatterGather.ScatterGather.g.cs", generated.HintName);
        ScenarioExpect.Contains("ScatterGather<global::MyApp.Request, global::MyApp.Quote, global::MyApp.Summary>", text);
        ScenarioExpect.True(text.IndexOf("secondary", StringComparison.Ordinal) < text.IndexOf("primary", StringComparison.Ordinal));
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("ReportsScatterGatherDiagnostics")]
    [Theory]
    [InlineData("public static class QuoteScatterGather { }", "PKSCG001")]
    [InlineData("public static partial class QuoteScatterGather { }", "PKSCG002")]
    public void ReportsScatterGatherDiagnostics(string declaration, string expected)
    {
        var source = $$"""
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record Request;
            public sealed record Quote;
            public sealed record Summary;
            [GenerateScatterGather(typeof(Request), typeof(Quote), typeof(Summary))]
            {{declaration}}
            """;

        var comp = CreateCompilation(source, nameof(ReportsScatterGatherDiagnostics) + expected);
        _ = RoslynTestHelpers.Run(comp, new ScatterGatherGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    [Scenario("ReportsInvalidScatterGatherRecipient")]
    [Fact]
    public void ReportsInvalidScatterGatherRecipient()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record Request;
            public sealed record Quote;
            public sealed record Summary;
            [GenerateScatterGather(typeof(Request), typeof(Quote), typeof(Summary))]
            public static partial class QuoteScatterGather
            {
                [ScatterGatherRecipient("primary")]
                private static string Primary(Message<Request> message, MessageContext context) => "bad";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsInvalidScatterGatherRecipient));
        _ = RoslynTestHelpers.Run(comp, new ScatterGatherGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKSCG003", diagnostic.Id);
    }

    [Scenario("ReportsInvalidScatterGatherAggregator")]
    [Fact]
    public void ReportsInvalidScatterGatherAggregator()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.Routing;
            namespace MyApp;
            public sealed record Request;
            public sealed record Quote;
            public sealed record Summary;
            [GenerateScatterGather(typeof(Request), typeof(Quote), typeof(Summary))]
            public static partial class QuoteScatterGather
            {
                [ScatterGatherRecipient("primary")]
                private static ScatterGatherReply<Quote> Primary(Message<Request> message, MessageContext context) => ScatterGatherReply<Quote>.Success(new Quote());
                [ScatterGatherAggregator]
                private static string Aggregate() => "bad";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsInvalidScatterGatherAggregator));
        _ = RoslynTestHelpers.Run(comp, new ScatterGatherGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKSCG004", diagnostic.Id);
    }

    [Scenario("ReportsDuplicateScatterGatherRecipient")]
    [Fact]
    public void ReportsDuplicateScatterGatherRecipient()
    {
        var source = """
            using System.Collections.Generic;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.Routing;
            namespace MyApp;
            public sealed record Request;
            public sealed record Quote;
            public sealed record Summary;
            [GenerateScatterGather(typeof(Request), typeof(Quote), typeof(Summary))]
            public static partial class QuoteScatterGather
            {
                [ScatterGatherRecipient("primary", 1)]
                private static ScatterGatherReply<Quote> One(Message<Request> message, MessageContext context) => ScatterGatherReply<Quote>.Success(new Quote());
                [ScatterGatherRecipient("primary", 2)]
                private static ScatterGatherReply<Quote> Two(Message<Request> message, MessageContext context) => ScatterGatherReply<Quote>.Success(new Quote());
                [ScatterGatherAggregator]
                private static Summary Aggregate(IReadOnlyList<ScatterGatherReply<Quote>> replies, Message<Request> message, MessageContext context) => new Summary();
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDuplicateScatterGatherRecipient));
        _ = RoslynTestHelpers.Run(comp, new ScatterGatherGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKSCG005", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.Routing.ScatterGather<,,>).Assembly.Location));
}
