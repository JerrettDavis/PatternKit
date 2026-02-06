using PatternKit.Examples.Generators.Iterator;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Generators.Iterator;

[Feature("Iterator Generator Example")]
public sealed class IteratorGeneratorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Demo runs without errors")]
    [Fact]
    public async Task Demo_Runs_Successfully()
    {
        await Given("the iterator generator demo", () => IteratorGeneratorDemo.Run())
            .Then("it should return output lines", lines => lines.Count > 0)
            .AssertPassed();
    }

    [Scenario("Fibonacci iterator yields correct first 10 values")]
    [Fact]
    public async Task Fibonacci_Yields_Correct_Values()
    {
        await Given("a Fibonacci iterator for 10 values", () => new FibonacciIterator(10))
            .When("iterating with foreach", fib =>
            {
                var values = new List<long>();
                foreach (var v in fib) values.Add(v);
                return values;
            })
            .Then("should yield 0, 1, 1, 2, 3, 5, 8, 13, 21, 34", values =>
                values.Count == 10 &&
                values[0] == 0 && values[1] == 1 && values[2] == 1 &&
                values[3] == 2 && values[4] == 3 && values[5] == 5 &&
                values[6] == 8 && values[7] == 13 && values[8] == 21 &&
                values[9] == 34)
            .AssertPassed();
    }

    [Scenario("Fibonacci TryMoveNext works with explicit state")]
    [Fact]
    public async Task Fibonacci_TryMoveNext_Works()
    {
        await Given("a Fibonacci iterator for 5 values", () => new FibonacciIterator(5))
            .When("calling TryMoveNext manually", fib =>
            {
                var state = (a: 0L, b: 0L, index: 0);
                var values = new List<long>();
                while (fib.TryMoveNext(ref state, out var item))
                    values.Add(item);
                return values;
            })
            .Then("should yield 0, 1, 1, 2, 3", values =>
                values.Count == 5 &&
                values[0] == 0 && values[1] == 1 && values[2] == 1 &&
                values[3] == 2 && values[4] == 3)
            .AssertPassed();
    }

    [Scenario("DFS traversal visits nodes in depth-first order")]
    [Fact]
    public async Task DFS_Traversal_Correct_Order()
    {
        await Given("a file system tree", BuildSampleTree)
            .When("performing DFS traversal", root =>
            {
                var result = FileSystemTraversal.DepthFirst(root);
                return result.Select(n => n.Name).ToList();
            })
            .Then("should visit root, src, main.cs, utils.cs, docs, readme.md, build.sh", names =>
                names.Count == 7 &&
                names[0] == "root" &&
                names[1] == "src" &&
                names[2] == "main.cs" &&
                names[3] == "utils.cs" &&
                names[4] == "docs" &&
                names[5] == "readme.md" &&
                names[6] == "build.sh")
            .AssertPassed();
    }

    [Scenario("BFS traversal visits nodes in breadth-first order")]
    [Fact]
    public async Task BFS_Traversal_Correct_Order()
    {
        await Given("a file system tree", BuildSampleTree)
            .When("performing BFS traversal", root =>
            {
                var result = FileSystemTraversal.BreadthFirst(root);
                return result.Select(n => n.Name).ToList();
            })
            .Then("should visit root, src, docs, build.sh, main.cs, utils.cs, readme.md", names =>
                names.Count == 7 &&
                names[0] == "root" &&
                names[1] == "src" &&
                names[2] == "docs" &&
                names[3] == "build.sh" &&
                names[4] == "main.cs" &&
                names[5] == "utils.cs" &&
                names[6] == "readme.md")
            .AssertPassed();
    }

    private static FileSystemNode BuildSampleTree() => new()
    {
        Name = "root", IsDirectory = true,
        Children =
        {
            new FileSystemNode
            {
                Name = "src", IsDirectory = true,
                Children =
                {
                    new FileSystemNode { Name = "main.cs" },
                    new FileSystemNode { Name = "utils.cs" }
                }
            },
            new FileSystemNode
            {
                Name = "docs", IsDirectory = true,
                Children =
                {
                    new FileSystemNode { Name = "readme.md" }
                }
            },
            new FileSystemNode { Name = "build.sh" }
        }
    };
}
