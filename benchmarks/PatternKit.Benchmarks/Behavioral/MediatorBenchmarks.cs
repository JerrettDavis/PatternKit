using BenchmarkDotNet.Attributes;
using PatternKit.Behavioral.Mediator;
using PatternKit.Generators.Messaging;

[assembly: GenerateDispatcher(
    Namespace = "PatternKit.Benchmarks.Behavioral.GeneratedMediator",
    Name = "BenchmarkDispatcher",
    IncludeStreaming = false)]

namespace PatternKit.Benchmarks.Behavioral;

using GeneratedMediator;

[BenchmarkCategory("Behavioral", "GoF", "Mediator")]
public class MediatorBenchmarks
{
    private static readonly SubmitInvoice Request = new("INV-100", 125m);

    [Benchmark(Baseline = true, Description = "Fluent: create mediator")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Mediator Fluent_CreateMediator()
        => Mediator.Create()
            .Command<SubmitInvoice, InvoiceSubmitted>(static (in SubmitInvoice request) =>
                new InvoiceSubmitted(request.InvoiceId, request.Amount, true))
            .Build();

    [Benchmark(Description = "Generated: create dispatcher mediator")]
    [BenchmarkCategory("Generated", "Construction")]
    public BenchmarkDispatcher Generated_CreateMediator()
        => BenchmarkDispatcher.Create()
            .Command<SubmitInvoice, InvoiceSubmitted>(static (request, _) =>
                new ValueTask<InvoiceSubmitted>(new InvoiceSubmitted(request.InvoiceId, request.Amount, true)))
            .Build();

    [Benchmark(Description = "Fluent: send mediator command")]
    [BenchmarkCategory("Fluent", "Execution")]
    public InvoiceSubmitted? Fluent_SendCommand()
        => Fluent_CreateMediator().Send<SubmitInvoice, InvoiceSubmitted>(Request).GetAwaiter().GetResult();

    [Benchmark(Description = "Generated: send mediator command")]
    [BenchmarkCategory("Generated", "Execution")]
    public InvoiceSubmitted Generated_SendCommand()
        => Generated_CreateMediator().Send<SubmitInvoice, InvoiceSubmitted>(Request).GetAwaiter().GetResult();
}

public readonly record struct SubmitInvoice(string InvoiceId, decimal Amount);

public readonly record struct InvoiceSubmitted(string InvoiceId, decimal Amount, bool Accepted);
