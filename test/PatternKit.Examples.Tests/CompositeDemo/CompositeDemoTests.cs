using PatternKit.Examples.CompositeDemo;
using static PatternKit.Examples.CompositeDemo.CompositeDemo;
using FileInfo = PatternKit.Examples.CompositeDemo.CompositeDemo.FileInfo;

namespace PatternKit.Examples.Tests.CompositeDemoTests;

public sealed class CompositeDemoTests
{
    [Fact]
    public void FileInfo_Record_Works()
    {
        var file = new FileInfo("test.txt", 1024, DateTime.Now);

        Assert.Equal("test.txt", file.Name);
        Assert.Equal(1024, file.Size);
    }

    [Fact]
    public void SearchResult_Record_Works()
    {
        var result = new SearchResult("/path", "file.ts", 500);

        Assert.Equal("/path", result.Path);
        Assert.Equal("file.ts", result.Name);
        Assert.Equal(500, result.Size);
    }

    [Fact]
    public void CreateFileSize_Returns_Correct_Size()
    {
        var fileNode = CreateFileSize(1024).Build();

        var size = fileNode.Execute("ignored");

        Assert.Equal(1024, size);
    }

    [Fact]
    public void CreateDirectorySizeBuilder_Sums_Children()
    {
        var directory = CreateDirectorySizeBuilder()
            .AddChild(CreateFileSize(100))
            .AddChild(CreateFileSize(200))
            .AddChild(CreateFileSize(300))
            .Build();

        var size = directory.Execute("ignored");

        Assert.Equal(600, size);
    }

    [Fact]
    public void CreateDirectorySizeBuilder_Nested_Directories()
    {
        var root = CreateDirectorySizeBuilder()
            .AddChild(CreateFileSize(100))
            .AddChild(CreateDirectorySizeBuilder()
                .AddChild(CreateFileSize(200))
                .AddChild(CreateFileSize(300)))
            .Build();

        var size = root.Execute("ignored");

        Assert.Equal(600, size);
    }

    [Fact]
    public void CreateFileSearcher_Matches_Pattern()
    {
        var file = new FileInfo("app.ts", 1000, DateTime.Now);
        var searcher = CreateFileSearcher(file, "/src").Build();

        var results = searcher.Execute(".ts");

        Assert.Single(results);
        Assert.Equal("app.ts", results[0].Name);
        Assert.Equal("/src", results[0].Path);
    }

    [Fact]
    public void CreateFileSearcher_No_Match()
    {
        var file = new FileInfo("readme.md", 500, DateTime.Now);
        var searcher = CreateFileSearcher(file, "/").Build();

        var results = searcher.Execute(".ts");

        Assert.Empty(results);
    }

    [Fact]
    public void CreateDirectorySearchBuilder_Aggregates_Results()
    {
        var file1 = new FileInfo("index.ts", 1000, DateTime.Now);
        var file2 = new FileInfo("app.ts", 2000, DateTime.Now);
        var file3 = new FileInfo("readme.md", 500, DateTime.Now);

        var directory = CreateDirectorySearchBuilder()
            .AddChild(CreateFileSearcher(file1, "/src"))
            .AddChild(CreateFileSearcher(file2, "/src"))
            .AddChild(CreateFileSearcher(file3, "/"))
            .Build();

        var results = directory.Execute(".ts");

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Name == "index.ts");
        Assert.Contains(results, r => r.Name == "app.ts");
    }

    [Fact]
    public void BuildProjectStructure_Creates_SizeCalculator_And_Searcher()
    {
        var (sizeCalc, searcher) = BuildProjectStructure();

        Assert.NotNull(sizeCalc);
        Assert.NotNull(searcher);
    }

    [Fact]
    public void BuildProjectStructure_SizeCalc_Calculates_Total()
    {
        var (sizeCalc, _) = BuildProjectStructure();

        var totalSize = sizeCalc.Execute("");

        // Expected: 2048 + 1100 + 256 + 1500 + 5000 + 12000 + 3500 + 8000 + 45000 = 78404
        Assert.Equal(78404, totalSize);
    }

    [Fact]
    public void BuildProjectStructure_Searcher_Finds_TypeScript_Files()
    {
        var (_, searcher) = BuildProjectStructure();

        var results = searcher.Execute(".ts");

        Assert.Equal(4, results.Count);
        Assert.Contains(results, r => r.Name == "index.ts");
        Assert.Contains(results, r => r.Name == "app.ts");
        Assert.Contains(results, r => r.Name == "utils.ts");
        Assert.Contains(results, r => r.Name == "app.spec.ts");
    }

    [Fact]
    public void BuildProjectStructure_Searcher_Finds_Json_Files()
    {
        var (_, searcher) = BuildProjectStructure();

        var results = searcher.Execute(".json");

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Name == "package.json");
        Assert.Contains(results, r => r.Name == "coverage.json");
    }

    [Fact]
    public void BuildProjectStructure_Searcher_CaseInsensitive()
    {
        var (_, searcher) = BuildProjectStructure();

        var lowerResults = searcher.Execute("readme");
        var upperResults = searcher.Execute("README");

        Assert.Single(lowerResults);
        Assert.Single(upperResults);
    }

    [Fact]
    public void Run_Executes_Without_Errors()
    {
        PatternKit.Examples.CompositeDemo.CompositeDemo.Run();
    }
}
