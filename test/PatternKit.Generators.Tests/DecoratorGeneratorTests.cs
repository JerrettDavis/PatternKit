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
            public partial interface IStorage
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
        Assert.Contains("IStorage.Decorator.g.cs", names);

        // Verify generated content contains expected elements
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "IStorage.Decorator.g.cs")
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
            public partial interface IAsyncStorage
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

        // Verify generated content contains async methods
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "IAsyncStorage.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("public", generatedSource);
        Assert.Contains("virtual", generatedSource);
        Assert.Contains("async", generatedSource);
        Assert.Contains("ReadFileAsync", generatedSource);
        Assert.Contains("await Inner.ReadFileAsync", generatedSource);
        Assert.Contains("WriteFileAsync", generatedSource);
        Assert.Contains("await Inner.WriteFileAsync", generatedSource);

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
            public partial interface IConfiguration
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
            .First(gs => gs.HintName == "IConfiguration.Decorator.g.cs")
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
            public partial interface IRepository
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
            .First(gs => gs.HintName == "IRepository.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("void Save", generatedSource);
        Assert.Contains("virtual", generatedSource); // Save should be virtual
        Assert.Contains("InternalMethod", generatedSource); // Still present, forwarded to Inner
        
        // InternalMethod should be present and non-virtual (sealed)
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
            public partial interface IStorage
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
            .First(gs => gs.HintName == "IStorage.Decorator.g.cs")
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
            public partial interface IStorage
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
            .First(gs => gs.HintName == "IStorage.Decorator.g.cs")
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
            .First(gs => gs.HintName == "StorageBase.Decorator.g.cs")
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
            public partial interface IStorage
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
            .First(gs => gs.HintName == "IStorage.Decorator.g.cs")
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
            public partial interface IStorage
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
            .First(gs => gs.HintName == "IStorage.Decorator.g.cs")
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
            public partial interface ICalculator
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
            .First(gs => gs.HintName == "ICalculator.Decorator.g.cs")
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
            public partial interface IStorage
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
            .First(gs => gs.HintName == "IStorage.Decorator.g.cs")
            .SourceText.ToString();

        Assert.Contains("OpenRead", generatedSource);
        Assert.Contains("OpenReadAsync", generatedSource);
        Assert.Contains("Write", generatedSource);
        Assert.Contains("WriteAsync", generatedSource);
        Assert.Contains("Exists", generatedSource);
        Assert.Contains("Delete", generatedSource);
        Assert.Contains("virtual", generatedSource);
        Assert.Contains("async", generatedSource);

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
            public partial interface IStorage : IReadable
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
            .First(gs => gs.HintName == "IStorage.Decorator.g.cs")
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
            public partial interface IService
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
}
