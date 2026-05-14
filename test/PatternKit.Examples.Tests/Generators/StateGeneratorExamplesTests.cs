using PatternKit.Examples.Generators.State;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.GeneratorTests;

[Feature("State generator examples")]
[Collection(PatternKit.Examples.Tests.ConsoleTestCollection.Name)]
public sealed class StateGeneratorExamplesTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Fact]
    public void OrderFlow_StartsInDraftAndExposesAvailableTriggers()
    {
        var order = new OrderFlow("ORD-TEST", 25m);

        Assert.Equal(OrderState.Draft, order.State);
        Assert.Equal("Order is being prepared", order.GetStateDescription());
        Assert.Equal([OrderTrigger.Submit, OrderTrigger.Cancel], order.GetAvailableTriggers().ToArray());
    }

    [Fact]
    public async Task OrderFlow_CompletesHappyPath()
    {
        var order = new OrderFlow("ORD-TEST", 25m);

        order.Fire(OrderTrigger.Submit);
        await order.FireAsync(OrderTrigger.Pay, CancellationToken.None);
        order.Fire(OrderTrigger.Ship);

        Assert.Equal(OrderState.Shipped, order.State);
        Assert.Equal("Order is on its way to you", order.GetStateDescription());
    }

    [Fact]
    public void OrderFlow_GuardBlocksInvalidPayment()
    {
        var order = new OrderFlow("ORD-BAD", -1m);

        order.Fire(OrderTrigger.Submit);

        Assert.False(order.CanFire(OrderTrigger.Pay));
        Assert.Equal(OrderState.Submitted, order.State);
    }

    [Fact]
    public void OrderFlow_CanCancelBeforePayment()
    {
        var order = new OrderFlow("ORD-CANCEL", 10m);

        order.Fire(OrderTrigger.Submit);
        order.Fire(OrderTrigger.Cancel);

        Assert.Equal(OrderState.Cancelled, order.State);
        Assert.Equal("Order has been cancelled", order.GetStateDescription());
    }

    [Fact]
    public async Task DocumentWorkflow_RequiresReviewCommentBeforeApproval()
    {
        var workflow = new DocumentWorkflow("DOC-1", "Alice");

        workflow.Fire(DocumentAction.SubmitForReview);

        Assert.Equal(DocumentState.PendingReview, workflow.State);
        Assert.False(workflow.CanFire(DocumentAction.Approve));

        workflow.ReviewComments.Add("Looks good.");
        workflow.Fire(DocumentAction.Approve);
        await workflow.FireAsync(DocumentAction.Publish, CancellationToken.None);
        workflow.Fire(DocumentAction.Archive);

        Assert.Equal(DocumentState.Archived, workflow.State);
    }

    [Fact]
    public void DocumentWorkflow_RejectionCanReturnToDraft()
    {
        var workflow = new DocumentWorkflow("DOC-2", "Alice");
        workflow.ReviewComments.Add("Needs changes.");

        workflow.Fire(DocumentAction.SubmitForReview);
        workflow.Fire(DocumentAction.Reject);
        workflow.Fire(DocumentAction.Revise);

        Assert.Equal(DocumentState.Draft, workflow.State);
        Assert.Empty(workflow.ReviewComments);
    }

    [Scenario("Order flow demo suite covers happy path, cancellation, guard failure, and state-based logic")]
    [Fact]
    public async Task OrderFlowDemo_RunAllDemosAsync_CompletesPublicWorkflowSuite()
    {
        await Given("a redirected console", CaptureConsole)
            .When("running every order flow demo", async Task<string> (capture) =>
            {
                try
                {
                    await OrderFlowDemo.RunAllDemosAsync();
                    return capture.Output();
                }
                finally
                {
                    capture.Dispose();
                }
            })
            .Then("the shipped happy path completed", output => output.Contains("Order processing complete", StringComparison.Ordinal))
            .And("the cancellation scenario completed", output => output.Contains("Order was cancelled", StringComparison.Ordinal))
            .And("the guard failure scenario blocked payment", output => output.Contains("Payment blocked by guard", StringComparison.Ordinal))
            .And("state-based trigger discovery completed", output => output.Contains("State-based logic complete", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Order flow rejects invalid triggers and honors async cancellation")]
    [Fact]
    public async Task OrderFlow_RejectsInvalidTriggersAndAsyncCancellation()
    {
        await Given("a submitted order", () =>
            {
                var order = new OrderFlow("ORD-BDD", 250m);
                order.Fire(OrderTrigger.Submit);
                return order;
            })
            .When("shipping before payment and cancelling payment",
                async Task<(OrderFlow order, InvalidOperationException? invalidShip, OperationCanceledException? cancelledPay)> (order) =>
            {
                InvalidOperationException? invalidShip = null;
                OperationCanceledException? cancelledPay = null;

                try
                {
                    order.Fire(OrderTrigger.Ship);
                }
                catch (InvalidOperationException ex)
                {
                    invalidShip = ex;
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));
                try
                {
                    await order.FireAsync(OrderTrigger.Pay, cts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    cancelledPay = ex;
                }

                return (order, invalidShip, cancelledPay);
            })
            .Then("shipping before payment is rejected", result => result.invalidShip is not null)
            .And("payment observes cancellation", result => result.cancelledPay is not null)
            .And("the order remains submitted after failed operations", result => result.order.State == OrderState.Submitted)
            .AssertPassed();
    }

    [Scenario("Document workflow covers approval, rejection, publish cancellation, and invalid actions")]
    [Fact]
    public async Task DocumentWorkflow_CoversHappyAndSadPaths()
    {
        await Given("independent document workflows", () =>
            (
                Approval: new DocumentWorkflow("DOC-APPROVE", "Alice"),
                Rejection: new DocumentWorkflow("DOC-REJECT", "Bob"),
                Cancellation: new DocumentWorkflow("DOC-CANCEL", "Chris")))
            .When("driving approval, rejection, and cancelled publish paths",
                async Task<((DocumentWorkflow Approval, DocumentWorkflow Rejection, DocumentWorkflow Cancellation) workflows, bool canApproveWithoutComment, OperationCanceledException? cancelled)> (workflows) =>
            {
                workflows.Approval.Fire(DocumentAction.SubmitForReview);
                var canApproveWithoutComment = workflows.Approval.CanFire(DocumentAction.Approve);
                workflows.Approval.ReviewComments.Add("Approved.");
                workflows.Approval.Fire(DocumentAction.Approve);
                await workflows.Approval.FireAsync(DocumentAction.Publish, CancellationToken.None);

                workflows.Rejection.Fire(DocumentAction.SubmitForReview);
                workflows.Rejection.Fire(DocumentAction.Reject);
                workflows.Rejection.ReviewComments.Add("Revise.");
                workflows.Rejection.Fire(DocumentAction.Revise);

                workflows.Cancellation.Fire(DocumentAction.SubmitForReview);
                workflows.Cancellation.ReviewComments.Add("Looks good.");
                workflows.Cancellation.Fire(DocumentAction.Approve);
                OperationCanceledException? cancelled = null;
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));
                try
                {
                    await workflows.Cancellation.FireAsync(DocumentAction.Publish, cts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    cancelled = ex;
                }

                return (workflows, canApproveWithoutComment, cancelled);
            })
            .Then("approval requires a review comment", result => !result.canApproveWithoutComment)
            .And("approved documents can be published", result => result.workflows.Approval.State == DocumentState.Published)
            .And("rejected documents can return to draft and clear comments", result =>
                result.workflows.Rejection.State == DocumentState.Draft
                && result.workflows.Rejection.ReviewComments.Count == 0)
            .And("publish observes cancellation", result =>
                result.cancelled is not null
                && result.workflows.Cancellation.State == DocumentState.Approved)
            .AssertPassed();
    }

    private static ConsoleCapture CaptureConsole() => new();

    private sealed class ConsoleCapture : IDisposable
    {
        private readonly TextWriter _original = Console.Out;
        private readonly StringWriter _writer = new();

        public ConsoleCapture()
        {
            Console.SetOut(_writer);
        }

        public string Output() => _writer.ToString();

        public void Dispose()
        {
            Console.SetOut(_original);
            _writer.Dispose();
        }
    }
}
