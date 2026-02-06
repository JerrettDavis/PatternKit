using PatternKit.Behavioral.Visitor;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - Visitor Concurrency (Async)")]
public sealed class AsyncVisitorConcurrencyTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record State(AsyncActionVisitor<Node> v, int[] counters, Node[] nodes, int perType);

    private abstract record Node;
    private sealed record Number(int Value) : Node;
    private sealed record Add(Node Left, Node Right) : Node;
    private sealed record Neg(Node Inner) : Node;

    [Scenario("AsyncActionVisitor executes correctly under concurrent calls across tasks")]
    [Fact(Timeout = 30_000)]
    public Task AsyncActionVisitor_Concurrent_Dispatch()
        => Given("an async action visitor and a mixed dataset", () =>
            {
                var counters = new int[3]; // [0]=Add, [1]=Number, [2]=Default
                var v = AsyncActionVisitor<Node>
                    .Create()
                    .On<Add>((_, _) => { Interlocked.Increment(ref counters[0]); return default; })
                    .On<Number>((_, _) => { Interlocked.Increment(ref counters[1]); return default; })
                    .Default((_, _) => { Interlocked.Increment(ref counters[2]); return default; })
                    .Build();

                const int perType = 1500;
                var baseNodes = new Node[] { new Add(new Number(1), new Number(2)), new Number(7), new Neg(new Number(0)) };
                var nodes = new Node[perType * baseNodes.Length];
                for (var i = 0; i < perType; i++)
                {
                    nodes[3 * i + 0] = baseNodes[0];
                    nodes[3 * i + 1] = baseNodes[1];
                    nodes[3 * i + 2] = baseNodes[2];
                }

                return new State(v, counters, nodes, perType);
            })
           .When("visiting concurrently with tasks", VisitConcurrently)
           .Then("each action ran expected times", x => x.counters[0] == x.perType && x.counters[1] == x.perType && x.counters[2] == x.perType)
           .AssertPassed();

    private static async Task<State> VisitConcurrently(State x)
    {
        var degree = Math.Min(Environment.ProcessorCount, 8);
        var total = x.nodes.Length;
        var slice = total / degree;
        var tasks = new List<Task>(degree);
        for (var i = 0; i < degree; i++)
        {
            var start = i * slice;
            var end = (i == degree - 1) ? total : start + slice;
            tasks.Add(Task.Run(async () =>
            {
                for (var j = start; j < end; j++)
                    await x.v.VisitAsync(x.nodes[j]);
            }));
        }
        await Task.WhenAll(tasks);
        return x;
    }
}
