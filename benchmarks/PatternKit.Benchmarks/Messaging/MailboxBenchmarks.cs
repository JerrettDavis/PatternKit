using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Mailboxes;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "Mailbox")]
public class MailboxBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create mailbox")]
    [BenchmarkCategory("Fluent", "Construction")]
    public int Fluent_CreateMailbox()
    {
        using var mailbox = Mailbox<MailboxWorkItem>.Create(static (_, _, _) => default)
            .Bounded(8, MailboxBackpressurePolicy.Wait)
            .OnError(MailboxErrorPolicy.Continue)
            .Build();

        return 8;
    }

    [Benchmark(Description = "Generated: create mailbox")]
    [BenchmarkCategory("Generated", "Construction")]
    public int Generated_CreateMailbox()
    {
        using var mailbox = GeneratedMailboxWorkQueue.CreateWorkQueue();
        return 8;
    }

    [Benchmark(Description = "Fluent: process mailbox work")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<IReadOnlyList<string>> Fluent_ProcessMailboxWork()
        => MailboxExample.RunFluentAsync();

    [Benchmark(Description = "Generated: process mailbox work")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<IReadOnlyList<string>> Generated_ProcessMailboxWork()
        => MailboxExample.RunGeneratedAsync();
}
