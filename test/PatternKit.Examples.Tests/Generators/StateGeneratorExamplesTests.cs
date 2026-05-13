using PatternKit.Examples.Generators.State;

namespace PatternKit.Examples.Tests.GeneratorTests;

public sealed class StateGeneratorExamplesTests
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
}
