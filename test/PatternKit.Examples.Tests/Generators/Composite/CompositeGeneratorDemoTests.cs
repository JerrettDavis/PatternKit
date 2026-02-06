using PatternKit.Examples.Generators.Composite;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;
using Directory = PatternKit.Examples.Generators.Composite.Directory;
using File = PatternKit.Examples.Generators.Composite.File;

namespace PatternKit.Examples.Tests.Generators.Composite;

[Feature("Composite Generator Example")]
public sealed class CompositeGeneratorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Demo runs and produces expected output")]
    [Fact]
    public Task Demo_Runs_Successfully()
        => Given("the composite demo", () => new { })
            .When("Run() is called", _ => CompositeGeneratorDemo.Run())
            .Then("output contains tree display and size", result =>
            {
                Assert.True(result.Count >= 3);
                Assert.Contains("root/", result[0]);
                Assert.Contains("Total size: 1100 bytes", result[1]);
            })
            .AssertPassed();

    [Scenario("Directory aggregates child sizes")]
    [Fact]
    public Task Directory_GetSize_Sums_Children()
        => Given("a directory with files", () =>
        {
            var dir = new Directory("test");
            dir.Add(new File("a.txt", 100));
            dir.Add(new File("b.txt", 200));
            return dir;
        })
            .When("GetSize is called", dir => dir.GetSize())
            .Then("the total equals the sum of file sizes", size =>
            {
                Assert.Equal(300, size);
            })
            .AssertPassed();

    [Scenario("File displays with correct format")]
    [Fact]
    public Task File_Displays_Name_And_Size()
        => Given("a file", () => new File("readme.md", 42))
            .When("Display is called", file => file.Display(0))
            .Then("output shows name and size", output =>
            {
                Assert.Contains("readme.md", output);
                Assert.Contains("42 bytes", output);
            })
            .AssertPassed();

    [Scenario("Nested directories display correct hierarchy")]
    [Fact]
    public Task Nested_Directory_Shows_Indentation()
        => Given("a nested directory structure", () =>
        {
            var root = new Directory("root");
            var sub = new Directory("sub");
            sub.Add(new File("file.txt", 10));
            root.Add(sub);
            return root;
        })
            .When("Display is called", root => root.Display(0))
            .Then("nested items are indented", output =>
            {
                Assert.Contains("root/", output);
                Assert.Contains("  sub/", output);
                Assert.Contains("    file.txt", output);
            })
            .AssertPassed();

    [Scenario("Add and Remove manage children correctly")]
    [Fact]
    public Task Add_Remove_Manages_Children()
        => Given("a directory", () => new Directory("test"))
            .When("files are added and removed", dir =>
            {
                var file1 = new File("a.txt", 10);
                var file2 = new File("b.txt", 20);
                dir.Add(file1);
                dir.Add(file2);
                Assert.Equal(2, dir.Children.Count);
                dir.Remove(file1);
                return dir;
            })
            .Then("only one child remains", dir =>
            {
                Assert.Equal(1, dir.Children.Count);
                Assert.Equal(20, dir.GetSize());
            })
            .AssertPassed();

    [Scenario("Traversal helpers enumerate all nodes")]
    [Fact]
    public Task Traversal_Enumerates_All_Nodes()
        => Given("a tree with 4 nodes", () =>
        {
            var root = new Directory("root");
            root.Add(new File("a.txt", 10));
            var sub = new Directory("sub");
            sub.Add(new File("b.txt", 20));
            root.Add(sub);
            return root;
        })
            .When("DepthFirst is enumerated", root =>
            {
                var count = 0;
                foreach (var _ in root.DepthFirst())
                    count++;
                return count;
            })
            .Then("all 4 nodes are visited", count =>
            {
                Assert.Equal(4, count);
            })
            .AssertPassed();
}
