using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PatternKit.Behavioral.Mediator;
using PatternKit.Examples.Messaging;
using SourceGenerated = PatternKit.Examples.Messaging.SourceGenerated;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "CQRS")]
public class CqrsBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create CQRS mediator")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Mediator Fluent_CreateMediator()
    {
        var log = new List<string>();
        var orders = new Dictionary<int, CqrsOrder>();
        var nextOrderId = 1000;

        return Mediator.Create()
            .Pre((in object? request, CancellationToken _) =>
            {
                log.Add($"pre:{request?.GetType().Name}");
                return ValueTask.CompletedTask;
            })
            .Post((in object? request, object? response, CancellationToken _) =>
            {
                log.Add($"post:{request?.GetType().Name}:{response?.GetType().Name ?? "void"}");
                return ValueTask.CompletedTask;
            })
            .Command<CreateCqrsOrder, CqrsOrder>((in CreateCqrsOrder command, CancellationToken _) =>
            {
                var order = new CqrsOrder(++nextOrderId, command.CustomerId, command.Lines, command.Lines.Sum(static line => line.Quantity * line.UnitPrice));
                orders[order.Id] = order;
                return new ValueTask<CqrsOrder>(order);
            })
            .Command<GetCqrsOrder, CqrsOrder?>((in GetCqrsOrder query, CancellationToken _) =>
            {
                orders.TryGetValue(query.OrderId, out var order);
                return new ValueTask<CqrsOrder?>(order);
            })
            .Notification<CqrsOrderCreated>((in CqrsOrderCreated notification, CancellationToken _) =>
            {
                log.Add($"event:order-created:{notification.OrderId}");
                return ValueTask.CompletedTask;
            })
            .Build();
    }

    [Benchmark(Description = "Generated: create CQRS dispatcher services")]
    [BenchmarkCategory("Generated", "Construction")]
    public int Generated_CreateDispatcherServices()
    {
        using var provider = CreateGeneratedProvider();
        return provider.GetServices<object>().Count();
    }

    [Benchmark(Description = "Fluent: command query workflow")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<CqrsSummary> Fluent_CommandQueryWorkflow()
        => CqrsPatternExample.RunFluentAsync();

    [Benchmark(Description = "Generated: command query workflow")]
    [BenchmarkCategory("Generated", "Execution")]
    public async ValueTask<CqrsSummary> Generated_CommandQueryWorkflow()
    {
        await using var provider = CreateGeneratedProvider();
        return await CqrsPatternExample.RunSourceGeneratedAsync(provider);
    }

    private static ServiceProvider CreateGeneratedProvider()
    {
        var services = new ServiceCollection()
            .AddSourceGeneratedCqrsServices();

        services.Replace(ServiceDescriptor.Singleton<SourceGenerated.ILogger, QuietLogger>());

        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class QuietLogger : SourceGenerated.ILogger
    {
        private readonly List<string> _logs = [];

        public void Log(string message) => _logs.Add(message);

        public List<string> GetLogs() => _logs;
    }
}
