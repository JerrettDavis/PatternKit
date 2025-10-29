using PatternKit.Behavioral.Visitor;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - Visitor Concurrency (Sync)")]
public sealed class VisitorConcurrencyTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record State(ActionVisitor<Node> v, int[] counters, Node[] nodes, int perType);

    private abstract record Node;
    private sealed record Number(int Value) : Node;
    private sealed record Add(Node Left, Node Right) : Node;
    private sealed record Neg(Node Inner) : Node;

    [Scenario("ActionVisitor executes correctly under concurrent calls across threads")]
    [Fact]
    public Task ActionVisitor_Concurrent_Dispatch()
        => Given("an action visitor and a mixed dataset", () =>
            {
                var counters = new int[3]; // [0]=Add, [1]=Number, [2]=Default
                var v = ActionVisitor<Node>
                    .Create()
                    .On<Add>(_ => Interlocked.Increment(ref counters[0]))
                    .On<Number>(_ => Interlocked.Increment(ref counters[1]))
                    .Default(_ => Interlocked.Increment(ref counters[2]))
                    .Build();

                const int perType = 2000;
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
           .When("visiting in parallel", VisitInParallel)
           .Then("each action ran expected times", x => x.counters[0] == x.perType && x.counters[1] == x.perType && x.counters[2] == x.perType)
           .AssertPassed();

    private static async Task<State> VisitInParallel(State x)
    {
        var degree = Math.Min(Environment.ProcessorCount, 8);
        var total = x.nodes.Length;
        var slice = total / degree;
        var tasks = new List<Task>(degree);
        for (var i = 0; i < degree; i++)
        {
            var start = i * slice;
            var end = (i == degree - 1) ? total : start + slice;
            tasks.Add(Task.Run(() =>
            {
                for (var j = start; j < end; j++)
                    x.v.Visit(x.nodes[j]);
            }));
        }
        await Task.WhenAll(tasks);
        return x;
    }
}
