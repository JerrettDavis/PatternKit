using BenchmarkDotNet.Attributes;
using PatternKit.Application.Repository;
using PatternKit.Application.TransactionScript;
using PatternKit.Examples.TransactionScriptDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "TransactionScript")]
public class TransactionScriptBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create transaction script")]
    [BenchmarkCategory("Fluent", "Construction")]
    public TransactionScript<SubmitOrderRequest, SubmitOrderReceipt> Fluent_CreateScript()
        => OrderTransactionScriptPolicies.CreateFluentScript(
            InMemoryRepository<SubmittedOrder, string>.Create(static order => order.OrderId).Build());

    [Benchmark(Description = "Generated: create transaction script")]
    [BenchmarkCategory("Generated", "Construction")]
    public ITransactionScript<SubmitOrderRequest, SubmitOrderReceipt> Generated_CreateScript()
        => GeneratedSubmitOrderScript.CreateScript();

    [Benchmark(Description = "Fluent: submit order")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<OrderTransactionScriptSummary> Fluent_SubmitOrder()
        => OrderTransactionScriptDemo.RunFluentAsync();

    [Benchmark(Description = "Generated: submit order")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<OrderTransactionScriptSummary> Generated_SubmitOrder()
        => OrderTransactionScriptDemo.RunGeneratedAsync();
}
