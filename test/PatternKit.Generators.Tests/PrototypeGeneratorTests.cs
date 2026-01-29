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

    [Fact]
    public void ErrorOnNoConstructionPath()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype]
            public partial class Container
            {
                public string Value { get; }
                
                public Container(string value)
                {
                    Value = value;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorOnNoConstructionPath));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKPRO002 error for no construction path
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRO002" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ErrorOnGenericType()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype]
            public partial class GenericContainer<T>
            {
                public T Value { get; set; } = default!;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorOnGenericType));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKPRO008 error for generic type
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRO008" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ErrorOnNestedType()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            public class OuterClass
            {
                [Prototype]
                public partial class InnerClass
                {
                    public string Value { get; set; } = "";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorOnNestedType));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKPRO009 error for nested type
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRO009" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ErrorOnStaticCloneMethod()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            public class DataWithStaticClone
            {
                public string Value { get; set; } = "";
                
                // Static Clone method - should NOT be treated as valid
                public static DataWithStaticClone Clone()
                {
                    return new DataWithStaticClone();
                }
            }

            [Prototype]
            public partial class Container
            {
                [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
                public DataWithStaticClone Data { get; set; } = new();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorOnStaticCloneMethod));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKPRO004 error because static Clone() is not a valid mechanism
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRO004" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void SucceedWithPrivateParameterlessConstructor()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype]
            public partial class SecureContainer
            {
                public string Value { get; set; } = "";
                
                private SecureContainer()
                {
                }
                
                public static SecureContainer Create() => new SecureContainer();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(SucceedWithPrivateParameterlessConstructor));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should succeed - private parameterless constructor is accessible from generated code
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.DoesNotContain(diagnostics, d => d.Id == "PKPRO002");

        // Compilation should succeed
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void SucceedWithInitOnlyPropertiesOnClass()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype]
            public partial class ImmutableData
            {
                public string Name { get; init; } = "";
                public int Value { get; init; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(SucceedWithInitOnlyPropertiesOnClass));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should succeed - init-only properties can be set in object initializers
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        // Compilation should succeed
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify init-only properties are cloned
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "ImmutableData.Prototype.g.cs")
            .SourceText.ToString();
        Assert.Contains("Name = this.Name", generatedSource);
        Assert.Contains("Value = this.Value", generatedSource);
    }

    [Fact]
    public void SucceedWithInitOnlyPropertiesWithCopyConstructor()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype]
            public partial class DataWithCtor
            {
                public string Name { get; init; } = "";
                public int Value { get; set; }
                
                public DataWithCtor() { }
                
                public DataWithCtor(DataWithCtor other)
                {
                    Name = other.Name;
                    Value = other.Value;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(SucceedWithInitOnlyPropertiesWithCopyConstructor));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should succeed - copy constructor handles init-only properties
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        // Compilation should succeed
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify copy constructor is used
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "DataWithCtor.Prototype.g.cs")
            .SourceText.ToString();
        Assert.Contains("new global::TestNamespace.DataWithCtor(this)", generatedSource);
    }

    [Fact]
    public void ErrorOnAbstractClass()
    {
        const string source = """
            using PatternKit.Generators.Prototype;

            namespace TestNamespace;

            [Prototype]
            public abstract partial class AbstractBase
            {
                public string Value { get; set; } = "";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorOnAbstractClass));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKPRO010 error for abstract type
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRO010" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void SucceedWithCustomStrategy()
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
                
                private static partial CustomData CloneData(CustomData value);
            }
            
            public partial class Container
            {
                private static partial CustomData CloneData(CustomData value)
                {
                    return new CustomData { Value = value.Value + "_cloned" };
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(SucceedWithCustomStrategy));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should succeed - custom partial method is provided
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.DoesNotContain(diagnostics, d => d.Id == "PKPRO005");
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        // Compilation should succeed
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify custom method is called
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "Container.Prototype.g.cs")
            .SourceText.ToString();
        Assert.Contains("CloneData(this.Data)", generatedSource);
    }

    [Fact]
    public void SucceedWithDeepWhenPossibleMode()
    {
        const string source = """
            using PatternKit.Generators.Prototype;
            using System.Collections.Generic;

            namespace TestNamespace;

            public class CloneableData
            {
                public string Value { get; set; } = "";
                public CloneableData Clone() => new CloneableData { Value = this.Value };
            }

            public class NonCloneableData
            {
                public string Value { get; set; } = "";
            }

            [Prototype(Mode = PrototypeMode.DeepWhenPossible)]
            public partial class Container
            {
                // Should use Clone strategy automatically
                public CloneableData Cloneable { get; set; } = new();
                
                // Should fall back to by-reference (no warning in DeepWhenPossible mode)
                public NonCloneableData NonCloneable { get; set; } = new();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(SucceedWithDeepWhenPossibleMode));
        var gen = new PrototypeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should succeed - DeepWhenPossible mode clones what it can
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        // No warnings in DeepWhenPossible mode
        Assert.DoesNotContain(diagnostics, d => d.Id == "PKPRO003");

        // Compilation should succeed
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify cloneable uses Clone() and non-cloneable uses by-reference
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "Container.Prototype.g.cs")
            .SourceText.ToString();
        Assert.Contains("Cloneable.Clone()", generatedSource);
        Assert.Contains("this.NonCloneable", generatedSource);
    }
}
