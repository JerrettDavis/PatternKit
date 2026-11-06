using BenchmarkDotNet.Attributes;
using PatternKit.Application.AuditLog;
using PatternKit.Examples.AuditLogDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "AuditLog")]
public class AuditLogBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create audit log")]
    [BenchmarkCategory("Fluent", "Construction")]
    public IAuditLog<OrderAuditEntry, string> Fluent_CreateLog()
        => OrderAuditLogPolicies.CreateFluentLog();

    [Benchmark(Description = "Generated: create audit log")]
    [BenchmarkCategory("Generated", "Construction")]
    public IAuditLog<OrderAuditEntry, string> Generated_CreateLog()
        => GeneratedOrderAuditLog.CreateLog();

    [Benchmark(Description = "Fluent: submit and approve order")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<OrderAuditLogSummary> Fluent_SubmitAndApprove()
        => OrderAuditLogDemo.RunFluentAsync();

    [Benchmark(Description = "Generated: submit and approve order")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<OrderAuditLogSummary> Generated_SubmitAndApprove()
        => OrderAuditLogDemo.RunGeneratedAsync();
}
