using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "ContentBasedRouter")]
public class ContentBasedRouterBenchmarks
{
    private static readonly Message<GeneratedOrder> WholesaleOrder = Message<GeneratedOrder>.Create(new("wholesale"));

    [Benchmark(Baseline = true, Description = "Fluent: create content-based router")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ContentRouter<GeneratedOrder, string> Fluent_CreateContentBasedRouter()
        => ContentRouter<GeneratedOrder, string>.Create()
            .When(static (message, _) => message.Payload.Channel == "wholesale")
            .Then(static (_, _) => "wholesale")
            .When(static (message, _) => message.Payload.Channel == "retail")
            .Then(static (_, _) => "retail")
            .Default(static (_, _) => "default")
            .Build();

    [Benchmark(Description = "Generated: create content-based router")]
    [BenchmarkCategory("Generated", "Construction")]
    public ContentRouter<GeneratedOrder, string> Generated_CreateContentBasedRouter()
        => GeneratedOrderRouter.Create();

    [Benchmark(Description = "Fluent: route wholesale order")]
    [BenchmarkCategory("Fluent", "Execution")]
    public string Fluent_RouteWholesaleOrder()
        => Fluent_CreateContentBasedRouter().Route(WholesaleOrder);

    [Benchmark(Description = "Generated: route wholesale order")]
    [BenchmarkCategory("Generated", "Execution")]
    public string Generated_RouteWholesaleOrder()
        => ContentRouterGeneratorExample.Run("wholesale");
}
