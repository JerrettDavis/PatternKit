using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

/// <summary>
/// Comprehensive tests for the Adapter pattern generator.
/// </summary>
public class AdapterGeneratorTests
{
    #region Basic Adapter Tests

    [Fact]
    public void Generates_Adapter_Without_Diagnostics()
    {
        var source = """
            using PatternKit.Generators.Adapter;

            namespace TestNs;

            public interface ITarget
            {
                string Process(string input);
            }

            public class LegacyService
            {
                public string LegacyProcess(string data) => $"legacy:{data}";
            }

            [GenerateAdapter(Target = typeof(ITarget), Adaptee = typeof(LegacyService))]
            public static partial class LegacyAdapterHost
            {
                [AdapterMap(TargetMember = "Process")]
                public static string Process(LegacyService adaptee, string input)
                    => adaptee.LegacyProcess(input);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Adapter_Without_Diagnostics));
        _ = RoslynTestHelpers.Run(comp, new AdapterGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("LegacyServiceToTargetAdapter.Adapter.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generated_Adapter_Implements_Target_Interface()
    {
        var source = """
            using PatternKit.Generators.Adapter;

            namespace TestNs;

            public interface ITarget
            {
                string Process(string input);
            }

            public class LegacyService
            {
                public string LegacyProcess(string data) => $"legacy:{data}";
            }

            [GenerateAdapter(Target = typeof(ITarget), Adaptee = typeof(LegacyService))]
            public static partial class LegacyAdapterHost
            {
                [AdapterMap(TargetMember = "Process")]
                public static string Process(LegacyService adaptee, string input)
                    => adaptee.LegacyProcess(input);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generated_Adapter_Implements_Target_Interface));
        _ = RoslynTestHelpers.Run(comp, new AdapterGenerator(), out var run, out var updated);

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        Assert.Contains("public sealed class LegacyServiceToTargetAdapter", generatedSource);
        Assert.Contains("ITarget", generatedSource);
        Assert.Contains("_adaptee", generatedSource);
        Assert.Contains("Process(", generatedSource);
        Assert.Contains("LegacyAdapterHost.Process(_adaptee", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Custom Adapter Name

    [Fact]
    public void Generates_Adapter_With_Custom_Name()
    {
        var source = """
            using PatternKit.Generators.Adapter;

            namespace TestNs;

            public interface ITarget
            {
                string Process(string input);
            }

            public class LegacyService
            {
                public string LegacyProcess(string data) => $"legacy:{data}";
            }

            [GenerateAdapter(Target = typeof(ITarget), Adaptee = typeof(LegacyService), AdapterTypeName = "MyCustomAdapter")]
            public static partial class LegacyAdapterHost
            {
                [AdapterMap(TargetMember = "Process")]
                public static string Process(LegacyService adaptee, string input)
                    => adaptee.LegacyProcess(input);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Adapter_With_Custom_Name));
        _ = RoslynTestHelpers.Run(comp, new AdapterGenerator(), out var run, out var updated);

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("MyCustomAdapter.Adapter.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Name-Based Matching

    [Fact]
    public void Generates_Adapter_With_Name_Based_Matching()
    {
        var source = """
            using PatternKit.Generators.Adapter;

            namespace TestNs;

            public interface ITarget
            {
                string DoWork(int value);
            }

            public class OldSystem
            {
                public string OldDoWork(int v) => $"old:{v}";
            }

            [GenerateAdapter(Target = typeof(ITarget), Adaptee = typeof(OldSystem))]
            public static partial class OldAdapterHost
            {
                [AdapterMap]
                public static string DoWork(OldSystem adaptee, int value)
                    => adaptee.OldDoWork(value);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Adapter_With_Name_Based_Matching));
        _ = RoslynTestHelpers.Run(comp, new AdapterGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region ThrowingStub Policy

    [Fact]
    public void Generates_ThrowingStub_For_Unmapped_Members()
    {
        var source = """
            using PatternKit.Generators.Adapter;

            namespace TestNs;

            public interface ITarget
            {
                string Process(string input);
                void Cleanup();
            }

            public class LegacyService
            {
                public string LegacyProcess(string data) => $"legacy:{data}";
            }

            [GenerateAdapter(Target = typeof(ITarget), Adaptee = typeof(LegacyService), MissingMap = AdapterMissingMapPolicy.ThrowingStub)]
            public static partial class StubAdapterHost
            {
                [AdapterMap(TargetMember = "Process")]
                public static string Process(LegacyService adaptee, string input)
                    => adaptee.LegacyProcess(input);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_ThrowingStub_For_Unmapped_Members));
        _ = RoslynTestHelpers.Run(comp, new AdapterGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        Assert.Contains("throw new System.NotImplementedException", generatedSource);
        Assert.Contains("Cleanup", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Void Return Type

    [Fact]
    public void Generates_Adapter_For_Void_Methods()
    {
        var source = """
            using PatternKit.Generators.Adapter;

            namespace TestNs;

            public interface ITarget
            {
                void Execute(string command);
            }

            public class Shell
            {
                public void RunCommand(string cmd) { }
            }

            [GenerateAdapter(Target = typeof(ITarget), Adaptee = typeof(Shell))]
            public static partial class ShellAdapterHost
            {
                [AdapterMap(TargetMember = "Execute")]
                public static void Execute(Shell adaptee, string command)
                    => adaptee.RunCommand(command);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Adapter_For_Void_Methods));
        _ = RoslynTestHelpers.Run(comp, new AdapterGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        Assert.Contains("public void Execute(", generatedSource);
        // Should NOT have "return" for void methods
        Assert.DoesNotContain("return global::TestNs.ShellAdapterHost.Execute", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Diagnostic Tests

    [Fact]
    public void Reports_Error_When_Host_Not_Static_Partial()
    {
        var source = """
            using PatternKit.Generators.Adapter;

            namespace TestNs;

            public interface ITarget
            {
                string Process(string input);
            }

            public class Adaptee { }

            [GenerateAdapter(Target = typeof(ITarget), Adaptee = typeof(Adaptee))]
            public class NotStaticPartial
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Host_Not_Static_Partial));
        _ = RoslynTestHelpers.Run(comp, new AdapterGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKADP001");
    }

    [Fact]
    public void Reports_Error_When_Target_Not_Interface()
    {
        var source = """
            using PatternKit.Generators.Adapter;

            namespace TestNs;

            public class ConcreteTarget
            {
                public string Process(string input) => input;
            }

            public class Adaptee { }

            [GenerateAdapter(Target = typeof(ConcreteTarget), Adaptee = typeof(Adaptee))]
            public static partial class BadTargetHost
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Target_Not_Interface));
        _ = RoslynTestHelpers.Run(comp, new AdapterGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKADP002");
    }

    [Fact]
    public void Reports_Error_When_Missing_Mapping()
    {
        var source = """
            using PatternKit.Generators.Adapter;

            namespace TestNs;

            public interface ITarget
            {
                string Process(string input);
                void Cleanup();
            }

            public class LegacyService { }

            [GenerateAdapter(Target = typeof(ITarget), Adaptee = typeof(LegacyService))]
            public static partial class MissingMapHost
            {
                [AdapterMap(TargetMember = "Process")]
                public static string Process(LegacyService adaptee, string input) => input;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Missing_Mapping));
        _ = RoslynTestHelpers.Run(comp, new AdapterGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKADP003");
    }

    [Fact]
    public void Reports_Error_When_Duplicate_Mapping()
    {
        var source = """
            using PatternKit.Generators.Adapter;

            namespace TestNs;

            public interface ITarget
            {
                string Process(string input);
            }

            public class LegacyService { }

            [GenerateAdapter(Target = typeof(ITarget), Adaptee = typeof(LegacyService))]
            public static partial class DupMapHost
            {
                [AdapterMap(TargetMember = "Process")]
                public static string Process1(LegacyService adaptee, string input) => input;

                [AdapterMap(TargetMember = "Process")]
                public static string Process2(LegacyService adaptee, string input) => input;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Duplicate_Mapping));
        _ = RoslynTestHelpers.Run(comp, new AdapterGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKADP004");
    }

    [Fact]
    public void Reports_Error_When_Signature_Mismatch()
    {
        var source = """
            using PatternKit.Generators.Adapter;

            namespace TestNs;

            public interface ITarget
            {
                string Process(string input);
            }

            public class LegacyService { }

            [GenerateAdapter(Target = typeof(ITarget), Adaptee = typeof(LegacyService))]
            public static partial class MismatchHost
            {
                [AdapterMap(TargetMember = "Process")]
                public static int Process(LegacyService adaptee, string input) => 42;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Signature_Mismatch));
        _ = RoslynTestHelpers.Run(comp, new AdapterGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKADP005");
    }

    #endregion
}
