using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.UnitOfWork;
using PatternKit.Generators.UnitOfWork;

namespace PatternKit.Examples.UnitOfWorkDemo;

public static class CheckoutUnitOfWorkDemo
{
    public static async ValueTask<CheckoutUnitOfWorkSummary> RunFluentAsync()
    {
        var log = new List<string>();
        var unit = UnitOfWork.Create()
            .Enlist("reserve-inventory", _ => { log.Add("reserve"); return default; }, _ => { log.Add("undo-reserve"); return default; })
            .Enlist("capture-payment", _ => { log.Add("capture"); return default; }, _ => { log.Add("refund"); return default; })
            .Build();
        var result = await unit.CommitAsync();
        return new(result.Committed, log);
    }

    public static async ValueTask<CheckoutUnitOfWorkSummary> RunGeneratedAsync()
    {
        GeneratedCheckoutUnitOfWork.Log.Clear();
        var result = await GeneratedCheckoutUnitOfWork.Create().CommitAsync();
        return new(result.Committed, GeneratedCheckoutUnitOfWork.Log.ToArray());
    }

    public static async ValueTask<CheckoutUnitOfWorkSummary> RunRollbackAsync()
    {
        var log = new List<string>();
        var unit = UnitOfWork.Create()
            .Enlist("reserve-inventory", _ => { log.Add("reserve"); return default; }, _ => { log.Add("undo-reserve"); return default; })
            .Enlist("persist-order", _ => throw new InvalidOperationException("database failed"))
            .Build();
        var result = await unit.CommitAsync();
        return new(result.Committed, log);
    }
}

public sealed record CheckoutUnitOfWorkSummary(bool Committed, IReadOnlyList<string> Log);

public sealed class CheckoutUnitOfWorkWorkflow
{
    public ValueTask<CheckoutUnitOfWorkSummary> RunAsync()
        => CheckoutUnitOfWorkDemo.RunFluentAsync();
}

public sealed record CheckoutUnitOfWorkDemoRunner(
    Func<ValueTask<CheckoutUnitOfWorkSummary>> RunFluentAsync,
    Func<ValueTask<CheckoutUnitOfWorkSummary>> RunGeneratedAsync,
    Func<ValueTask<CheckoutUnitOfWorkSummary>> RunRollbackAsync);

public static class CheckoutUnitOfWorkServiceCollectionExtensions
{
    public static IServiceCollection AddCheckoutUnitOfWorkDemo(this IServiceCollection services)
    {
        services.AddSingleton<CheckoutUnitOfWorkWorkflow>();
        services.AddSingleton(new CheckoutUnitOfWorkDemoRunner(
            CheckoutUnitOfWorkDemo.RunFluentAsync,
            CheckoutUnitOfWorkDemo.RunGeneratedAsync,
            CheckoutUnitOfWorkDemo.RunRollbackAsync));
        return services;
    }
}

[GenerateUnitOfWork]
public static partial class GeneratedCheckoutUnitOfWork
{
    public static List<string> Log { get; } = new();

    [UnitOfWorkStep("reserve-inventory", 10, RollbackMethodName = nameof(UndoReserve))]
    private static ValueTask Reserve(CancellationToken cancellationToken)
    {
        Log.Add("reserve");
        return default;
    }

    private static ValueTask UndoReserve(CancellationToken cancellationToken)
    {
        Log.Add("undo-reserve");
        return default;
    }

    [UnitOfWorkStep("capture-payment", 20, RollbackMethodName = nameof(Refund))]
    private static ValueTask Capture(CancellationToken cancellationToken)
    {
        Log.Add("capture");
        return default;
    }

    private static ValueTask Refund(CancellationToken cancellationToken)
    {
        Log.Add("refund");
        return default;
    }
}
