using Microsoft.CodeAnalysis;
using PatternKit.Common;
using PatternKit.Creational.Builder;

namespace PatternKit.Generators.Tests;

public class MementoGeneratorTests
{
    [Fact]
    public void GenerateMementoForClass()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento]
            public partial class Document
            {
                public string Text { get; set; } = "";
                public int Version { get; set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateMementoForClass));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Memento struct is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("Document.Memento.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateMementoForRecordClass()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento]
            public partial record class EditorState(string Text, int Cursor);
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateMementoForRecordClass));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Memento struct is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("EditorState.Memento.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateMementoForRecordStruct()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento]
            public partial record struct Point(int X, int Y);
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateMementoForRecordStruct));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Memento struct is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("Point.Memento.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateMementoForStruct()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento]
            public partial struct Counter
            {
                public int Value { get; set; }
                public string Name { get; set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateMementoForStruct));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Memento struct is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("Counter.Memento.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void ErrorWhenNotPartial()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento]
            public class Document
            {
                public string Text { get; set; } = "";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenNotPartial));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKMEM001 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKMEM001");
    }

    [Fact]
    public void GenerateCaretakerWhenRequested()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento(GenerateCaretaker = true, Capacity = 100)]
            public partial record class EditorState(string Text, int Cursor);
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateCaretakerWhenRequested));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Memento and caretaker are generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("EditorState.Memento.g.cs", names);
        Assert.Contains("EditorState.History.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void MemberExclusionWithIgnore()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento]
            public partial class Document
            {
                public string Text { get; set; } = "";
                
                [MementoIgnore]
                public string InternalId { get; set; } = "";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(MemberExclusionWithIgnore));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);
        
        var mementoSourceResult = result.Results
            .SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(gs => gs.HintName.Contains("Memento.g.cs"));
        
        Assert.NotEqual(default, mementoSourceResult);
        var mementoSource = mementoSourceResult.SourceText.ToString();

        // Memento includes Text but not InternalId
        Assert.Contains("string Text", mementoSource); // Type might be "string" or "global::System.String"
        Assert.DoesNotContain("InternalId", mementoSource);
    }

    [Fact]
    public void ExplicitInclusionMode()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento(InclusionMode = MementoInclusionMode.ExplicitOnly)]
            public partial class Document
            {
                [MementoInclude]
                public string Text { get; set; } = "";
                
                public string InternalData { get; set; } = "";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ExplicitInclusionMode));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);
        
        var mementoSourceResult = result.Results
            .SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(gs => gs.HintName.Contains("Memento.g.cs"));
        
        Assert.NotEqual(default, mementoSourceResult);
        var mementoSource = mementoSourceResult.SourceText.ToString();

        // Memento includes Text but not InternalData
        Assert.Contains("string Text", mementoSource); // Type might be "string" or "global::System.String"
        Assert.DoesNotContain("InternalData", mementoSource);
    }

    [Fact]
    public void WarningForMutableReferenceCapture()
    {
        const string source = """
            using PatternKit.Generators;
            using System.Collections.Generic;

            namespace TestNamespace;

            [Memento]
            public partial class Document
            {
                public string Text { get; set; } = "";
                public List<string> Tags { get; set; } = new();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(WarningForMutableReferenceCapture));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKMEM003 warning is reported for List<string>
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKMEM003" && d.GetMessage().Contains("Tags"));
    }

    [Fact]
    public void GeneratedMementoHasCaptureAndRestore()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento]
            public partial record class EditorState(string Text, int Cursor);
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GeneratedMementoHasCaptureAndRestore));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);
        
        var mementoSourceResult = result.Results
            .SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(gs => gs.HintName.Contains("Memento.g.cs"));
        
        Assert.NotEqual(default, mementoSourceResult);
        var mementoSource = mementoSourceResult.SourceText.ToString();

        // Verify Capture and RestoreNew methods exist
        Assert.Contains("public static EditorStateMemento Capture", mementoSource);
        Assert.Contains("public global::TestNamespace.EditorState RestoreNew()", mementoSource);
    }

    [Fact]
    public void GeneratedCaretakerHasUndoRedo()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento(GenerateCaretaker = true)]
            public partial record class EditorState(string Text, int Cursor);
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GeneratedCaretakerHasUndoRedo));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);
        
        var caretakerSourceResult = result.Results
            .SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(gs => gs.HintName.Contains("History.g.cs"));
        
        Assert.NotEqual(default, caretakerSourceResult);
        var caretakerSource = caretakerSourceResult.SourceText.ToString();

        // Verify caretaker has undo/redo functionality
        Assert.Contains("public bool Undo()", caretakerSource);
        Assert.Contains("public bool Redo()", caretakerSource);
        Assert.Contains("public void Capture", caretakerSource);
        Assert.Contains("public bool CanUndo", caretakerSource);
        Assert.Contains("public bool CanRedo", caretakerSource);
    }

    [Fact]
    public void GeneratedMementoIncludesVersion()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento]
            public partial class Document
            {
                public string Text { get; set; } = "";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GeneratedMementoIncludesVersion));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);
        
        var mementoSourceResult = result.Results
            .SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(gs => gs.HintName.Contains("Memento.g.cs"));
        
        Assert.NotEqual(default, mementoSourceResult);
        var mementoSource = mementoSourceResult.SourceText.ToString();

        // Verify MementoVersion property exists
        Assert.Contains("public int MementoVersion", mementoSource);
    }

    [Fact]
    public void CaretakerRespectsCapacity()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento(GenerateCaretaker = true, Capacity = 50)]
            public partial record class EditorState(string Text, int Cursor);
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(CaretakerRespectsCapacity));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);
        
        var caretakerSourceResult = result.Results
            .SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(gs => gs.HintName.Contains("History.g.cs"));
        
        Assert.NotEqual(default, caretakerSourceResult);
        var caretakerSource = caretakerSourceResult.SourceText.ToString();

        // Verify capacity setting
        Assert.Contains("private const int MaxCapacity = 50", caretakerSource);
        Assert.Contains("if (_states.Count > MaxCapacity)", caretakerSource);
    }
}
