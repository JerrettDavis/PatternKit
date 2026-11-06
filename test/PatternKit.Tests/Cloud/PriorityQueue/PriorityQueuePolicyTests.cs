using PatternKit.Cloud.PriorityQueue;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Cloud.PriorityQueue;

[Feature("Priority Queue")]
public sealed class PriorityQueuePolicyTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Priority queue dequeues highest priority first")]
    [Fact]
    public Task Priority_Queue_Dequeues_Highest_Priority_First()
        => Given("a priority queue", () => PriorityQueuePolicy<WorkItem, int>.Create("fulfillment-priority")
            .WithPrioritySelector(static item => item.Priority)
            .Build())
        .When("mixed priority work is enqueued", queue =>
        {
            queue.Enqueue(new("standard", 1));
            queue.Enqueue(new("expedited", 5));
            return queue.Dequeue();
        })
        .Then("the highest priority item is returned first", result =>
        {
            ScenarioExpect.True(result.HasItem);
            ScenarioExpect.Equal("expedited", result.Item!.Name);
            ScenarioExpect.Equal(5, result.Priority);
            ScenarioExpect.Equal(1, result.RemainingCount);
        })
        .AssertPassed();

    [Scenario("Priority queue preserves FIFO order for matching priorities")]
    [Fact]
    public Task Priority_Queue_Preserves_Fifo_Order_For_Matching_Priorities()
        => Given("a priority queue", () => PriorityQueuePolicy<WorkItem, int>.Create()
            .WithPrioritySelector(static item => item.Priority)
            .Build())
        .When("same-priority work is enqueued", queue =>
        {
            queue.Enqueue(new("first", 3));
            queue.Enqueue(new("second", 3));
            return new[] { queue.Dequeue(), queue.Dequeue() };
        })
        .Then("items with matching priority keep arrival order", results =>
        {
            ScenarioExpect.Equal("first", results[0].Item!.Name);
            ScenarioExpect.Equal("second", results[1].Item!.Name);
        })
        .AssertPassed();

    [Scenario("Priority queue can dequeue lowest priority first")]
    [Fact]
    public Task Priority_Queue_Can_Dequeue_Lowest_Priority_First()
        => Given("a lowest-first priority queue", () => PriorityQueuePolicy<WorkItem, int>.Create()
            .WithPrioritySelector(static item => item.Priority)
            .DequeueLowestPriorityFirst()
            .Build())
        .When("mixed priority work is enqueued", queue =>
        {
            queue.Enqueue(new("low", 1));
            queue.Enqueue(new("high", 9));
            return queue.Peek();
        })
        .Then("the lowest priority item is visible first", result =>
            ScenarioExpect.Equal("low", result.Item!.Name))
        .AssertPassed();

    [Scenario("Priority queue reports enqueue metadata")]
    [Fact]
    public Task Priority_Queue_Reports_Enqueue_Metadata()
        => Given("a priority queue", () => PriorityQueuePolicy<WorkItem, int>.Create("support-priority")
            .WithPrioritySelector(static item => item.Priority)
            .Build())
        .When("work is enqueued", queue => queue.Enqueue(new("ticket", 7)))
        .Then("the enqueue result reports queue priority item and count", result =>
        {
            ScenarioExpect.Equal("support-priority", result.QueueName);
            ScenarioExpect.Equal("ticket", result.Item.Name);
            ScenarioExpect.Equal(7, result.Priority);
            ScenarioExpect.Equal(1, result.Count);
        })
        .AssertPassed();

    [Scenario("Priority queue reports empty dequeue and peek")]
    [Fact]
    public Task Priority_Queue_Reports_Empty_Dequeue_And_Peek()
        => Given("an empty priority queue", () => PriorityQueuePolicy<WorkItem, int>.Create("empty-priority")
            .WithPrioritySelector(static item => item.Priority)
            .Build())
        .When("dequeueing and peeking", queue => new[] { queue.Dequeue(), queue.Peek() })
        .Then("both results are empty", results =>
        {
            ScenarioExpect.False(results[0].HasItem);
            ScenarioExpect.False(results[1].HasItem);
            ScenarioExpect.Equal("empty-priority", results[0].QueueName);
            ScenarioExpect.Equal(0, results[0].RemainingCount);
            ScenarioExpect.Null(results[0].Item);
        })
        .AssertPassed();

    [Scenario("Priority queue supports custom priority comparers")]
    [Fact]
    public Task Priority_Queue_Supports_Custom_Priority_Comparers()
        => Given("a priority queue with a custom comparer", () => PriorityQueuePolicy<WorkItem, string>.Create()
            .WithPrioritySelector(static item => item.Name)
            .WithComparer(StringComparer.OrdinalIgnoreCase)
            .Build())
        .When("case-varied priorities are enqueued", queue =>
        {
            queue.Enqueue(new("alpha", 0));
            queue.Enqueue(new("Bravo", 0));
            return queue.Dequeue();
        })
        .Then("the comparer controls priority ordering", result =>
            ScenarioExpect.Equal("Bravo", result.Item!.Name))
        .AssertPassed();

    [Scenario("Priority queue validates configuration")]
    [Fact]
    public Task Priority_Queue_Validates_Configuration()
        => Given("invalid priority queue inputs", () => true)
        .Then("invalid names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => PriorityQueuePolicy<WorkItem, int>.Create("").WithPrioritySelector(static item => item.Priority).Build()))
        .And("missing selectors are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => PriorityQueuePolicy<WorkItem, int>.Create().Build()))
        .And("null selectors are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => PriorityQueuePolicy<WorkItem, int>.Create().WithPrioritySelector(null!)))
        .And("null comparers are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => PriorityQueuePolicy<WorkItem, int>.Create().WithComparer(null!)))
        .And("null items are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => PriorityQueuePolicy<string, int>.Create().WithPrioritySelector(static item => item.Length).Build().Enqueue(null!)))
        .AssertPassed();

    private sealed record WorkItem(string Name, int Priority);
}
