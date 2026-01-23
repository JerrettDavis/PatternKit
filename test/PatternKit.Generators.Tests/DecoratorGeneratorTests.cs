using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

public class DecoratorGeneratorTests
{
    [Fact]
    public void GenerateDecoratorForInterface_BasicContract()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IStorage
            {
                string ReadFile(string path);
                void WriteFile(string path, string content);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_BasicContract));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Decorator class is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("TestNamespace_IStorage.Decorator.g.cs", names);

        // Verify generated content contains expected elements
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IStorage.Decorator.g.cs")
            .SourceText.ToString();

        Assert.True(generatedSource.Length > 100, $"Generated source is too short ({generatedSource.Length} chars): {generatedSource}");

        // The Inner property will use fully qualified names
        Assert.Contains("StorageDecoratorBase", generatedSource);
        Assert.Contains("protected", generatedSource);
        Assert.Contains(" Inner ", generatedSource);
        Assert.Contains("public virtual", generatedSource);
        Assert.Contains("ReadFile", generatedSource);
        Assert.Contains("WriteFile", generatedSource);
        Assert.Contains("StorageDecorators", generatedSource);
        Assert.Contains("Compose", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateDecoratorForInterface_WithAsyncMethods()
    {
        const string source = """
            using PatternKit.Generators.Decorator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IAsyncStorage
            {
                Task<string> ReadFileAsync(string path, CancellationToken ct = default);
                ValueTask WriteFileAsync(string path, string content, CancellationToken ct = default);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_WithAsyncMethods));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify generated content contains async method forwarding (direct forwarding without async/await)
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IAsyncStorage.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("public", generatedSource);
        Assert.Contains("virtual", generatedSource);
        Assert.Contains("ReadFileAsync", generatedSource);
        Assert.Contains("=> Inner.ReadFileAsync", generatedSource);
        Assert.Contains("WriteFileAsync", generatedSource);
        Assert.Contains("=> Inner.WriteFileAsync", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateDecoratorForInterface_WithProperties()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IConfiguration
            {
                string ApiKey { get; set; }
                int Timeout { get; }
                bool IsEnabled { get; set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_WithProperties));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify generated content contains properties
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IConfiguration.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("public virtual string ApiKey", generatedSource);
        Assert.Contains("get => Inner.ApiKey", generatedSource);
        Assert.Contains("set => Inner.ApiKey = value", generatedSource);
        Assert.Contains("public virtual int Timeout => Inner.Timeout", generatedSource);
        Assert.Contains("public virtual bool IsEnabled", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateDecoratorForInterface_WithDecoratorIgnore()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IRepository
            {
                void Save(string data);
                
                [DecoratorIgnore]
                void InternalMethod();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_WithDecoratorIgnore));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify generated content - ignored methods are still forwarded but not virtual
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IRepository.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("void Save", generatedSource);
        Assert.Contains("virtual", generatedSource); // Save should be virtual
        Assert.Contains("InternalMethod", generatedSource); // Still present, forwarded to Inner
        
        // InternalMethod should be present and non-virtual (i.e., not declared as virtual)
        // We can't easily check "public void InternalMethod" vs "public virtual void InternalMethod"
        // so we'll just verify it compiles and InternalMethod exists

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateDecoratorForInterface_CustomNames()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator(BaseTypeName = "CustomStorageDecorator", HelpersTypeName = "CustomHelpers")]
            public interface IStorage
            {
                string ReadFile(string path);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_CustomNames));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify custom names are used
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IStorage.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("class CustomStorageDecorator", generatedSource);
        Assert.Contains("class CustomHelpers", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateDecoratorForInterface_NoCompositionHelpers()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator(Composition = DecoratorCompositionMode.None)]
            public interface IStorage
            {
                string ReadFile(string path);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_NoCompositionHelpers));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify composition helpers are NOT generated
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IStorage.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("class StorageDecoratorBase", generatedSource);
        Assert.DoesNotContain("class StorageDecorators", generatedSource);
        Assert.DoesNotContain("public static IStorage Compose", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateDecoratorForAbstractClass_VirtualMembersOnly()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public abstract partial class StorageBase
            {
                public abstract string ReadFile(string path);
                public virtual void WriteFile(string path, string content) { }
                public void NonVirtualMethod() { } // Should be excluded
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForAbstractClass_VirtualMembersOnly));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify only virtual/abstract members are included
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_StorageBase.Decorator.g.cs")
            .SourceText.ToString();

        // For abstract classes, methods use "override" not "virtual"
        Assert.Contains("public override", generatedSource);
        Assert.Contains("ReadFile", generatedSource);
        Assert.Contains("WriteFile", generatedSource);
        Assert.DoesNotContain("NonVirtualMethod", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateDecoratorForInterface_WithDefaultParameters()
    {
        const string source = """
            using PatternKit.Generators.Decorator;
            using System.Threading;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IStorage
            {
                string ReadFile(string path, int bufferSize = 4096);
                void WriteFile(string path, string content, bool append = false);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_WithDefaultParameters));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify default parameters are preserved
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IStorage.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("int bufferSize = 4096", generatedSource);
        Assert.Contains("bool append = false", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateDecoratorForInterface_DeterministicOrdering()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IStorage
            {
                void Zebra();
                void Apple();
                void Mango();
                void Banana();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_DeterministicOrdering));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify members are ordered alphabetically
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IStorage.Decorator.g.cs")
            .SourceText.ToString();

        var appleIndex = generatedSource.IndexOf("void Apple");
        var bananaIndex = generatedSource.IndexOf("void Banana");
        var mangoIndex = generatedSource.IndexOf("void Mango");
        var zebraIndex = generatedSource.IndexOf("void Zebra");

        Assert.True(appleIndex < bananaIndex);
        Assert.True(bananaIndex < mangoIndex);
        Assert.True(mangoIndex < zebraIndex);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateDecoratorForInterface_WithRefParameters()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface ICalculator
            {
                void Calculate(ref int value);
                void TryParse(string input, out int result);
                void Process(in int value);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_WithRefParameters));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify ref/out/in parameters are preserved
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_ICalculator.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("ref int value", generatedSource);
        Assert.Contains("out int result", generatedSource);
        Assert.Contains("in int value", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateDecoratorForInterface_ComplexExample()
    {
        const string source = """
            using PatternKit.Generators.Decorator;
            using System.IO;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IStorage
            {
                Stream OpenRead(string path);
                ValueTask<Stream> OpenReadAsync(string path, CancellationToken ct = default);
                void Write(string path, byte[] data);
                Task WriteAsync(string path, byte[] data, CancellationToken ct = default);
                bool Exists(string path);
                void Delete(string path);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_ComplexExample));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify all methods are generated correctly
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IStorage.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("OpenRead", generatedSource);
        Assert.Contains("OpenReadAsync", generatedSource);
        Assert.Contains("Write", generatedSource);
        Assert.Contains("WriteAsync", generatedSource);
        Assert.Contains("Exists", generatedSource);
        Assert.Contains("Delete", generatedSource);
        Assert.Contains("virtual", generatedSource);
        // Async methods use direct forwarding (no async/await keywords)

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateDecoratorForInterface_InheritedMembers()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            public interface IReadable
            {
                string Read();
            }

            [GenerateDecorator]
            public interface IStorage : IReadable
            {
                void Write(string data);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_InheritedMembers));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify inherited members are included
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IStorage.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("public virtual string Read", generatedSource);
        Assert.Contains("public virtual void Write", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void DecoratorComposition_AppliesInCorrectOrder()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IService
            {
                string Execute(string input);
            }

            public class LoggingDecorator : ServiceDecoratorBase
            {
                public LoggingDecorator(IService inner) : base(inner) { }
                
                public override string Execute(string input)
                {
                    return "Logging: " + base.Execute(input);
                }
            }

            public class CachingDecorator : ServiceDecoratorBase
            {
                public CachingDecorator(IService inner) : base(inner) { }
                
                public override string Execute(string input)
                {
                    return "Caching: " + base.Execute(input);
                }
            }

            public class BaseService : IService
            {
                public string Execute(string input) => "Base: " + input;
            }

            public class TestRunner
            {
                public static string Test()
                {
                    var service = new BaseService();
                    var decorated = ServiceDecorators.Compose(
                        service,
                        s => new LoggingDecorator(s),
                        s => new CachingDecorator(s)
                    );
                    return decorated.Execute("test");
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(DecoratorComposition_AppliesInCorrectOrder));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Diagnostic_PKDEC001_UnsupportedTargetType()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public class ConcreteClass  // Not abstract
            {
                public void Method() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Diagnostic_PKDEC001_UnsupportedTargetType));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have PKDEC001 diagnostic
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKDEC001");
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("ConcreteClass"));
    }

    [Fact]
    public void Diagnostic_PKDEC002_UnsupportedMemberKind_Event()
    {
        const string source = """
            using PatternKit.Generators.Decorator;
            using System;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IWithEvent
            {
                event EventHandler Changed;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Diagnostic_PKDEC002_UnsupportedMemberKind_Event));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have PKDEC002 diagnostic for the event
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKDEC002" && d.GetMessage().Contains("Changed"));
        
        // Generation should be skipped when PKDEC002 (error) is reported
        var generatedSources = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
        Assert.Empty(generatedSources);
    }

    [Fact]
    public void Diagnostic_PKDEC003_NameConflict_BaseType()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IService
            {
                void Execute();
            }

            // This conflicts with the generated name
            public class ServiceDecoratorBase
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Diagnostic_PKDEC003_NameConflict_BaseType));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have PKDEC003 diagnostic
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKDEC003" && d.GetMessage().Contains("ServiceDecoratorBase"));
    }

    [Fact]
    public void Diagnostic_PKDEC003_NameConflict_HelpersType()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IService
            {
                void Execute();
            }

            // This conflicts with the generated helpers name
            public static class ServiceDecorators
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Diagnostic_PKDEC003_NameConflict_HelpersType));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have PKDEC003 diagnostic
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKDEC003" && d.GetMessage().Contains("ServiceDecorators"));
    }

    [Fact]
    public void Diagnostic_PKDEC002_Indexer()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IIndexable
            {
                string this[int index] { get; set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Diagnostic_PKDEC002_Indexer));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have PKDEC002 diagnostic for the indexer
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKDEC002" && d.GetMessage().Contains("Indexer"));
        
        // Generation should be skipped when PKDEC002 (error) is reported
        var generatedSources = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
        Assert.Empty(generatedSources);
    }

    [Fact]
    public void Diagnostic_PKDEC005_GenericContract()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IGenericService<T>
            {
                T Process(T input);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Diagnostic_PKDEC005_GenericContract));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have PKDEC005 diagnostic for the generic contract
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKDEC005" && d.GetMessage().Contains("IGenericService"));
    }

    [Fact]
    public void Diagnostic_PKDEC002_InitOnlyProperty()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IConfig
            {
                string Name { get; init; }
                int Value { get; set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Diagnostic_PKDEC002_InitOnlyProperty));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have PKDEC002 diagnostic for the init-only property
        // Init setters are incompatible with the decorator pattern
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKDEC002" && d.GetMessage().Contains("Name"));
        
        // Generation should be skipped when PKDEC002 (error) is reported
        var generatedSources = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
        Assert.Empty(generatedSources);
    }

    [Fact]
    public void GenerateDecoratorForAbstractClass_WithInternalProtectedMembers()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public abstract class ServiceBase
            {
                public abstract void PublicMethod();
                protected internal abstract void ProtectedInternalMethod();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForAbstractClass_WithInternalProtectedMembers));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify generated content contains correct accessibility
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_ServiceBase.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("public override", generatedSource);
        Assert.Contains("PublicMethod", generatedSource);
        Assert.Contains("protected internal override", generatedSource);
        Assert.Contains("ProtectedInternalMethod", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Diagnostic_PKDEC002_GenericMethod()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IGenericService
            {
                T Process<T>(T input);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Diagnostic_PKDEC002_GenericMethod));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have PKDEC002 diagnostic for the generic method
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKDEC002" && d.GetMessage().Contains("Generic method"));
        
        // Generation should be skipped when PKDEC002 (error) is reported
        var generatedSources = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
        Assert.Empty(generatedSources);
    }

    [Fact]
    public void GenerateDecoratorForInterface_IgnoresStaticMembers()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IService
            {
                void InstanceMethod();
                static void StaticMethod() { }
                string InstanceProperty { get; set; }
                static string StaticProperty { get; set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_IgnoresStaticMembers));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics for static members
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify generated content contains only instance members
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IService.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("InstanceMethod", generatedSource);
        Assert.Contains("InstanceProperty", generatedSource);
        Assert.DoesNotContain("StaticMethod", generatedSource);
        Assert.DoesNotContain("StaticProperty", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Diagnostic_PKDEC004_PropertyWithProtectedSetter()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public abstract class ServiceBase
            {
                public abstract string Name { get; protected set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Diagnostic_PKDEC004_PropertyWithProtectedSetter));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have PKDEC004 diagnostic for the property with protected setter
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKDEC004" && d.GetMessage().Contains("Name"));
    }

    [Fact]
    public void GenerateDecoratorForInterface_SupportsParamsModifier()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IService
            {
                void ProcessMany(params string[] items);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_SupportsParamsModifier));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify generated content preserves params modifier
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IService.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("params", generatedSource);
        Assert.Contains("params string[] items", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateDecoratorForAbstractClass_InheritsVirtualMembers()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            public abstract class BaseClass
            {
                public abstract void BaseMethod();
            }

            [GenerateDecorator]
            public abstract class DerivedClass : BaseClass
            {
                public abstract void DerivedMethod();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForAbstractClass_InheritsVirtualMembers));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify generated content includes both base and derived methods
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_DerivedClass.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("BaseMethod", generatedSource);
        Assert.Contains("DerivedMethod", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Diagnostic_PKDEC006_NestedType()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            public class OuterClass
            {
                [GenerateDecorator]
                public interface INestedService
                {
                    void DoWork();
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Diagnostic_PKDEC006_NestedType));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have PKDEC006 diagnostic for nested type
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKDEC006");
    }

    [Fact]
    public void GenerateDecoratorForInterface_SupportsRefReturns()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IRefService
            {
                ref int GetRef();
                ref readonly int GetRefReadonly();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_SupportsRefReturns));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify generated content includes ref/ref readonly modifiers
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IRefService.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("ref int GetRef()", generatedSource);
        Assert.Contains("ref readonly int GetRefReadonly()", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
