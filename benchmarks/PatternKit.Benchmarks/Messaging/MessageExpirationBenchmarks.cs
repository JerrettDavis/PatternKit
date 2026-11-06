using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "MessageExpiration")]
public class MessageExpirationBenchmarks
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly ExpiringOrderCommand Command = new("order-100", "customer-42");

    [Benchmark(Baseline = true, Description = "Fluent: create message expiration")]
    [BenchmarkCategory("Fluent", "Construction")]
    public MessageExpiration<ExpiringOrderCommand> Fluent_CreateMessageExpiration()
        => OrderMessageExpirations.Create(Now);

    [Benchmark(Description = "Generated: create message expiration")]
    [BenchmarkCategory("Generated", "Construction")]
    public MessageExpiration<ExpiringOrderCommand> Generated_CreateMessageExpiration()
        => GeneratedOrderMessageExpiration.Create();

    [Benchmark(Description = "Fluent: stamp and evaluate order command")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderExpirationSummary Fluent_StampAndEvaluate()
        => OrderMessageExpirationExampleRunner.RunFluent(Command, Now);

    [Benchmark(Description = "Generated: stamp and evaluate order command")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderExpirationSummary Generated_StampAndEvaluate()
    {
        var expiration = GeneratedOrderMessageExpiration.Create();
        var message = expiration.Stamp(Message<ExpiringOrderCommand>.Create(Command));
        var result = expiration.Evaluate(message);
        return new OrderExpirationSummary(result.Expired, result.ExpiresAt, result.Reason);
    }
}
