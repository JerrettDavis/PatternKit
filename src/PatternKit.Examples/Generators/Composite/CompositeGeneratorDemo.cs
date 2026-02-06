using System;
using System.Collections.Generic;
using PatternKit.Generators.Composite;

namespace PatternKit.Examples.Generators.Composite;

// ============================================================================
// EXAMPLE: Composite Pattern - File System Tree
// ============================================================================
// Demonstrates the Composite pattern modelling a file system where files and
// directories implement the same component interface. Directories are composites
// that delegate operations to their children.
// ============================================================================

#region Component Contract

/// <summary>
/// Defines the contract for file system entries (files and directories).
/// </summary>
[CompositeComponent(GenerateTraversalHelpers = true)]
public partial interface IFileSystemEntry
{
    /// <summary>Gets the display representation of this entry at a given indent level.</summary>
    string Display(int indent);

    /// <summary>Gets the total size in bytes.</summary>
    long GetSize();
}

#endregion

#region Leaf - File

/// <summary>A leaf file node in the file system tree.</summary>
public class File : FileSystemEntryBase
{
    private readonly string _name;
    private readonly long _size;

    public File(string name, long size)
    {
        _name = name;
        _size = size;
    }

    public override string Display(int indent)
        => $"{new string(' ', indent * 2)}{_name} ({_size} bytes)";

    public override long GetSize() => _size;
}

#endregion

#region Composite - Directory

/// <summary>A composite directory node that contains children.</summary>
public class Directory : FileSystemEntryComposite
{
    private readonly string _name;

    public Directory(string name)
    {
        _name = name;
    }

    public override string Display(int indent)
    {
        var lines = new List<string>();
        lines.Add($"{new string(' ', indent * 2)}{_name}/");
        for (int i = 0; i < Children.Count; i++)
        {
            lines.Add(Children[i].Display(indent + 1));
        }
        return string.Join("\n", lines);
    }

    /// <summary>Returns the total size of all files in this directory.</summary>
    public override long GetSize()
    {
        long total = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            total += Children[i].GetSize();
        }
        return total;
    }
}

#endregion

#region Demo

/// <summary>
/// Demonstrates the Composite pattern generator building a file system tree.
/// </summary>
public static class CompositeGeneratorDemo
{
    /// <summary>
    /// Runs the composite demo, building and displaying a file system tree.
    /// </summary>
    public static List<string> Run()
    {
        var log = new List<string>();

        // Build tree:
        // root/
        //   readme.txt (100 bytes)
        //   src/
        //     main.cs (500 bytes)
        //     util.cs (300 bytes)
        //   docs/
        //     guide.md (200 bytes)

        var root = new Directory("root");

        var readme = new File("readme.txt", 100);
        root.Add(readme);

        var src = new Directory("src");
        src.Add(new File("main.cs", 500));
        src.Add(new File("util.cs", 300));
        root.Add(src);

        var docs = new Directory("docs");
        docs.Add(new File("guide.md", 200));
        root.Add(docs);

        // Display tree
        log.Add(root.Display(0));

        // Total size
        log.Add($"Total size: {root.GetSize()} bytes");

        // Traversal (depth-first)
        var count = 0;
        foreach (var entry in root.DepthFirst())
        {
            count++;
        }
        log.Add($"Depth-first node count: {count}");

        // Traversal (breadth-first)
        count = 0;
        foreach (var entry in root.BreadthFirst())
        {
            count++;
        }
        log.Add($"Breadth-first node count: {count}");

        return log;
    }
}

#endregion
