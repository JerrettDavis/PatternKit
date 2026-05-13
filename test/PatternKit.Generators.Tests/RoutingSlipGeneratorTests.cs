using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;

namespace PatternKit.Generators.Tests;

public sealed class RoutingSlipGeneratorTests
{
    [Fact]
    public void GeneratesSyncRoutingSlipFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Status);

            [GenerateRoutingSlip(typeof(Order), FactoryName = "Build")]
            public static partial class OrderSlip
            {
                [RoutingSlipStep("ship", 20)]
                private static Message<Order> Ship(Message<Order> message, MessageContext context)
                    => message.WithPayload(message.Payload with { Status = message.Payload.Status + ",ship" });

                [RoutingSlipStep("validate", 10)]
                private static Message<Order> Validate(Message<Order> message, MessageContext context)
                    => message.WithPayload(message.Payload with { Status = "validate" });
            }

            public static class Demo
            {
                public static string Run()
                {
                    var result = OrderSlip.Build().Execute(Message<Order>.Create(new Order("new")));
                    return result.Message.Payload.Status;
                }
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesSyncRoutingSlipFactory));
        var gen = new RoutingSlipGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, result => Assert.Empty(result.Diagnostics));
        var generated = Assert.Single(run.Results.SelectMany(result => result.GeneratedSources));
        Assert.Equal("OrderSlip.RoutingSlip.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        Assert.Contains(".Step(\"validate\", Validate)", text);
        Assert.Contains(".Step(\"ship\", Ship)", text);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GeneratesAsyncRoutingSlipFactory()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Status);

            [GenerateRoutingSlip(typeof(Order), AsyncFactoryName = "BuildAsync")]
            public static partial class OrderSlip
            {
                [RoutingSlipStep("validate", 10)]
                private static ValueTask<Message<Order>> ValidateAsync(Message<Order> message, MessageContext context, CancellationToken cancellationToken)
                    => new ValueTask<Message<Order>>(message.WithPayload(message.Payload with { Status = "validate" }));
            }

            public static class Demo
            {
                public static async Task<string> Run()
                {
                    var result = await OrderSlip.BuildAsync().ExecuteAsync(Message<Order>.Create(new Order("new")));
                    return result.Message.Payload.Status;
                }
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesAsyncRoutingSlipFactory));
        var gen = new RoutingSlipGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, result => Assert.Empty(result.Diagnostics));
        var generated = Assert.Single(run.Results.SelectMany(result => result.GeneratedSources));
        Assert.Contains("AsyncRoutingSlip<global::MyApp.Order>", generated.SourceText.ToString());

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void ReportsDiagnosticForNonPartialSlip()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Status);

            [GenerateRoutingSlip(typeof(Order))]
            public static class OrderSlip
            {
                [RoutingSlipStep("validate", 10)]
                private static Message<Order> Validate(Message<Order> message, MessageContext context) => message;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialSlip));
        var gen = new RoutingSlipGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = Assert.Single(run.Results.SelectMany(result => result.Diagnostics));
        Assert.Equal("PKRS001", diagnostic.Id);
    }

    [Fact]
    public void ReportsDiagnosticForMissingSteps()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Status);

            [GenerateRoutingSlip(typeof(Order))]
            public static partial class OrderSlip;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForMissingSteps));
        var gen = new RoutingSlipGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = Assert.Single(run.Results.SelectMany(result => result.Diagnostics));
        Assert.Equal("PKRS002", diagnostic.Id);
    }

    [Fact]
    public void ReportsDiagnosticForInvalidStepSignature()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Status);

            [GenerateRoutingSlip(typeof(Order))]
            public static partial class OrderSlip
            {
                [RoutingSlipStep("validate", 10)]
                private static Order Validate(Message<Order> message, MessageContext context) => message.Payload;
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidStepSignature));
        var gen = new RoutingSlipGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = Assert.Single(run.Results.SelectMany(result => result.Diagnostics));
        Assert.Equal("PKRS003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Message<>).Assembly.Location));
}
