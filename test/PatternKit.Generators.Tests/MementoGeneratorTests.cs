using Microsoft.CodeAnalysis;
using PatternKit.Common;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public class MementoGeneratorTests
{
    [Scenario("GenerateMementoForClass")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Memento struct is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("Document.Memento.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateMementoForRecordClass")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Memento struct is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("EditorState.Memento.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateMementoForRecordStruct")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Memento struct is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("Point.Memento.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateMementoForStruct")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Memento struct is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("Counter.Memento.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ErrorWhenNotPartial")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKMEM001");
    }

    [Scenario("GenerateCaretakerWhenRequested")]
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
        ScenarioExpect.Contains("EditorState.Memento.g.cs", names);
        ScenarioExpect.Contains("EditorState.History.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("MemberExclusionWithIgnore")]
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

        ScenarioExpect.NotEqual(default, mementoSourceResult);
        var mementoSource = mementoSourceResult.SourceText.ToString();

        // Memento includes Text but not InternalId
        ScenarioExpect.Contains("string Text", mementoSource); // Type might be "string" or "global::System.String"
        ScenarioExpect.DoesNotContain("InternalId", mementoSource);
    }

    [Scenario("RecordFallbackConstructorExcludesCopyConstructor")]
    [Fact]
    public void RecordFallbackConstructorExcludesCopyConstructor()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento]
            public sealed partial record class EditorState(string Text, [property: MementoIgnore] string Secret);
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(RecordFallbackConstructorExcludesCopyConstructor));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.DoesNotContain(r.Diagnostics, d => d.Severity == DiagnosticSeverity.Error));
        var mementoSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Memento.g.cs"))
            .SourceText.ToString();

        ScenarioExpect.Contains("new global::TestNamespace.EditorState(default!, default!)", mementoSource);
        ScenarioExpect.DoesNotContain("new global::TestNamespace.EditorState(default!)", mementoSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ExplicitInclusionMode")]
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

        ScenarioExpect.NotEqual(default, mementoSourceResult);
        var mementoSource = mementoSourceResult.SourceText.ToString();

        // Memento includes Text but not InternalData
        ScenarioExpect.Contains("string Text", mementoSource); // Type might be "string" or "global::System.String"
        ScenarioExpect.DoesNotContain("InternalData", mementoSource);
    }

    [Scenario("WarningForMutableReferenceCapture")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKMEM003" && d.GetMessage().Contains("Tags"));
    }

    [Scenario("GeneratedMementoHasCaptureAndRestore")]
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

        ScenarioExpect.NotEqual(default, mementoSourceResult);
        var mementoSource = mementoSourceResult.SourceText.ToString();

        // Verify Capture and RestoreNew methods exist
        ScenarioExpect.Contains("public static EditorStateMemento Capture", mementoSource);
        ScenarioExpect.Contains("public global::TestNamespace.EditorState RestoreNew()", mementoSource);
    }

    [Scenario("GeneratedCaretakerHasUndoRedo")]
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

        ScenarioExpect.NotEqual(default, caretakerSourceResult);
        var caretakerSource = caretakerSourceResult.SourceText.ToString();

        // Verify caretaker has undo/redo functionality
        ScenarioExpect.Contains("public bool Undo()", caretakerSource);
        ScenarioExpect.Contains("public bool Redo()", caretakerSource);
        ScenarioExpect.Contains("public void Capture", caretakerSource);
        ScenarioExpect.Contains("public bool CanUndo", caretakerSource);
        ScenarioExpect.Contains("public bool CanRedo", caretakerSource);
    }

    [Scenario("GeneratedMementoIncludesVersion")]
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

        ScenarioExpect.NotEqual(default, mementoSourceResult);
        var mementoSource = mementoSourceResult.SourceText.ToString();

        // Verify MementoVersion property exists
        ScenarioExpect.Contains("public int MementoVersion", mementoSource);
    }

    [Scenario("CaretakerRespectsCapacity")]
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

        ScenarioExpect.NotEqual(default, caretakerSourceResult);
        var caretakerSource = caretakerSourceResult.SourceText.ToString();

        // Verify capacity setting (using regex for flexibility)
        ScenarioExpect.Matches(@"private\s+const\s+int\s+MaxCapacity\s*=\s*50", caretakerSource);
        ScenarioExpect.Matches(@"if\s*\(\s*_states\.Count\s*>\s*MaxCapacity\s*\)", caretakerSource);
    }

    [Scenario("ExplicitCaptureStrategies AreAcceptedWithoutReferenceWarnings")]
    [Fact]
    public void ExplicitCaptureStrategies_AreAcceptedWithoutReferenceWarnings()
    {
        const string source = """
            using PatternKit.Generators;
            using System.Collections.Generic;

            namespace TestNamespace;

            [Memento]
            public partial class Document
            {
                [MementoStrategy(MementoCaptureStrategy.ByReference)]
                public List<string> Tags { get; set; } = new();

                [MementoStrategy(MementoCaptureStrategy.Clone)]
                public CloneableBuffer Cloneable { get; set; } = new();

                [MementoStrategy(MementoCaptureStrategy.DeepCopy)]
                public CloneableBuffer Deep { get; set; } = new();

                [MementoStrategy(MementoCaptureStrategy.Custom)]
                public CloneableBuffer Custom { get; set; } = new();
            }

            public sealed class CloneableBuffer
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ExplicitCaptureStrategies_AreAcceptedWithoutReferenceWarnings));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.DoesNotContain(result.Results.SelectMany(r => r.Diagnostics), d => d.Id == "PKMEM003");

        var mementoSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(gs => gs.HintName == "Document.Memento.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("global::System.Collections.Generic.List<string> Tags", mementoSource);
        ScenarioExpect.Contains("global::TestNamespace.CloneableBuffer Cloneable", mementoSource);
        ScenarioExpect.Contains("global::TestNamespace.CloneableBuffer Deep", mementoSource);
        ScenarioExpect.Contains("global::TestNamespace.CloneableBuffer Custom", mementoSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("RecordWithoutPrimaryConstructor RestoresWithObjectInitializer")]
    [Fact]
    public void RecordWithoutPrimaryConstructor_RestoresWithObjectInitializer()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento]
            public partial record class EditorState
            {
                public string Text { get; init; } = "";
                public int Cursor { get; init; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(RecordWithoutPrimaryConstructor_RestoresWithObjectInitializer));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var mementoSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(gs => gs.HintName == "EditorState.Memento.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("return new global::TestNamespace.EditorState()", mementoSource);
        ScenarioExpect.Contains("Text = this.Text,", mementoSource);
        ScenarioExpect.Contains("Cursor = this.Cursor,", mementoSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ClassWithOnlyReadonlyMembers RestoresWithDefaultConstructor")]
    [Fact]
    public void ClassWithOnlyReadonlyMembers_RestoresWithDefaultConstructor()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento]
            public partial class ReadOnlyState
            {
                public readonly int Version;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ClassWithOnlyReadonlyMembers_RestoresWithDefaultConstructor));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var mementoSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(gs => gs.HintName == "ReadOnlyState.Memento.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("public int Version { get; }", mementoSource);
        ScenarioExpect.Contains("return new global::TestNamespace.ReadOnlyState();", mementoSource);
        ScenarioExpect.DoesNotContain("originator.Version = this.Version;", mementoSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("RecordWithNoCapturedMembers RestoresWithDefaultConstructor")]
    [Fact]
    public void RecordWithNoCapturedMembers_RestoresWithDefaultConstructor()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento]
            public partial record class EmptyState;
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(RecordWithNoCapturedMembers_RestoresWithDefaultConstructor));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var mementoSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(gs => gs.HintName == "EmptyState.Memento.g.cs")
            .SourceText.ToString();

        ScenarioExpect.DoesNotContain("private EmptyStateMemento()", mementoSource);
        ScenarioExpect.Contains("return new global::TestNamespace.EmptyState();", mementoSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("RecordWithIgnoredPrimaryConstructorMember FallsBackToObjectInitializer")]
    [Fact]
    public void RecordWithIgnoredPrimaryConstructorMember_FallsBackToObjectInitializer()
    {
        const string source = """
            using PatternKit.Generators;

            namespace TestNamespace;

            [Memento]
            public partial record class FilteredState([property: MementoIgnore] string Raw)
            {
                public string Name { get; init; } = Raw;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(RecordWithIgnoredPrimaryConstructorMember_FallsBackToObjectInitializer));
        var gen = new MementoGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var mementoSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(gs => gs.HintName == "FilteredState.Memento.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("return new global::TestNamespace.FilteredState(default!)", mementoSource);
        ScenarioExpect.Contains("Name = this.Name,", mementoSource);
        ScenarioExpect.DoesNotContain("Raw = this.Raw", mementoSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
