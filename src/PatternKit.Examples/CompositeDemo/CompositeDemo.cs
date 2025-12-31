using PatternKit.Structural.Composite;

namespace PatternKit.Examples.CompositeDemo;

/// <summary>
/// Demonstrates the Composite pattern for building hierarchical file system operations.
/// This example shows a virtual file system with files and directories that support
/// unified operations (size calculation, search).
/// </summary>
/// <remarks>
/// <para>
/// <b>Real-world scenario:</b> A file synchronization service that needs to calculate
/// sizes and search content uniformly across files and directories.
/// </para>
/// <para>
/// <b>Key GoF concepts demonstrated:</b>
/// <list type="bullet">
/// <item>Component (unified interface for files and directories)</item>
/// <item>Leaf (File - has no children)</item>
/// <item>Composite (Directory - contains files and subdirectories)</item>
/// <item>Uniform treatment - operations work on both individual files and entire trees</item>
/// </list>
/// </para>
/// </remarks>
public static class CompositeDemo
{
    // ─────────────────────────────────────────────────────────────────────────
    // File System Types
    // ─────────────────────────────────────────────────────────────────────────

    public sealed record FileInfo(string Name, long Size, DateTime Modified);
    public sealed record SearchResult(string Path, string Name, long Size);

    // ─────────────────────────────────────────────────────────────────────────
    // Size Calculation Composite (calculates total size of files/directories)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a leaf node for a file with known size.
    /// </summary>
    public static Composite<string, long>.Builder CreateFileSize(long size) =>
        Composite<string, long>.Leaf((in string _) => size);

    /// <summary>
    /// Creates a directory builder that sums up all child sizes.
    /// </summary>
    public static Composite<string, long>.Builder CreateDirectorySizeBuilder() =>
        Composite<string, long>.Node(
            seed: (in string _) => 0L,
            combine: (in string _, long acc, long childSize) => acc + childSize);

    // ─────────────────────────────────────────────────────────────────────────
    // File Search Composite (searches files matching a pattern)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a leaf node that returns matching files.
    /// </summary>
    public static Composite<string, List<SearchResult>>.Builder CreateFileSearcher(FileInfo file, string path) =>
        Composite<string, List<SearchResult>>.Leaf((in string pattern) =>
        {
            var results = new List<SearchResult>();
            if (file.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new SearchResult(path, file.Name, file.Size));
            }
            return results;
        });

    /// <summary>
    /// Creates a directory builder that aggregates search results from children.
    /// </summary>
    public static Composite<string, List<SearchResult>>.Builder CreateDirectorySearchBuilder() =>
        Composite<string, List<SearchResult>>.Node(
            seed: (in string _) => new List<SearchResult>(),
            combine: (in string _, List<SearchResult> acc, List<SearchResult> childResults) =>
            {
                acc.AddRange(childResults);
                return acc;
            });

    // ─────────────────────────────────────────────────────────────────────────
    // Demo File System Structure
    // ─────────────────────────────────────────────────────────────────────────

    public static (Composite<string, long> sizeCalc, Composite<string, List<SearchResult>> searcher)
        BuildProjectStructure()
    {
        // Define file info
        var readme = new FileInfo("README.md", 2048, DateTime.Now);
        var license = new FileInfo("LICENSE", 1100, DateTime.Now);
        var gitignore = new FileInfo(".gitignore", 256, DateTime.Now);
        var packageJson = new FileInfo("package.json", 1500, DateTime.Now);
        var indexTs = new FileInfo("index.ts", 5000, DateTime.Now);
        var appTs = new FileInfo("app.ts", 12000, DateTime.Now);
        var utilsTs = new FileInfo("utils.ts", 3500, DateTime.Now);
        var testSpec = new FileInfo("app.spec.ts", 8000, DateTime.Now);
        var coverageJson = new FileInfo("coverage.json", 45000, DateTime.Now);

        // Build size calculator tree
        var sizeCalc = CreateDirectorySizeBuilder()  // root
            .AddChild(CreateFileSize(readme.Size))
            .AddChild(CreateFileSize(license.Size))
            .AddChild(CreateFileSize(gitignore.Size))
            .AddChild(CreateFileSize(packageJson.Size))
            .AddChild(CreateDirectorySizeBuilder()  // src/
                .AddChild(CreateFileSize(indexTs.Size))
                .AddChild(CreateFileSize(appTs.Size))
                .AddChild(CreateFileSize(utilsTs.Size)))
            .AddChild(CreateDirectorySizeBuilder()  // test/
                .AddChild(CreateFileSize(testSpec.Size)))
            .AddChild(CreateDirectorySizeBuilder()  // coverage/
                .AddChild(CreateFileSize(coverageJson.Size)))
            .Build();

        // Build searcher tree
        var searcher = CreateDirectorySearchBuilder()
            .AddChild(CreateFileSearcher(readme, "/"))
            .AddChild(CreateFileSearcher(license, "/"))
            .AddChild(CreateFileSearcher(gitignore, "/"))
            .AddChild(CreateFileSearcher(packageJson, "/"))
            .AddChild(CreateDirectorySearchBuilder()
                .AddChild(CreateFileSearcher(indexTs, "/src"))
                .AddChild(CreateFileSearcher(appTs, "/src"))
                .AddChild(CreateFileSearcher(utilsTs, "/src")))
            .AddChild(CreateDirectorySearchBuilder()
                .AddChild(CreateFileSearcher(testSpec, "/test")))
            .AddChild(CreateDirectorySearchBuilder()
                .AddChild(CreateFileSearcher(coverageJson, "/coverage")))
            .Build();

        return (sizeCalc, searcher);
    }

    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB"];
        int i = 0;
        double size = bytes;
        while (size >= 1024 && i < suffixes.Length - 1) { size /= 1024; i++; }
        return $"{size:F1} {suffixes[i]}";
    }

    /// <summary>
    /// Runs the complete Composite pattern demonstration.
    /// </summary>
    public static void Run()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║            COMPOSITE PATTERN DEMONSTRATION                    ║");
        Console.WriteLine("║   File System Operations on Trees of Files and Directories   ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

        var (sizeCalc, searcher) = BuildProjectStructure();

        // ── Scenario 1: Calculate Total Size ──
        Console.WriteLine("▶ Scenario 1: Calculate Total Project Size");
        Console.WriteLine(new string('─', 50));
        var totalSize = sizeCalc.Execute("");
        Console.WriteLine($"  Total size: {FormatSize(totalSize)} ({totalSize:N0} bytes)");
        Console.WriteLine();

        // ── Scenario 2: Search for TypeScript files ──
        Console.WriteLine("▶ Scenario 2: Search for '.ts' files");
        Console.WriteLine(new string('─', 50));
        var results = searcher.Execute(".ts");
        Console.WriteLine($"  Found {results.Count} files:");
        foreach (var result in results)
        {
            Console.WriteLine($"    📄 {result.Path}/{result.Name} ({FormatSize(result.Size)})");
        }
        Console.WriteLine();

        // ── Scenario 3: Search for JSON files ──
        Console.WriteLine("▶ Scenario 3: Search for '.json' files");
        Console.WriteLine(new string('─', 50));
        var jsonResults = searcher.Execute(".json");
        Console.WriteLine($"  Found {jsonResults.Count} files:");
        foreach (var result in jsonResults)
        {
            Console.WriteLine($"    📄 {result.Path}/{result.Name} ({FormatSize(result.Size)})");
        }
        Console.WriteLine();

        // ── Scenario 4: Demonstrate uniform treatment ──
        Console.WriteLine("▶ Scenario 4: Uniform Treatment of Leaf and Composite");
        Console.WriteLine(new string('─', 50));

        // Single file (leaf)
        var singleFile = Composite<string, long>.Leaf((in string _) => 1024L).Build();
        Console.WriteLine($"  Single file size: {FormatSize(singleFile.Execute(""))}");

        // Directory with files (composite)
        var directory = Composite<string, long>.Node(
                seed: (in string _) => 0L,
                combine: (in string _, long acc, long child) => acc + child)
            .AddChild(Composite<string, long>.Leaf((in string _) => 1024L))
            .AddChild(Composite<string, long>.Leaf((in string _) => 2048L))
            .AddChild(Composite<string, long>.Leaf((in string _) => 512L))
            .Build();
        Console.WriteLine($"  Directory size: {FormatSize(directory.Execute(""))}");
        Console.WriteLine("  (Same Execute() call works for both!)");
        Console.WriteLine();

        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("Pattern Benefits Demonstrated:");
        Console.WriteLine("  • Files and directories treated uniformly");
        Console.WriteLine("  • Operations (size, search) work on any node");
        Console.WriteLine("  • Tree structure is transparent to client code");
        Console.WriteLine("  • Easy to add new operations without changing structure");
        Console.WriteLine("  • Builder pattern makes tree construction fluent");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
    }
}
