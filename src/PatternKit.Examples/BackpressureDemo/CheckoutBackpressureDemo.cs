using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Backpressure;
using PatternKit.Messaging.Reliability.Backpressure;

namespace PatternKit.Examples.BackpressureDemo;

public sealed record CheckoutWork(string OrderId, decimal Total);
public sealed record CheckoutAdmission(string OrderId, bool Accepted, string Reason);

public interface ICheckoutProcessor
{
    ValueTask<CheckoutAdmission> ProcessAsync(CheckoutWork work, CancellationToken cancellationToken = default);
}

public sealed class ScriptedCheckoutProcessor(CheckoutAdmission admission) : ICheckoutProcessor
{
    public ValueTask<CheckoutAdmission> ProcessAsync(CheckoutWork work, CancellationToken cancellationToken = default)
        => new(admission with { OrderId = work.OrderId });
}

public sealed class CheckoutBackpressureService(
    ICheckoutProcessor processor,
    BackpressurePolicy<CheckoutAdmission> policy)
{
    public async ValueTask<CheckoutAdmission> SubmitAsync(CheckoutWork work, CancellationToken cancellationToken = default)
    {
        var result = await policy.ExecuteAsync(
            async ct => await processor.ProcessAsync(work, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        if (result.Accepted && result.Value is not null)
            return result.Value;

        var reason = result.Shed ? "shed" : result.Dropped ? "dropped" : "rejected";
        return new CheckoutAdmission(work.OrderId, false, reason);
    }
}

public static class CheckoutBackpressurePolicies
{
    public static BackpressurePolicy<CheckoutAdmission> CreateFluentPolicy()
        => BackpressurePolicy<CheckoutAdmission>.Create("checkout-backpressure")
            .WithCapacity(8)
            .WithMode(BackpressureMode.Wait)
            .WithWaitTimeout(TimeSpan.FromMilliseconds(50))
            .Build();
}

[GenerateBackpressurePolicy(
    typeof(CheckoutAdmission),
    FactoryMethodName = nameof(CreateGeneratedPolicy),
    PolicyName = "checkout-backpressure",
    Capacity = 8,
    Mode = "Wait",
    WaitTimeoutMilliseconds = 50)]
public static partial class GeneratedCheckoutBackpressurePolicy;

public static class CheckoutBackpressureServiceCollectionExtensions
{
    public static IServiceCollection AddCheckoutBackpressureDemo(this IServiceCollection services)
    {
        services.AddSingleton<ICheckoutProcessor>(_ => new ScriptedCheckoutProcessor(new CheckoutAdmission("", true, "accepted")));
        services.AddSingleton(_ => GeneratedCheckoutBackpressurePolicy.CreateGeneratedPolicy());
        services.AddSingleton<CheckoutBackpressureService>();
        return services;
    }
}
