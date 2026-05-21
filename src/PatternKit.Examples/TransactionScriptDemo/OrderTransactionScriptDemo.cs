using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.Repository;
using PatternKit.Application.TransactionScript;
using PatternKit.Application.UnitOfWork;
using PatternKit.Generators.TransactionScript;

namespace PatternKit.Examples.TransactionScriptDemo;

public static class OrderTransactionScriptDemo
{
    public static async ValueTask<OrderTransactionScriptSummary> RunFluentAsync()
    {
        var repository = InMemoryRepository<SubmittedOrder, string>.Create(static order => order.OrderId).Build();
        var script = OrderTransactionScriptPolicies.CreateFluentScript(repository);
        var result = await script.ExecuteAsync(new SubmitOrderRequest("order-100", "customer-10", 125m));
        return new(result.Succeeded, result.Response?.OrderId ?? "", (await repository.ListAsync()).Count);
    }

    public static async ValueTask<OrderTransactionScriptSummary> RunGeneratedAsync()
    {
        GeneratedSubmitOrderScript.Repository = InMemoryRepository<SubmittedOrder, string>.Create(static order => order.OrderId).Build();
        var result = await GeneratedSubmitOrderScript.CreateScript().ExecuteAsync(new SubmitOrderRequest("order-200", "customer-20", 75m));
        return new(result.Succeeded, result.Response?.OrderId ?? "", (await GeneratedSubmitOrderScript.Repository.ListAsync()).Count);
    }
}

public sealed record SubmitOrderRequest(string OrderId, string CustomerId, decimal Total);

public sealed record SubmitOrderReceipt(string OrderId, decimal Total);

public sealed record SubmittedOrder(string OrderId, string CustomerId, decimal Total);

public sealed record OrderTransactionScriptSummary(bool Submitted, string OrderId, int RepositoryCount);

public static class OrderTransactionScriptPolicies
{
    public static TransactionScript<SubmitOrderRequest, SubmitOrderReceipt> CreateFluentScript(IRepository<SubmittedOrder, string> repository)
    {
        if (repository is null)
            throw new ArgumentNullException(nameof(repository));

        return TransactionScript<SubmitOrderRequest, SubmitOrderReceipt>.Create("submit-order")
            .Validate(static request => request.Total <= 0m
                ? [new TransactionScriptError("total", "Order total must be positive.")]
                : [])
            .Execute(async (request, cancellationToken) =>
            {
                var unit = UnitOfWork.Create()
                    .Enlist("persist-order", async ct =>
                    {
                        var result = await repository.AddAsync(new SubmittedOrder(request.OrderId, request.CustomerId, request.Total), ct).ConfigureAwait(false);
                        if (!result.Succeeded)
                            throw new InvalidOperationException(result.Reason);
                    })
                    .Build();

                var commit = await unit.CommitAsync(cancellationToken).ConfigureAwait(false);
                if (!commit.Committed)
                    throw commit.Exception ?? new InvalidOperationException("Order transaction failed.");

                return new SubmitOrderReceipt(request.OrderId, request.Total);
            })
            .Build();
    }
}

public sealed class OrderTransactionScriptWorkflow
{
    private readonly ITransactionScript<SubmitOrderRequest, SubmitOrderReceipt> _script;

    public OrderTransactionScriptWorkflow(ITransactionScript<SubmitOrderRequest, SubmitOrderReceipt> script)
    {
        _script = script;
    }

    public async ValueTask<OrderTransactionScriptSummary> SubmitAsync(SubmitOrderRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _script.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return new(result.Succeeded, result.Response?.OrderId ?? "", result.Succeeded ? 1 : 0);
    }
}

public sealed record OrderTransactionScriptDemoRunner(
    Func<ValueTask<OrderTransactionScriptSummary>> RunFluentAsync,
    Func<ValueTask<OrderTransactionScriptSummary>> RunGeneratedAsync);

public static class OrderTransactionScriptServiceCollectionExtensions
{
    public static IServiceCollection AddOrderTransactionScriptDemo(this IServiceCollection services)
    {
        services.AddScoped<IRepository<SubmittedOrder, string>>(_ => InMemoryRepository<SubmittedOrder, string>.Create(static order => order.OrderId).Build());
        services.AddScoped<ITransactionScript<SubmitOrderRequest, SubmitOrderReceipt>>(sp =>
            OrderTransactionScriptPolicies.CreateFluentScript(sp.GetRequiredService<IRepository<SubmittedOrder, string>>()));
        services.AddScoped<OrderTransactionScriptWorkflow>();
        services.AddSingleton(new OrderTransactionScriptDemoRunner(
            OrderTransactionScriptDemo.RunFluentAsync,
            OrderTransactionScriptDemo.RunGeneratedAsync));
        return services;
    }
}

[GenerateTransactionScript(typeof(SubmitOrderRequest), typeof(SubmitOrderReceipt), FactoryName = "CreateScript", ScriptName = "submit-order")]
public static partial class GeneratedSubmitOrderScript
{
    public static IRepository<SubmittedOrder, string> Repository { get; set; } =
        InMemoryRepository<SubmittedOrder, string>.Create(static order => order.OrderId).Build();

    [TransactionScriptValidator]
    private static IEnumerable<TransactionScriptError> Validate(SubmitOrderRequest request)
        => request.Total <= 0m
            ? [new TransactionScriptError("total", "Order total must be positive.")]
            : [];

    [TransactionScriptHandler]
    private static async ValueTask<SubmitOrderReceipt> Handle(SubmitOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await Repository.AddAsync(new SubmittedOrder(request.OrderId, request.CustomerId, request.Total), cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Reason);

        return new SubmitOrderReceipt(request.OrderId, request.Total);
    }
}
