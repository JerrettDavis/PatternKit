using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

/// <summary>
/// Comprehensive tests for the Singleton pattern generator.
/// </summary>
public class SingletonGeneratorTests
{
    #region Basic Eager Singleton

    [Fact]
    public void Generates_Eager_Singleton_Without_Diagnostics()
    {
        var source = """
            using PatternKit.Generators.Singleton;

            namespace TestNs;

            [Singleton]
            public partial class AppClock
            {
                private AppClock() { }
                public System.DateTime Now => System.DateTime.UtcNow;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Eager_Singleton_Without_Diagnostics));
        _ = RoslynTestHelpers.Run(comp, new SingletonGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("AppClock.Singleton.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Eager_Singleton_Generates_Static_Property_With_Initializer()
    {
        var source = """
            using PatternKit.Generators.Singleton;

            namespace TestNs;

            [Singleton]
            public partial class AppClock
            {
                private AppClock() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Eager_Singleton_Generates_Static_Property_With_Initializer));
        _ = RoslynTestHelpers.Run(comp, new SingletonGenerator(), out var run, out var updated);

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("AppClock"))
            .SourceText.ToString();

        Assert.Contains("public static AppClock Instance { get; } = new AppClock();", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Lazy Singleton

    [Fact]
    public void Generates_Lazy_ThreadSafe_Singleton()
    {
        var source = """
            using PatternKit.Generators.Singleton;

            namespace TestNs;

            [Singleton(Mode = SingletonMode.Lazy)]
            public partial class ExpensiveService
            {
                private ExpensiveService() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Lazy_ThreadSafe_Singleton));
        _ = RoslynTestHelpers.Run(comp, new SingletonGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ExpensiveService"))
            .SourceText.ToString();

        Assert.Contains("System.Lazy<ExpensiveService>", generatedSource);
        Assert.Contains("LazyThreadSafetyMode.ExecutionAndPublication", generatedSource);
        Assert.Contains("public static ExpensiveService Instance => _instance.Value;", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Lazy_SingleThreaded_Singleton()
    {
        var source = """
            using PatternKit.Generators.Singleton;

            namespace TestNs;

            [Singleton(Mode = SingletonMode.Lazy, Threading = SingletonThreading.SingleThreadedFast)]
            public partial class FastService
            {
                private FastService() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Lazy_SingleThreaded_Singleton));
        _ = RoslynTestHelpers.Run(comp, new SingletonGenerator(), out var run, out var updated);

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("FastService"))
            .SourceText.ToString();

        Assert.Contains("LazyThreadSafetyMode.None", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Factory Method

    [Fact]
    public void Generates_Singleton_With_Factory_Method()
    {
        var source = """
            using PatternKit.Generators.Singleton;

            namespace TestNs;

            [Singleton]
            public partial class ConfigService
            {
                private ConfigService() { }

                [SingletonFactory]
                private static ConfigService CreateInstance()
                {
                    return new ConfigService();
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Singleton_With_Factory_Method));
        _ = RoslynTestHelpers.Run(comp, new SingletonGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ConfigService"))
            .SourceText.ToString();

        Assert.Contains("CreateInstance()", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Lazy_Singleton_With_Factory_Method()
    {
        var source = """
            using PatternKit.Generators.Singleton;

            namespace TestNs;

            [Singleton(Mode = SingletonMode.Lazy)]
            public partial class ConfigService
            {
                private ConfigService() { }

                [SingletonFactory]
                private static ConfigService CreateInstance()
                {
                    return new ConfigService();
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Lazy_Singleton_With_Factory_Method));
        _ = RoslynTestHelpers.Run(comp, new SingletonGenerator(), out var run, out var updated);

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ConfigService"))
            .SourceText.ToString();

        Assert.Contains("() => CreateInstance()", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Custom Property Name

    [Fact]
    public void Generates_Singleton_With_Custom_Property_Name()
    {
        var source = """
            using PatternKit.Generators.Singleton;

            namespace TestNs;

            [Singleton(InstancePropertyName = "Default")]
            public partial class Logger
            {
                private Logger() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Singleton_With_Custom_Property_Name));
        _ = RoslynTestHelpers.Run(comp, new SingletonGenerator(), out var run, out var updated);

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Logger"))
            .SourceText.ToString();

        Assert.Contains("public static Logger Default { get; }", generatedSource);
        Assert.DoesNotContain("Instance", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Diagnostic Tests

    [Fact]
    public void Reports_Error_When_Type_Not_Partial()
    {
        var source = """
            using PatternKit.Generators.Singleton;

            namespace TestNs;

            [Singleton]
            public class NotPartial
            {
                private NotPartial() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Type_Not_Partial));
        _ = RoslynTestHelpers.Run(comp, new SingletonGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKSNG001");
    }

    [Fact]
    public void Reports_Error_When_Not_Class()
    {
        var source = """
            using PatternKit.Generators.Singleton;

            namespace TestNs;

            [Singleton]
            public partial struct NotAClass
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Not_Class));
        _ = RoslynTestHelpers.Run(comp, new SingletonGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKSNG002");
    }

    [Fact]
    public void Reports_Error_When_No_Parameterless_Ctor_Or_Factory()
    {
        var source = """
            using PatternKit.Generators.Singleton;

            namespace TestNs;

            [Singleton]
            public partial class NoDefaultCtor
            {
                private NoDefaultCtor(string name) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_No_Parameterless_Ctor_Or_Factory));
        _ = RoslynTestHelpers.Run(comp, new SingletonGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKSNG003");
    }

    [Fact]
    public void Reports_Error_When_Multiple_Factories()
    {
        var source = """
            using PatternKit.Generators.Singleton;

            namespace TestNs;

            [Singleton]
            public partial class MultiFactory
            {
                private MultiFactory() { }

                [SingletonFactory]
                private static MultiFactory Create1() => new();

                [SingletonFactory]
                private static MultiFactory Create2() => new();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Multiple_Factories));
        _ = RoslynTestHelpers.Run(comp, new SingletonGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKSNG004");
    }

    [Fact]
    public void Reports_Warning_When_Public_Constructor()
    {
        var source = """
            using PatternKit.Generators.Singleton;

            namespace TestNs;

            [Singleton]
            public partial class PublicCtor
            {
                public PublicCtor() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Warning_When_Public_Constructor));
        _ = RoslynTestHelpers.Run(comp, new SingletonGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKSNG005");
    }

    [Fact]
    public void Reports_Error_When_Name_Conflicts()
    {
        var source = """
            using PatternKit.Generators.Singleton;

            namespace TestNs;

            [Singleton]
            public partial class HasInstance
            {
                private HasInstance() { }
                public static HasInstance Instance { get; } = new();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Name_Conflicts));
        _ = RoslynTestHelpers.Run(comp, new SingletonGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKSNG006");
    }

    #endregion

    #region Namespace Tests

    [Fact]
    public void Generates_Singleton_In_Global_Namespace()
    {
        var source = """
            using PatternKit.Generators.Singleton;

            [Singleton]
            public partial class GlobalSingleton
            {
                private GlobalSingleton() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Singleton_In_Global_Namespace));
        _ = RoslynTestHelpers.Run(comp, new SingletonGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("GlobalSingleton"))
            .SourceText.ToString();

        Assert.DoesNotContain("namespace", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion
}
