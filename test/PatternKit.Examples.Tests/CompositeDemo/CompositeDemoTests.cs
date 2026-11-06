using PatternKit.Examples.CompositeDemo;
using TinyBDD;
using static PatternKit.Examples.CompositeDemo.CompositeDemo;
using FileInfo = PatternKit.Examples.CompositeDemo.CompositeDemo.FileInfo;

namespace PatternKit.Examples.Tests.CompositeDemoTests;

public sealed class CompositeDemoTests
{
    [Scenario("FileInfo Record Works")]
    [Fact]
    public void FileInfo_Record_Works()
    {
        var file = new FileInfo("test.txt", 1024, DateTime.Now);

        ScenarioExpect.Equal("test.txt", file.Name);
        ScenarioExpect.Equal(1024, file.Size);
    }

    [Scenario("SearchResult Record Works")]
    [Fact]
    public void SearchResult_Record_Works()
    {
        var result = new SearchResult("/path", "file.ts", 500);

        ScenarioExpect.Equal("/path", result.Path);
        ScenarioExpect.Equal("file.ts", result.Name);
        ScenarioExpect.Equal(500, result.Size);
    }

    [Scenario("CreateFileSize Returns Correct Size")]
    [Fact]
    public void CreateFileSize_Returns_Correct_Size()
    {
        var fileNode = CreateFileSize(1024).Build();

        var size = fileNode.Execute("ignored");

        ScenarioExpect.Equal(1024, size);
    }

    [Scenario("CreateDirectorySizeBuilder Sums Children")]
    [Fact]
    public void CreateDirectorySizeBuilder_Sums_Children()
    {
        var directory = CreateDirectorySizeBuilder()
            .AddChild(CreateFileSize(100))
            .AddChild(CreateFileSize(200))
            .AddChild(CreateFileSize(300))
            .Build();

        var size = directory.Execute("ignored");

        ScenarioExpect.Equal(600, size);
    }

    [Scenario("CreateDirectorySizeBuilder Nested Directories")]
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

        ScenarioExpect.Equal(600, size);
    }

    [Scenario("CreateFileSearcher Matches Pattern")]
    [Fact]
    public void CreateFileSearcher_Matches_Pattern()
    {
        var file = new FileInfo("app.ts", 1000, DateTime.Now);
        var searcher = CreateFileSearcher(file, "/src").Build();

        var results = searcher.Execute(".ts");

        ScenarioExpect.Single(results);
        ScenarioExpect.Equal("app.ts", results[0].Name);
        ScenarioExpect.Equal("/src", results[0].Path);
    }

    [Scenario("CreateFileSearcher No Match")]
    [Fact]
    public void CreateFileSearcher_No_Match()
    {
        var file = new FileInfo("readme.md", 500, DateTime.Now);
        var searcher = CreateFileSearcher(file, "/").Build();

        var results = searcher.Execute(".ts");

        ScenarioExpect.Empty(results);
    }

    [Scenario("CreateDirectorySearchBuilder Aggregates Results")]
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

        ScenarioExpect.Equal(2, results.Count);
        ScenarioExpect.Contains(results, r => r.Name == "index.ts");
        ScenarioExpect.Contains(results, r => r.Name == "app.ts");
    }

    [Scenario("BuildProjectStructure Creates SizeCalculator And Searcher")]
    [Fact]
    public void BuildProjectStructure_Creates_SizeCalculator_And_Searcher()
    {
        var (sizeCalc, searcher) = BuildProjectStructure();

        ScenarioExpect.NotNull(sizeCalc);
        ScenarioExpect.NotNull(searcher);
    }

    [Scenario("BuildProjectStructure SizeCalc Calculates Total")]
    [Fact]
    public void BuildProjectStructure_SizeCalc_Calculates_Total()
    {
        var (sizeCalc, _) = BuildProjectStructure();

        var totalSize = sizeCalc.Execute("");

        // Expected: 2048 + 1100 + 256 + 1500 + 5000 + 12000 + 3500 + 8000 + 45000 = 78404
        ScenarioExpect.Equal(78404, totalSize);
    }

    [Scenario("BuildProjectStructure Searcher Finds TypeScript Files")]
    [Fact]
    public void BuildProjectStructure_Searcher_Finds_TypeScript_Files()
    {
        var (_, searcher) = BuildProjectStructure();

        var results = searcher.Execute(".ts");

        ScenarioExpect.Equal(4, results.Count);
        ScenarioExpect.Contains(results, r => r.Name == "index.ts");
        ScenarioExpect.Contains(results, r => r.Name == "app.ts");
        ScenarioExpect.Contains(results, r => r.Name == "utils.ts");
        ScenarioExpect.Contains(results, r => r.Name == "app.spec.ts");
    }

    [Scenario("BuildProjectStructure Searcher Finds Json Files")]
    [Fact]
    public void BuildProjectStructure_Searcher_Finds_Json_Files()
    {
        var (_, searcher) = BuildProjectStructure();

        var results = searcher.Execute(".json");

        ScenarioExpect.Equal(2, results.Count);
        ScenarioExpect.Contains(results, r => r.Name == "package.json");
        ScenarioExpect.Contains(results, r => r.Name == "coverage.json");
    }

    [Scenario("BuildProjectStructure Searcher CaseInsensitive")]
    [Fact]
    public void BuildProjectStructure_Searcher_CaseInsensitive()
    {
        var (_, searcher) = BuildProjectStructure();

        var lowerResults = searcher.Execute("readme");
        var upperResults = searcher.Execute("README");

        ScenarioExpect.Single(lowerResults);
        ScenarioExpect.Single(upperResults);
    }

    [Scenario("Run Executes Without Errors")]
    [Fact]
    public void Run_Executes_Without_Errors()
    {
        PatternKit.Examples.CompositeDemo.CompositeDemo.Run();
    }
}
