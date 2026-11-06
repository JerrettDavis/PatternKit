using PatternKit.Application.CompensatingTransactions;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Application.CompensatingTransactions;

[Feature("Compensating transaction")]
public sealed class CompensatingTransactionTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Transaction completes ordered conditional steps")]
    [Fact]
    public Task Transaction_Completes_Ordered_Conditional_Steps()
        => Given("a transaction with ordered and conditional steps", () =>
            {
                var context = new TransactionContext(runOptional: false);
                var transaction = CompensatingTransaction<TransactionContext>
                    .Create("checkout")
                    .AddStep("capture", static (ctx, _) => { ctx.Events.Add("capture"); return default; }, static (ctx, _) => { ctx.Events.Add("refund"); return default; }, static step => step.At(20))
                    .AddStep("optional", static (ctx, _) => { ctx.Events.Add("optional"); return default; }, static (ctx, _) => { ctx.Events.Add("undo-optional"); return default; }, static step => step.At(10).When(static ctx => ctx.RunOptional))
                    .Build();
                return (context, transaction);
            })
            .When("executing the transaction", (Func<(TransactionContext context, CompensatingTransaction<TransactionContext> transaction), ValueTask<CompensatingTransactionExecution<TransactionContext>>>)(async ctx => await ctx.transaction.ExecuteAsync(ctx.context)))
            .Then("only eligible steps run in order", execution =>
            {
                ScenarioExpect.Equal(CompensatingTransactionStatus.Completed, execution.Status);
                ScenarioExpect.Equal("checkout", execution.TransactionName);
                ScenarioExpect.Equal(["optional", "capture"], execution.History.Select(static item => item.StepName).ToArray());
                ScenarioExpect.Equal(CompensatingTransactionRecordKind.Skipped, execution.History[0].Kind);
                ScenarioExpect.Equal(CompensatingTransactionRecordKind.Completed, execution.History[1].Kind);
                ScenarioExpect.Equal(["capture"], execution.Context.Events);
            })
            .AssertPassed();

    [Scenario("Transaction compensates completed work when a later step fails")]
    [Fact]
    public Task Transaction_Compensates_Completed_Work_When_A_Later_Step_Fails()
        => Given("a checkout transaction with a failing shipment step", () =>
            {
                var context = new TransactionContext(runOptional: true);
                return CompensatingTransaction<TransactionContext>
                    .Create()
                    .AddStep("reserve", static (ctx, _) => { ctx.Events.Add("reserve"); return default; }, static (ctx, _) => { ctx.Events.Add("release"); return default; }, static step => step.At(10))
                    .AddStep("ship", static (_, _) => throw new InvalidOperationException("carrier unavailable"), static (ctx, _) => { ctx.Events.Add("cancel-ship"); return default; }, static step => step.At(20));
            })
            .When("executing the transaction", (Func<CompensatingTransaction<TransactionContext>.Builder, ValueTask<CompensatingTransactionExecution<TransactionContext>>>)(async builder => await builder.Build().ExecuteAsync(new TransactionContext(runOptional: true))))
            .Then("completed steps are compensated in reverse order", execution =>
            {
                ScenarioExpect.Equal(CompensatingTransactionStatus.Compensated, execution.Status);
                ScenarioExpect.Equal(["reserve", "release"], execution.Context.Events);
                ScenarioExpect.Equal(CompensatingTransactionRecordKind.Failed, execution.History[1].Kind);
                ScenarioExpect.Equal(CompensatingTransactionRecordKind.Compensated, execution.History[2].Kind);
            })
            .AssertPassed();

    [Scenario("Transaction records compensation failures")]
    [Fact]
    public Task Transaction_Records_Compensation_Failures()
        => Given("a transaction whose compensation throws", () => CompensatingTransaction<TransactionContext>
            .Create()
            .AddStep("reserve", static (ctx, _) => { ctx.Events.Add("reserve"); return default; }, static (_, _) => throw new InvalidOperationException("release failed"), static step => step.At(10))
            .AddStep("capture", static (_, _) => throw new InvalidOperationException("capture failed"), static (_, _) => default, static step => step.At(20))
            .Build())
        .When("executing the transaction", transaction => transaction.Execute(new TransactionContext(runOptional: true)))
        .Then("the failed compensation is observable", execution =>
        {
            ScenarioExpect.Equal(CompensatingTransactionStatus.CompensationFailed, execution.Status);
            ScenarioExpect.Contains(execution.History, static record => record.Kind == CompensatingTransactionRecordKind.CompensationFailed);
            ScenarioExpect.Equal("release failed", execution.History.Last().ErrorMessage);
        })
        .AssertPassed();

    [Scenario("Transaction rejects invalid configuration")]
    [Fact]
    public Task Transaction_Rejects_Invalid_Configuration()
        => Given("a compensating transaction builder", () => CompensatingTransaction<TransactionContext>.Create("checkout"))
            .Then("invalid setup throws clear exceptions", builder =>
            {
                ScenarioExpect.Throws<ArgumentException>(() => CompensatingTransaction<TransactionContext>.Create("").AddStep("x", static (_, _) => default, static (_, _) => default).Build());
                ScenarioExpect.Throws<ArgumentException>(() => CompensatingTransaction<TransactionContext>.Create().Build());
                ScenarioExpect.Throws<ArgumentException>(() => builder.AddStep("", static (_, _) => default, static (_, _) => default));
                ScenarioExpect.Throws<ArgumentNullException>(() => builder.AddStep("execute", null!, static (_, _) => default));
                ScenarioExpect.Throws<ArgumentNullException>(() => builder.AddStep("compensate", static (_, _) => default, null!));
                ScenarioExpect.Throws<ArgumentNullException>(() => builder.AddStep("condition", static (_, _) => default, static (_, _) => default, static step => step.When(null!)));
                ScenarioExpect.Throws<InvalidOperationException>(() => CompensatingTransaction<TransactionContext>.Create().AddStep("same", static (_, _) => default, static (_, _) => default).AddStep("same", static (_, _) => default, static (_, _) => default).Build());
                ScenarioExpect.Throws<ArgumentNullException>(() => new CompensatingTransactionExecution<TransactionContext>("bad", new TransactionContext(false), CompensatingTransactionStatus.Completed, null!));
            })
            .AssertPassed();

    [Scenario("Transaction honors cancellation before work starts")]
    [Fact]
    public Task Transaction_Honors_Cancellation_Before_Work_Starts()
        => Given("a canceled token and a transaction", () =>
            {
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                var transaction = CompensatingTransaction<TransactionContext>
                    .Create()
                    .AddStep("never", static (_, _) => throw new InvalidOperationException("should not run"), static (_, _) => default)
                    .Build();
                return (transaction, Token: cancellation.Token);
            })
            .Then("execution is canceled before invoking any step", async ctx =>
                await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() => ctx.transaction.ExecuteAsync(new TransactionContext(false), ctx.Token).AsTask()))
            .AssertPassed();

    private sealed class TransactionContext(bool runOptional)
    {
        public bool RunOptional { get; } = runOptional;

        public List<string> Events { get; } = [];
    }
}
