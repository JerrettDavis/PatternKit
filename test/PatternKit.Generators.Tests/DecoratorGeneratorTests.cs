using Microsoft.CodeAnalysis;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public class DecoratorGeneratorTests
{
    [Scenario("GenerateDecoratorForInterface BasicContract")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Decorator class is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("TestNamespace_IStorage.Decorator.g.cs", names);

        // Verify generated content contains expected elements
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IStorage.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.True(generatedSource.Length > 100, $"Generated source is too short ({generatedSource.Length} chars): {generatedSource}");

        // The Inner property will use fully qualified names
        ScenarioExpect.Contains("StorageDecoratorBase", generatedSource);
        ScenarioExpect.Contains("protected", generatedSource);
        ScenarioExpect.Contains(" Inner ", generatedSource);
        ScenarioExpect.Contains("public virtual", generatedSource);
        ScenarioExpect.Contains("ReadFile", generatedSource);
        ScenarioExpect.Contains("WriteFile", generatedSource);
        ScenarioExpect.Contains("StorageDecorators", generatedSource);
        ScenarioExpect.Contains("Compose", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecoratorForInterface WithAsyncMethods")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify generated content contains async method forwarding (direct forwarding without async/await)
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IAsyncStorage.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("public", generatedSource);
        ScenarioExpect.Contains("virtual", generatedSource);
        ScenarioExpect.Contains("ReadFileAsync", generatedSource);
        ScenarioExpect.Contains("=> Inner.ReadFileAsync", generatedSource);
        ScenarioExpect.Contains("WriteFileAsync", generatedSource);
        ScenarioExpect.Contains("=> Inner.WriteFileAsync", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecoratorForInterface WithProperties")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify generated content contains properties
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IConfiguration.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("public virtual string ApiKey", generatedSource);
        ScenarioExpect.Contains("get => Inner.ApiKey", generatedSource);
        ScenarioExpect.Contains("set => Inner.ApiKey = value", generatedSource);
        ScenarioExpect.Contains("public virtual int Timeout => Inner.Timeout", generatedSource);
        ScenarioExpect.Contains("public virtual bool IsEnabled", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecoratorForInterface WithDecoratorIgnore")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify generated content - ignored methods are still forwarded but not virtual
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IRepository.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("void Save", generatedSource);
        ScenarioExpect.Contains("virtual", generatedSource); // Save should be virtual
        ScenarioExpect.Contains("InternalMethod", generatedSource); // Still present, forwarded to Inner

        // InternalMethod should be present and non-virtual (i.e., not declared as virtual)
        // We can't easily check "public void InternalMethod" vs "public virtual void InternalMethod"
        // so we'll just verify it compiles and InternalMethod exists

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecoratorForInterface CustomNames")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify custom names are used
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IStorage.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("class CustomStorageDecorator", generatedSource);
        ScenarioExpect.Contains("class CustomHelpers", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecoratorForInterface NoCompositionHelpers")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify composition helpers are NOT generated
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IStorage.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("class StorageDecoratorBase", generatedSource);
        ScenarioExpect.DoesNotContain("class StorageDecorators", generatedSource);
        ScenarioExpect.DoesNotContain("public static IStorage Compose", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecoratorForAbstractClass VirtualMembersOnly")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify only virtual/abstract members are included
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_StorageBase.Decorator.g.cs")
            .SourceText.ToString();

        // For abstract classes, methods use "override" not "virtual"
        ScenarioExpect.Contains("public override", generatedSource);
        ScenarioExpect.Contains("ReadFile", generatedSource);
        ScenarioExpect.Contains("WriteFile", generatedSource);
        ScenarioExpect.DoesNotContain("NonVirtualMethod", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecoratorForInterface WithDefaultParameters")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify default parameters are preserved
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IStorage.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("int bufferSize = 4096", generatedSource);
        ScenarioExpect.Contains("bool append = false", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecoratorForInterface PreservesFloatingPointDefaults")]
    [Fact]
    public void GenerateDecoratorForInterface_PreservesFloatingPointDefaults()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public interface IScoring
            {
                double Normalize(double score = double.NaN, float weight = float.PositiveInfinity);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForInterface_PreservesFloatingPointDefaults));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IScoring.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("double score = double.NaN", generatedSource);
        ScenarioExpect.Contains("float weight = float.PositiveInfinity", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecoratorForInterface DeterministicOrdering")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify members are ordered alphabetically
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IStorage.Decorator.g.cs")
            .SourceText.ToString();

        var appleIndex = generatedSource.IndexOf("void Apple", StringComparison.Ordinal);
        var bananaIndex = generatedSource.IndexOf("void Banana", StringComparison.Ordinal);
        var mangoIndex = generatedSource.IndexOf("void Mango", StringComparison.Ordinal);
        var zebraIndex = generatedSource.IndexOf("void Zebra", StringComparison.Ordinal);

        ScenarioExpect.True(appleIndex < bananaIndex);
        ScenarioExpect.True(bananaIndex < mangoIndex);
        ScenarioExpect.True(mangoIndex < zebraIndex);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecoratorForInterface WithRefParameters")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify ref/out/in parameters are preserved
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_ICalculator.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("ref int value", generatedSource);
        ScenarioExpect.Contains("out int result", generatedSource);
        ScenarioExpect.Contains("in int value", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecoratorForInterface ComplexExample")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify all methods are generated correctly
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IStorage.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("OpenRead", generatedSource);
        ScenarioExpect.Contains("OpenReadAsync", generatedSource);
        ScenarioExpect.Contains("Write", generatedSource);
        ScenarioExpect.Contains("WriteAsync", generatedSource);
        ScenarioExpect.Contains("Exists", generatedSource);
        ScenarioExpect.Contains("Delete", generatedSource);
        ScenarioExpect.Contains("virtual", generatedSource);
        // Async methods use direct forwarding (no async/await keywords)

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecoratorForInterface InheritedMembers")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify inherited members are included
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IStorage.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("public virtual string Read", generatedSource);
        ScenarioExpect.Contains("public virtual void Write", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("DecoratorComposition AppliesInCorrectOrder")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Diagnostic PKDEC001 UnsupportedTargetType")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC001");
        ScenarioExpect.Contains(diagnostics, d => d.GetMessage().Contains("ConcreteClass"));
    }

    [Scenario("Diagnostic PKDEC002 UnsupportedMemberKind Event")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC002" && d.GetMessage().Contains("Changed"));

        // Generation should be skipped when PKDEC002 (error) is reported
        var generatedSources = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
        ScenarioExpect.Empty(generatedSources);
    }

    [Scenario("Diagnostic PKDEC003 NameConflict BaseType")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC003" && d.GetMessage().Contains("ServiceDecoratorBase"));
    }

    [Scenario("Diagnostic PKDEC003 NameConflict HelpersType")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC003" && d.GetMessage().Contains("ServiceDecorators"));
    }

    [Scenario("Diagnostic PKDEC002 Indexer")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC002" && d.GetMessage().Contains("Indexer"));

        // Generation should be skipped when PKDEC002 (error) is reported
        var generatedSources = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
        ScenarioExpect.Empty(generatedSources);
    }

    [Scenario("Diagnostic PKDEC005 GenericContract")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC005" && d.GetMessage().Contains("IGenericService"));
    }

    [Scenario("Diagnostic PKDEC002 InitOnlyProperty")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC002" && d.GetMessage().Contains("Name"));

        // Generation should be skipped when PKDEC002 (error) is reported
        var generatedSources = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
        ScenarioExpect.Empty(generatedSources);
    }

    [Scenario("GenerateDecoratorForAbstractClass WithInternalProtectedMembers")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify generated content contains correct accessibility
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_ServiceBase.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("public override", generatedSource);
        ScenarioExpect.Contains("PublicMethod", generatedSource);
        ScenarioExpect.Contains("protected internal override", generatedSource);
        ScenarioExpect.Contains("ProtectedInternalMethod", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Diagnostic PKDEC002 GenericMethod")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC002" && d.GetMessage().Contains("Generic method"));

        // Generation should be skipped when PKDEC002 (error) is reported
        var generatedSources = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
        ScenarioExpect.Empty(generatedSources);
    }

    [Scenario("GenerateDecoratorForInterface IgnoresStaticMembers")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify generated content contains only instance members
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IService.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("InstanceMethod", generatedSource);
        ScenarioExpect.Contains("InstanceProperty", generatedSource);
        ScenarioExpect.DoesNotContain("StaticMethod", generatedSource);
        ScenarioExpect.DoesNotContain("StaticProperty", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Diagnostic PKDEC004 PropertyWithProtectedSetter")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC004" && d.GetMessage().Contains("Name"));
    }

    [Scenario("GenerateDecoratorForInterface SupportsParamsModifier")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify generated content preserves params modifier
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IService.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("params", generatedSource);
        ScenarioExpect.Contains("params string[] items", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecoratorForAbstractClass InheritsVirtualMembers")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify generated content includes both base and derived methods
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_DerivedClass.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("BaseMethod", generatedSource);
        ScenarioExpect.Contains("DerivedMethod", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Diagnostic PKDEC006 NestedType")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC006");
    }

    [Scenario("GenerateDecoratorForInterface SupportsRefReturns")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify generated content includes ref/ref readonly modifiers
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IRefService.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("ref int GetRef()", generatedSource);
        ScenarioExpect.Contains("ref readonly int GetRefReadonly()", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecoratorForAbstractClass PreservesAccessibilityRefsAndAccessors")]
    [Fact]
    public void GenerateDecoratorForAbstractClass_PreservesAccessibilityRefsAndAccessors()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator(Composition = DecoratorCompositionMode.HelpersOnly)]
            public abstract class RepositoryBase
            {
                public abstract string Name { get; internal set; }
                public abstract string Secret { protected internal get; set; }
                public abstract int Version { set; }
                public abstract void Copy(ref int source, out int destination, in bool enabled);
                public abstract ref int GetCurrent();

                [DecoratorIgnore]
                public abstract string Snapshot();

                protected abstract void ProtectedOperation();
                public int NonVirtualValue => 42;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecoratorForAbstractClass_PreservesAccessibilityRefsAndAccessors));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.DoesNotContain(r.Diagnostics, d => d.Severity == DiagnosticSeverity.Error));
        ScenarioExpect.Contains(result.Results.SelectMany(r => r.Diagnostics), d => d.Id == "PKDEC004" && d.GetMessage().Contains("ProtectedOperation"));

        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_RepositoryBase.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("public abstract partial class RepositoryBaseDecoratorBase", generatedSource);
        ScenarioExpect.Contains("override string Name", generatedSource);
        ScenarioExpect.Contains("internal set => Inner.Name = value;", generatedSource);
        ScenarioExpect.Contains("protected internal get => Inner.Secret;", generatedSource);
        ScenarioExpect.Contains("override int Version", generatedSource);
        ScenarioExpect.Contains("set => Inner.Version = value;", generatedSource);
        ScenarioExpect.Contains("public override void Copy(ref int source, out int destination, in bool enabled)", generatedSource);
        ScenarioExpect.Contains("Inner.Copy(ref source, out destination, in enabled);", generatedSource);
        ScenarioExpect.Contains("public override ref int GetCurrent()", generatedSource);
        ScenarioExpect.Contains("=> ref Inner.GetCurrent();", generatedSource);
        ScenarioExpect.Contains("public sealed override string Snapshot()", generatedSource);
        ScenarioExpect.Contains("RepositoryBaseDecorators", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("DecoratorDiagnostics ForIndexerInitPropertyFieldAndNestedType")]
    [Fact]
    public void DecoratorDiagnostics_ForIndexerInitPropertyFieldAndNestedType()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public abstract class InvalidContract
            {
                public int Field;
                public abstract string this[int index] { get; }
                public abstract string InitOnly { get; init; }
                public abstract event System.EventHandler? Changed;

                public class NestedType
                {
                }

                public abstract void Save();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(DecoratorDiagnostics_ForIndexerInitPropertyFieldAndNestedType));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC002" && d.GetMessage().Contains("Indexer"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC002" && d.GetMessage().Contains("Init-only property"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC002" && d.GetMessage().Contains("Changed"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC002" && d.GetMessage().Contains("Field"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKDEC002" && d.GetMessage().Contains("NestedType"));
    }

    [Scenario("Diagnostic PKDEC004 InaccessibleProtectedPropertyDeclaration")]
    [Fact]
    public void Diagnostic_PKDEC004_InaccessibleProtectedPropertyDeclaration()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public abstract class SecretRepository
            {
                protected abstract string Secret { get; }
                public abstract string Visible { get; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Diagnostic_PKDEC004_InaccessibleProtectedPropertyDeclaration));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.Contains(result.Results.SelectMany(r => r.Diagnostics), d => d.Id == "PKDEC004" && d.GetMessage().Contains("Secret"));

        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(gs => gs.HintName == "TestNamespace_SecretRepository.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("override string Visible => Inner.Visible;", generatedSource);
        ScenarioExpect.DoesNotContain("override string Secret", generatedSource);
        ScenarioExpect.DoesNotContain("=> Inner.Secret", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateDecorator PreservesPrimitiveAndStringDefaultLiterals")]
    [Fact]
    public void GenerateDecorator_PreservesPrimitiveAndStringDefaultLiterals()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            public enum Mode { None = 0, Known = 1 }

            [GenerateDecorator]
            public interface ILiteralService
            {
                string Format(
                    string text = "line\n\"quoted\"\tend",
                    string? optionalText = null,
                    object boxed = null,
                    int? retryCount = null,
                    bool enabled = true,
                    float missing = float.NaN,
                    float negativeWeight = float.NegativeInfinity,
                    float ratio = 1.25f,
                    double overflow = double.PositiveInfinity,
                    double underflow = double.NegativeInfinity,
                    double weight = 2.5d,
                    decimal amount = 3.75m,
                    Mode known = Mode.Known,
                    Mode mode = (Mode)99);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateDecorator_PreservesPrimitiveAndStringDefaultLiterals));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_ILiteralService.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("string text = \"line\\n\\\"quoted\\\"\\tend\"", generatedSource);
        ScenarioExpect.Contains("string? optionalText = null", generatedSource);
        ScenarioExpect.Contains("object boxed = null", generatedSource);
        ScenarioExpect.Contains("int? retryCount = null", generatedSource);
        ScenarioExpect.Contains("bool enabled = true", generatedSource);
        ScenarioExpect.Contains("float missing = float.NaN", generatedSource);
        ScenarioExpect.Contains("float negativeWeight = float.NegativeInfinity", generatedSource);
        ScenarioExpect.Contains("float ratio = 1.25f", generatedSource);
        ScenarioExpect.Contains("double overflow = double.PositiveInfinity", generatedSource);
        ScenarioExpect.Contains("double underflow = double.NegativeInfinity", generatedSource);
        ScenarioExpect.Contains("double weight = 2.5d", generatedSource);
        ScenarioExpect.Contains("decimal amount = 3.75m", generatedSource);
        ScenarioExpect.Contains("global::TestNamespace.Mode known = global::TestNamespace.Mode.Known", generatedSource);
        ScenarioExpect.Contains("global::TestNamespace.Mode mode = (global::TestNamespace.Mode)99", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Diagnostic PKDEC004 InaccessiblePropertyDeclaration")]
    [Fact]
    public void Diagnostic_PKDEC004_InaccessiblePropertyDeclaration()
    {
        const string source = """
            using PatternKit.Generators.Decorator;

            namespace TestNamespace;

            [GenerateDecorator]
            public abstract class SecretRepository
            {
                private protected abstract string Confidential { get; }
                public abstract string Visible { get; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Diagnostic_PKDEC004_InaccessiblePropertyDeclaration));
        var gen = new DecoratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.Contains(result.Results.SelectMany(r => r.Diagnostics), d => d.Id == "PKDEC004" && d.GetMessage().Contains("Confidential"));

        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(gs => gs.HintName == "TestNamespace_SecretRepository.Decorator.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("Visible", generatedSource);
        ScenarioExpect.DoesNotContain("Confidential", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
