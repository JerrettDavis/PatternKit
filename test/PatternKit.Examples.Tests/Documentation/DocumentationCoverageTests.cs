using System.Text.RegularExpressions;
using TinyBDD;

namespace PatternKit.Examples.Tests.Documentation;

public sealed class DocumentationCoverageTests
{
    private static readonly Regex TocHrefRegex = new(@"href:\s*(?<href>[^\s#]+)", RegexOptions.Compiled);

    [Scenario("Toc Hrefs Point To Existing Files")]
    [Theory]
    [InlineData("docs/generators/toc.yml")]
    [InlineData("docs/examples/toc.yml")]
    [InlineData("docs/patterns/toc.yml")]
    public void Toc_Hrefs_Point_To_Existing_Files(string relativeTocPath)
    {
        var root = FindRepoRoot();
        var tocPath = Path.Combine(root, Normalize(relativeTocPath));
        var tocDirectory = Path.GetDirectoryName(tocPath)!;

        foreach (Match match in TocHrefRegex.Matches(File.ReadAllText(tocPath)))
        {
            var href = match.Groups["href"].Value;
            if (href.EndsWith("/", StringComparison.Ordinal))
            {
                continue;
            }

            var target = Path.GetFullPath(Path.Combine(tocDirectory, Normalize(href)));

            ScenarioExpect.True(File.Exists(target), $"{relativeTocPath} references missing file: {href}");
        }
    }

    [Scenario("Generator Toc Includes All Generator Pages")]
    [Fact]
    public void Generator_Toc_Includes_All_Generator_Pages()
    {
        var root = FindRepoRoot();
        var generatorDocs = Path.Combine(root, "docs", "generators");
        var toc = File.ReadAllText(Path.Combine(generatorDocs, "toc.yml"));
        var expectedPages = Directory
            .EnumerateFiles(generatorDocs, "*.md")
            .Select(Path.GetFileName)
            .Where(name => !string.Equals(name, "index.md", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase);

        foreach (var page in expectedPages)
        {
            ScenarioExpect.Contains($"href: {page}", toc, StringComparison.Ordinal);
        }
    }

    [Scenario("Example Toc Exposes Generator And Messaging Suites")]
    [Fact]
    public void Example_Toc_Exposes_Generator_And_Messaging_Suites()
    {
        var root = FindRepoRoot();
        var toc = File.ReadAllText(Path.Combine(root, "docs", "examples", "toc.yml"));

        ScenarioExpect.Contains("href: source-generator-application-suite.md", toc, StringComparison.Ordinal);
        ScenarioExpect.Contains("href: enterprise-messaging-workflows.md", toc, StringComparison.Ordinal);
    }

    [Scenario("Source Generator Application Suite Maps Example Families To Tests")]
    [Fact]
    public void Source_Generator_Application_Suite_Maps_Example_Families_To_Tests()
    {
        var root = FindRepoRoot();
        var doc = File.ReadAllText(Path.Combine(root, "docs", "examples", "source-generator-application-suite.md"));

        var expectedGeneratorFamilies = new[]
        {
            "Builder",
            "Factory Method",
            "Factory Class",
            "Facade",
            "Memento",
            "State Machine",
            "Strategy",
            "Visitor",
            "Adapter",
            "Observer",
            "Proxy",
            "Singleton",
            "Template Method",
            "Messaging"
        };

        foreach (var family in expectedGeneratorFamilies)
        {
            ScenarioExpect.Contains($"| {family} |", doc, StringComparison.Ordinal);
        }

        ScenarioExpect.Contains("test/PatternKit.Examples.Tests/Generators", doc, StringComparison.Ordinal);
        ScenarioExpect.Contains("test/PatternKit.Examples.Tests/Messaging", doc, StringComparison.Ordinal);
    }

    [Scenario("Enterprise Messaging Workflow Suite Maps Runtime And Generated Patterns")]
    [Fact]
    public void Enterprise_Messaging_Workflow_Suite_Maps_Runtime_And_Generated_Patterns()
    {
        var root = FindRepoRoot();
        var doc = File.ReadAllText(Path.Combine(root, "docs", "examples", "enterprise-messaging-workflows.md"));

        var expectedPatterns = new[]
        {
            "Message envelope/context",
            "Content router",
            "Recipient list",
            "Splitter",
            "Aggregator",
            "Routing slip",
            "Saga/process manager",
            "Mailbox",
            "Idempotent receiver",
            "Inbox/outbox",
            "Source-generated dispatcher",
            "Source-generated content router"
        };

        foreach (var pattern in expectedPatterns)
        {
            ScenarioExpect.Contains($"| {pattern} |", doc, StringComparison.Ordinal);
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PatternKit.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find PatternKit repository root.");
    }

    private static string Normalize(string path)
        => path.Replace('/', Path.DirectorySeparatorChar);
}
