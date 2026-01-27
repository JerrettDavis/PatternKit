using Microsoft.CodeAnalysis;
using PatternKit.Common;

namespace PatternKit.Generators.Tests;

public class PrototypeGeneratorTests
{
    [Fact]
    public void GenerateCloneForClass()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype]
            public partial class Person
            {
                public string Name { get; set; } = "";
                public int Age { get; set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateCloneForClass));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Clone method is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("Person.Prototype.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateCloneForRecordClass()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype]
            public partial record class Person(string Name, int Age);
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateCloneForRecordClass));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Clone method is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("Person.Prototype.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Records get "Duplicate" method by default
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "Person.Prototype.g.cs")
            .SourceText.ToString();
        Assert.Contains("Duplicate()", generatedSource);
    }

    [Fact]
    public void GenerateCloneForRecordStruct()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype]
            public partial record struct Point(int X, int Y);
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateCloneForRecordStruct));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Clone method is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("Point.Prototype.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Records get "Duplicate" method by default
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "Point.Prototype.g.cs")
            .SourceText.ToString();
        Assert.Contains("Duplicate()", generatedSource);
    }

    [Fact]
    public void GenerateCloneForStruct()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype]
            public partial struct Vector
            {
                public double X { get; set; }
                public double Y { get; set; }
                public double Z { get; set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateCloneForStruct));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Clone method is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("Vector.Prototype.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void ErrorIfNotPartial()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype]
            public class NonPartialClass
            {
                public int Value { get; set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorIfNotPartial));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKPRO001 diagnostic
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRO001");
    }

    [Fact]
    public void GenerateCloneWithCustomMethodName()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype(CloneMethodName = "Duplicate")]
            public partial class Item
            {
                public string Name { get; set; } = "";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateCloneWithCustomMethodName));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Clone method is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("Item.Prototype.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Check that the custom method name is used
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "Item.Prototype.g.cs")
            .SourceText.ToString();
        Assert.Contains("Duplicate()", generatedSource);
        Assert.DoesNotContain("Clone()", generatedSource);
    }

    [Fact]
    public void GenerateCloneWithIgnoreAttribute()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype]
            public partial class User
            {
                public string Username { get; set; } = "";
                
                [PrototypeIgnore]
                public string Password { get; set; } = "";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateCloneWithIgnoreAttribute));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Check that Password is not cloned
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "User.Prototype.g.cs")
            .SourceText.ToString();
        Assert.Contains("Username", generatedSource);
        Assert.DoesNotContain("Password", generatedSource);
    }

    [Fact]
    public void GenerateCloneWithExplicitInclude()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype(IncludeExplicit = true)]
            public partial class Config
            {
                [PrototypeInclude]
                public string ApiKey { get; set; } = "";
                
                public string Internal { get; set; } = "";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateCloneWithExplicitInclude));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Check that only ApiKey is cloned
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "Config.Prototype.g.cs")
            .SourceText.ToString();
        Assert.Contains("ApiKey", generatedSource);
        Assert.DoesNotContain("Internal", generatedSource);
    }

    [Fact]
    public void WarnOnMutableReferenceType()
    {
        const string source = """
            using PatternKit.Generators.Prototype;
            using System.Collections.Generic;

            namespace TestNamespace;

            [Prototype]
            public partial class Container
            {
                public List<string> Items { get; set; } = new();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(WarnOnMutableReferenceType));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKPRO003 warning for mutable reference type
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRO003" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ErrorOnCloneStrategyWithoutMechanism()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            public class NonCloneable
            {
                public string Value { get; set; } = "";
            }

            [Prototype]
            public partial class Container
            {
                [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
                public NonCloneable Data { get; set; } = new();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorOnCloneStrategyWithoutMechanism));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKPRO004 error for missing clone mechanism
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRO004" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ErrorOnCustomStrategyWithoutPartialMethod()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            public class CustomData
            {
                public string Value { get; set; } = "";
            }

            [Prototype]
            public partial class Container
            {
                [PrototypeStrategy(PrototypeCloneStrategy.Custom)]
                public CustomData Data { get; set; } = new();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorOnCustomStrategyWithoutPartialMethod));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKPRO005 error for missing custom hook
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRO005" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void WarnOnAttributeMisuseIncludeInIncludeAllMode()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype]
            public partial class Container
            {
                [PrototypeInclude]
                public string Value { get; set; } = "";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(WarnOnAttributeMisuseIncludeInIncludeAllMode));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKPRO006 warning for attribute misuse
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRO006" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void WarnOnAttributeMisuseIgnoreInExplicitMode()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype(IncludeExplicit = true)]
            public partial class Container
            {
                [PrototypeIgnore]
                public string Value { get; set; } = "";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(WarnOnAttributeMisuseIgnoreInExplicitMode));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKPRO006 warning for attribute misuse
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRO006" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ErrorOnDeepCopyStrategy()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            public class ComplexData
            {
                public string Value { get; set; } = "";
            }

            [Prototype]
            public partial class Container
            {
                [PrototypeStrategy(PrototypeCloneStrategy.DeepCopy)]
                public ComplexData Data { get; set; } = new();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorOnDeepCopyStrategy));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKPRO007 error for DeepCopy not implemented
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRO007" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void GenerateCloneWithShallowCopyStrategy()
    {
        const string source = """
            using PatternKit.Generators.Prototype;
            using System.Collections.Generic;

            namespace TestNamespace;

            [Prototype(Mode = PrototypeMode.Shallow)]
            public partial class Container
            {
                [PrototypeStrategy(PrototypeCloneStrategy.ShallowCopy)]
                public List<string> Items { get; set; } = new();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateCloneWithShallowCopyStrategy));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No errors
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Check that new List<string> is created (using fully qualified name)
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "Container.Prototype.g.cs")
            .SourceText.ToString();
        Assert.Contains("new global::System.Collections.Generic.List<string>(this.Items)", generatedSource);
    }

    [Fact]
    public void GenerateCloneWithCloneStrategyUsingICloneable()
    {
        const string source = """
            using PatternKit.Generators.Prototype;
            using System;

            namespace TestNamespace;

            public class CloneableData : ICloneable
            {
                public string Value { get; set; } = "";
                public object Clone() => new CloneableData { Value = this.Value };
            }

            [Prototype]
            public partial class Container
            {
                [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
                public CloneableData Data { get; set; } = new();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateCloneWithCloneStrategyUsingICloneable));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No errors
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Check that Clone() is called
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "Container.Prototype.g.cs")
            .SourceText.ToString();
        Assert.Contains("Clone()", generatedSource);
    }

    [Fact]
    public void GenerateCloneWithCloneStrategyUsingCloneMethod()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            public class DataWithClone
            {
                public string Value { get; set; } = "";
                public DataWithClone Clone() => new DataWithClone { Value = this.Value };
            }

            [Prototype]
            public partial class Container
            {
                [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
                public DataWithClone Data { get; set; } = new();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateCloneWithCloneStrategyUsingCloneMethod));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No errors
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Check that Clone() is called
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "Container.Prototype.g.cs")
            .SourceText.ToString();
        Assert.Contains("Clone()", generatedSource);
    }

    [Fact]
    public void GenerateCloneWithCloneStrategyUsingCopyConstructor()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            public class DataWithCopyCtor
            {
                public string Value { get; set; } = "";
                
                public DataWithCopyCtor() { }
                public DataWithCopyCtor(DataWithCopyCtor other)
                {
                    Value = other.Value;
                }
            }

            [Prototype]
            public partial class Container
            {
                [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
                public DataWithCopyCtor Data { get; set; } = new();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateCloneWithCloneStrategyUsingCopyConstructor));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No errors
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Check that copy constructor is called
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "Container.Prototype.g.cs")
            .SourceText.ToString();
        Assert.Contains("new global::TestNamespace.DataWithCopyCtor(this.Data)", generatedSource);
    }

    [Fact]
    public void GenerateCloneWithCloneStrategyForListCollection()
    {
        const string source = """
            using PatternKit.Generators.Prototype;
            using System.Collections.Generic;

            namespace TestNamespace;

            [Prototype]
            public partial class Container
            {
                [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
                public List<string> Items { get; set; } = new();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateCloneWithCloneStrategyForListCollection));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No errors
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Check that List copy constructor is used (using fully qualified name)
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "Container.Prototype.g.cs")
            .SourceText.ToString();
        Assert.Contains("new global::System.Collections.Generic.List<string>(this.Items)", generatedSource);
    }
}
