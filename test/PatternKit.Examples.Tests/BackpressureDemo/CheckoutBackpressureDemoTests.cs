using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.BackpressureDemo;
using PatternKit.Messaging.Reliability.Backpressure;
using TinyBDD;

namespace PatternKit.Examples.Tests.BackpressureDemo;

public sealed class CheckoutBackpressureDemoTests
{
    [Scenario("Checkout backpressure accepts work through fluent and generated policies")]
    [Fact]
    public async Task Checkout_Backpressure_Accepts_Work_Through_Fluent_And_Generated_Policies()
    {
        var work = new CheckoutWork("ORDER-100", 42m);
        var processor = new ScriptedCheckoutProcessor(new CheckoutAdmission("", true, "accepted"));
        var fluent = new CheckoutBackpressureService(processor, CheckoutBackpressurePolicies.CreateFluentPolicy());
        var generated = new CheckoutBackpressureService(processor, GeneratedCheckoutBackpressurePolicy.CreateGeneratedPolicy());

        var fluentResult = await fluent.SubmitAsync(work);
        var generatedResult = await generated.SubmitAsync(work);

        ScenarioExpect.True(fluentResult.Accepted);
        ScenarioExpect.True(generatedResult.Accepted);
        ScenarioExpect.Equal("ORDER-100", generatedResult.OrderId);
    }

    [Scenario("Checkout backpressure is importable through IServiceCollection")]
    [Fact]
    public async Task Checkout_Backpressure_Is_Importable_Through_ServiceCollection()
    {
        using var provider = new ServiceCollection()
            .AddCheckoutBackpressureDemo()
            .BuildServiceProvider();

        var policy = provider.GetRequiredService<BackpressurePolicy<CheckoutAdmission>>();
        var service = provider.GetRequiredService<CheckoutBackpressureService>();
        var result = await service.SubmitAsync(new CheckoutWork("ORDER-200", 99m));

        ScenarioExpect.Equal("checkout-backpressure", policy.Name);
        ScenarioExpect.True(result.Accepted);
        ScenarioExpect.Equal("accepted", result.Reason);
    }

    [Scenario("Checkout backpressure maps dropped and shed work to domain admissions")]
    [Theory]
    [InlineData(BackpressureMode.DropNewest, "dropped")]
    [InlineData(BackpressureMode.Shed, "shed")]
    [InlineData(BackpressureMode.Reject, "rejected")]
    public async Task Checkout_Backpressure_Maps_Saturated_Work_To_Domain_Admissions(BackpressureMode mode, string reason)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processor = new BlockingCheckoutProcessor(entered, release);
        var policy = BackpressurePolicy<CheckoutAdmission>.Create("checkout")
            .WithCapacity(1)
            .WithMode(mode)
            .Build();
        var service = new CheckoutBackpressureService(processor, policy);

        var first = service.SubmitAsync(new CheckoutWork("ORDER-300", 10m));
        await entered.Task;
        var saturated = await service.SubmitAsync(new CheckoutWork("ORDER-301", 10m));
        release.SetResult();
        _ = await first;

        ScenarioExpect.False(saturated.Accepted);
        ScenarioExpect.Equal(reason, saturated.Reason);
    }

    private sealed class BlockingCheckoutProcessor(TaskCompletionSource entered, TaskCompletionSource release) : ICheckoutProcessor
    {
        public async ValueTask<CheckoutAdmission> ProcessAsync(CheckoutWork work, CancellationToken cancellationToken = default)
        {
            entered.SetResult();
            await release.Task.WaitAsync(cancellationToken);
            return new CheckoutAdmission(work.OrderId, true, "accepted");
        }
    }
}
