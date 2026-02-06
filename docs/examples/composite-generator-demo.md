# Composite Generator Demo: File System Tree

Demonstrates the Composite pattern generator modelling a **file system** where files (leaves) and directories (composites) share a common component interface.

* **Goal:** build a tree of file system entries where operations like `GetSize()` and `Display()` work uniformly on individual files and entire directory trees.
* **Key idea:** `[CompositeComponent]` generates `FileSystemEntryBase` (leaf defaults) and `FileSystemEntryComposite` (child management + delegation), so concrete types just override what they need.

---

## The demo

```csharp
using PatternKit.Generators.Composite;

[CompositeComponent(GenerateTraversalHelpers = true)]
public partial interface IFileSystemEntry
{
    string Display(int indent);
    long GetSize();
}

// Leaf
public class File : FileSystemEntryBase
{
    public override string Display(int indent) => $"{new string(' ', indent * 2)}{_name} ({_size} bytes)";
    public override long GetSize() => _size;
}

// Composite
public class Directory : FileSystemEntryComposite
{
    public override string Display(int indent) { /* display self + children */ }
    // GetSize() inherited: iterates children and returns last result
}
```

## Mental model

```
IFileSystemEntry (component)
  │
  ├── FileSystemEntryBase (generated leaf)
  │     └── File (concrete leaf)
  │
  └── FileSystemEntryComposite (generated composite)
        └── Directory (concrete composite)
              ├── File
              ├── Directory
              │     └── File
              └── File
```

## Test references

- `CompositeGeneratorDemoTests.Demo_Runs_Successfully` — full demo with tree display and size
- `CompositeGeneratorDemoTests.Directory_GetSize_Sums_Children` — composite aggregation
- `CompositeGeneratorDemoTests.Nested_Directory_Shows_Indentation` — tree display hierarchy
- `CompositeGeneratorDemoTests.Add_Remove_Manages_Children` — child management
- `CompositeGeneratorDemoTests.Traversal_Enumerates_All_Nodes` — depth-first traversal
