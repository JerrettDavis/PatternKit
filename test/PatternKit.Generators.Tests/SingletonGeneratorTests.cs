using Microsoft.CodeAnalysis;
using PatternKit.Generators.Singleton;

namespace PatternKit.Generators.Tests;

public class SingletonGeneratorTests
{
    [Fact]
    public void GenerateEagerSingleton()
    {
        const string source = """
            using PatternKit.Generators.Singleton;

            namespace TestNamespace;

            [Singleton]
            public partial class AppClock
            {
                private AppClock() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateEagerSingleton));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Singleton file is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("TestNamespace.AppClock.Singleton.g.cs", names);

        // Generated code contains expected shape
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace.AppClock.Singleton.g.cs")
            .SourceText.ToString();

        Assert.Contains("private static readonly AppClock _instance = new AppClock();", generatedSource);
        Assert.Contains("public static AppClock Instance => _instance;", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateLazyThreadSafeSingleton()
    {
        const string source = """
            using PatternKit.Generators.Singleton;

            namespace TestNamespace;

            [Singleton(Mode = SingletonMode.Lazy, Threading = SingletonThreading.ThreadSafe)]
            public partial class ConfigManager
            {
                private ConfigManager() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateLazyThreadSafeSingleton));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Singleton file is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("TestNamespace.ConfigManager.Singleton.g.cs", names);

        // Generated code contains Lazy<T> pattern
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace.ConfigManager.Singleton.g.cs")
            .SourceText.ToString();

        Assert.Contains("System.Lazy<ConfigManager>", generatedSource);
        Assert.Contains("_lazyInstance.Value", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateLazySingleThreadedSingleton()
    {
        const string source = """
            using PatternKit.Generators.Singleton;

            namespace TestNamespace;

            [Singleton(Mode = SingletonMode.Lazy, Threading = SingletonThreading.SingleThreadedFast)]
            public partial class FastCache
            {
                private FastCache() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateLazySingleThreadedSingleton));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Generated code contains non-thread-safe pattern
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace.FastCache.Singleton.g.cs")
            .SourceText.ToString();

        Assert.Contains("_instance ??=", generatedSource);
        Assert.Contains("not thread-safe", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateSingletonWithCustomFactory()
    {
        const string source = """
            using PatternKit.Generators.Singleton;

            namespace TestNamespace;

            [Singleton(Mode = SingletonMode.Lazy)]
            public partial class ServiceLocator
            {
                private ServiceLocator(string config) { }

                [SingletonFactory]
                private static ServiceLocator Create() => new ServiceLocator("default.config");
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateSingletonWithCustomFactory));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Generated code uses factory method
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace.ServiceLocator.Singleton.g.cs")
            .SourceText.ToString();

        Assert.Contains("Create()", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateSingletonForRecordClass()
    {
        const string source = """
            using PatternKit.Generators.Singleton;

            namespace TestNamespace;

            [Singleton]
            public partial record class AppSettings
            {
                private AppSettings() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateSingletonForRecordClass));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Singleton file is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("TestNamespace.AppSettings.Singleton.g.cs", names);

        // Uses record class keyword
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace.AppSettings.Singleton.g.cs")
            .SourceText.ToString();

        Assert.Contains("partial record class AppSettings", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateSingletonWithCustomPropertyName()
    {
        const string source = """
            using PatternKit.Generators.Singleton;

            namespace TestNamespace;

            [Singleton(InstancePropertyName = "Default")]
            public partial class Logger
            {
                private Logger() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateSingletonWithCustomPropertyName));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Generated code uses custom property name
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace.Logger.Singleton.g.cs")
            .SourceText.ToString();

        Assert.Contains("public static Logger Default =>", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void ErrorWhenNotPartial()
    {
        const string source = """
            using PatternKit.Generators.Singleton;

            namespace TestNamespace;

            [Singleton]
            public class NotPartialSingleton
            {
                private NotPartialSingleton() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenNotPartial));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKSNG001 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKSNG001");
    }

    [Fact]
    public void ErrorWhenStruct()
    {
        const string source = """
            using PatternKit.Generators.Singleton;

            namespace TestNamespace;

            [Singleton]
            public partial struct StructSingleton
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenStruct));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKSNG002 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKSNG002");
    }

    [Fact]
    public void ErrorWhenNoConstructorOrFactory()
    {
        const string source = """
            using PatternKit.Generators.Singleton;

            namespace TestNamespace;

            [Singleton]
            public partial class NoWayToConstruct
            {
                private NoWayToConstruct(string required) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenNoConstructorOrFactory));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKSNG003 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKSNG003");
    }

    [Fact]
    public void ErrorWhenMultipleFactories()
    {
        const string source = """
            using PatternKit.Generators.Singleton;

            namespace TestNamespace;

            [Singleton]
            public partial class TwoFactories
            {
                private TwoFactories() { }

                [SingletonFactory]
                private static TwoFactories Create1() => new TwoFactories();

                [SingletonFactory]
                private static TwoFactories Create2() => new TwoFactories();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenMultipleFactories));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKSNG004 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKSNG004");
    }

    [Fact]
    public void WarnWhenPublicConstructor()
    {
        const string source = """
            using PatternKit.Generators.Singleton;

            namespace TestNamespace;

            [Singleton]
            public partial class PublicCtorSingleton
            {
                public PublicCtorSingleton() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(WarnWhenPublicConstructor));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // PKSNG005 diagnostic is reported (warning)
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKSNG005" && d.Severity == DiagnosticSeverity.Warning);

        // Still generates code despite warning
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("TestNamespace.PublicCtorSingleton.Singleton.g.cs", names);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void ErrorWhenNameConflict()
    {
        const string source = """
            using PatternKit.Generators.Singleton;

            namespace TestNamespace;

            [Singleton]
            public partial class HasExistingInstance
            {
                private HasExistingInstance() { }

                public static int Instance => 42;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenNameConflict));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKSNG006 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKSNG006");
    }

    [Fact]
    public void GenerateSingletonInGlobalNamespace()
    {
        const string source = """
            using PatternKit.Generators.Singleton;

            [Singleton]
            public partial class GlobalSingleton
            {
                private GlobalSingleton() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateSingletonInGlobalNamespace));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Generated code has no namespace
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "GlobalSingleton.Singleton.g.cs")
            .SourceText.ToString();

        Assert.DoesNotContain("namespace", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void EagerSingleton_Compiles()
    {
        const string source = """
            using PatternKit.Generators.Singleton;
            using System;

            namespace TestNamespace;

            [Singleton]
            public partial class Counter
            {
                public int Value { get; set; }
                private Counter() { Value = 42; }
            }

            public static class TestRunner
            {
                public static bool Test()
                {
                    var a = Counter.Instance;
                    var b = Counter.Instance;
                    return object.ReferenceEquals(a, b) && a.Value == 42;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(EagerSingleton_Compiles));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void LazySingleton_Compiles()
    {
        const string source = """
            using PatternKit.Generators.Singleton;
            using System;

            namespace TestNamespace;

            [Singleton(Mode = SingletonMode.Lazy)]
            public partial class LazyCounter
            {
                public int Value { get; set; }
                private LazyCounter() { Value = 99; }
            }

            public static class TestRunner
            {
                public static bool Test()
                {
                    var a = LazyCounter.Instance;
                    var b = LazyCounter.Instance;
                    return object.ReferenceEquals(a, b) && a.Value == 99;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(LazySingleton_Compiles));
        var gen = new SingletonGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
