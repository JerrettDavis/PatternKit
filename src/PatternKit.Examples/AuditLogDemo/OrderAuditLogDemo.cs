using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.AuditLog;
using PatternKit.Generators.AuditLog;

namespace PatternKit.Examples.AuditLogDemo;

public static class OrderAuditLogDemo
{
    public static async ValueTask<OrderAuditLogSummary> RunFluentAsync()
    {
        var log = OrderAuditLogPolicies.CreateFluentLog();
        return await RunScenarioAsync(log, "order-100");
    }

    public static async ValueTask<OrderAuditLogSummary> RunGeneratedAsync()
        => await RunScenarioAsync(GeneratedOrderAuditLog.CreateLog(), "order-200");

    private static async ValueTask<OrderAuditLogSummary> RunScenarioAsync(IAuditLog<OrderAuditEntry, string> log, string orderId)
    {
        _ = await log.AppendAsync(OrderAuditEntry.Create(orderId, "submitted", "api"));
        _ = await log.AppendAsync(OrderAuditEntry.Create(orderId, "approved", "risk"));
        var entries = await log.QueryAsync(entry => entry.OrderId == orderId);
        return new(log.Name, orderId, entries.Count, entries[^1].Action);
    }
}

public sealed record OrderAuditEntry(string EntryId, string OrderId, string Action, string Actor, DateTimeOffset RecordedAt)
{
    public static OrderAuditEntry Create(string orderId, string action, string actor)
        => new($"{orderId}-{action}", orderId, action, actor, DateTimeOffset.UtcNow);
}

public sealed record OrderAuditLogSummary(string LogName, string OrderId, int EntryCount, string LastAction);

public static class OrderAuditLogPolicies
{
    public static InMemoryAuditLog<OrderAuditEntry, string> CreateFluentLog()
        => InMemoryAuditLog<OrderAuditEntry, string>.Create("order-audit", static entry => entry.EntryId).Build();
}

public sealed class OrderAuditLogWorkflow
{
    private readonly IAuditLog<OrderAuditEntry, string> _log;

    public OrderAuditLogWorkflow(IAuditLog<OrderAuditEntry, string> log)
    {
        _log = log;
    }

    public async ValueTask<OrderAuditLogSummary> SubmitAndApproveAsync(string orderId, CancellationToken cancellationToken = default)
    {
        _ = await _log.AppendAsync(OrderAuditEntry.Create(orderId, "submitted", "api"), cancellationToken).ConfigureAwait(false);
        _ = await _log.AppendAsync(OrderAuditEntry.Create(orderId, "approved", "risk"), cancellationToken).ConfigureAwait(false);
        var entries = await _log.QueryAsync(entry => entry.OrderId == orderId, cancellationToken).ConfigureAwait(false);
        return new(_log.Name, orderId, entries.Count, entries[^1].Action);
    }
}

public sealed record OrderAuditLogDemoRunner(
    Func<ValueTask<OrderAuditLogSummary>> RunFluentAsync,
    Func<ValueTask<OrderAuditLogSummary>> RunGeneratedAsync);

public static class OrderAuditLogServiceCollectionExtensions
{
    public static IServiceCollection AddOrderAuditLogDemo(this IServiceCollection services)
    {
        services.AddScoped<IAuditLog<OrderAuditEntry, string>>(_ => OrderAuditLogPolicies.CreateFluentLog());
        services.AddScoped<OrderAuditLogWorkflow>();
        services.AddSingleton(new OrderAuditLogDemoRunner(
            OrderAuditLogDemo.RunFluentAsync,
            OrderAuditLogDemo.RunGeneratedAsync));
        return services;
    }
}

[GenerateAuditLog(typeof(OrderAuditEntry), typeof(string), FactoryName = "CreateLog", LogName = "order-audit")]
public static partial class GeneratedOrderAuditLog
{
    [AuditLogKeySelector]
    private static string SelectKey(OrderAuditEntry entry) => entry.EntryId;
}
