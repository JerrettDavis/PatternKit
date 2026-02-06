using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using PatternKit.Common;
using PatternKit.Creational.Builder;

namespace PatternKit.Generators.Tests;

public class StrategyGeneratorTests
{
    // A small user file that triggers all 3 strategies
    private const string Specs = """
                                 using PatternKit.Generators;

                                 namespace PatternKit.Examples.Generators;

                                 [GenerateStrategy(nameof(OrderRouter), typeof(char), StrategyKind.Action)]
                                 public partial class OrderRouter
                                 {
                                 }

                                 [GenerateStrategy(nameof(ScoreLabeler), typeof(int), typeof(string), StrategyKind.Result)]
                                 public partial class ScoreLabeler
                                 {
                                 }

                                 [GenerateStrategy(nameof(IntParser), typeof(string), typeof(int), StrategyKind.Try)]
                                 public partial class IntParser
                                 {
                                 }
                                 """;

    [Fact]
    public void Generates_All_Strategies_Without_Diagnostics()
    {
        // The generated code references PatternKit.Core (BranchBuilder/ChainBuilder/Throw),
        // so add that assembly as a metadata reference to make compilation succeed.
        var coreRef = MetadataReference.CreateFromFile(typeof(BranchBuilder<,>).Assembly.Location);
        var commonRef = MetadataReference.CreateFromFile(typeof(Throw).Assembly.Location);

        var comp = RoslynTestHelpers.CreateCompilation(
            Specs,
            assemblyName: nameof(Generates_All_Strategies_Without_Diagnostics),
            extra: [coreRef, commonRef]);

        var gen = new StrategyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // No generator diagnostics
        Assert.All(run.Results, r => Assert.True(r.Diagnostics.Length == 0));

        // Confirm we generated expected files
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("OrderRouter.g.cs", names);
        Assert.Contains("ScoreLabeler.g.cs", names);
        Assert.Contains("IntParser.g.cs", names);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void OrderRouter_Wires_Predicate_And_Action()
    {
        var coreRef = MetadataReference.CreateFromFile(typeof(BranchBuilder<,>).Assembly.Location);
        var commonRef = MetadataReference.CreateFromFile(typeof(Throw).Assembly.Location);

        var user = Specs + """
                               public static class Demo
                               {
                                   public static string Run()
                                   {
                                       var log = new System.Collections.Generic.List<string>();
                                       var r = new OrderRouter.Builder()
                                           .When((in char c) => char.IsLetter(c)).Then((in char c) => log.Add($"L:{c}"))
                                           .When((in char c) => char.IsDigit(c)).Then((in char c) => log.Add($"D:{c}"))
                                           .Default((in char c) => log.Add($"O:{c}"))
                                           .Build();

                                       r.Execute('A');
                                       r.Execute('7');
                                       r.Execute('@');

                                       return string.Join("|", log);
                                   }
                               }
                           """;
        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(OrderRouter_Wires_Predicate_And_Action),
            extra: [coreRef, commonRef]);
        var gen = new StrategyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // (Optional) load & invoke Demo.Run via reflection to assert behavior
        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var run = asm.GetType("PatternKit.Examples.Generators.Demo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;

        Assert.Equal("L:A|D:7|O:@", run);
    }

    [Fact]
    public void IntParser_Try_Handler_Signature_Works()
    {
        var coreRef = MetadataReference.CreateFromFile(typeof(ChainBuilder<>).Assembly.Location);
        var commonRef = MetadataReference.CreateFromFile(typeof(Throw).Assembly.Location);

        var user = Specs + """
                               public static class ParseDemo
                               {
                                   public static (bool ok, int value) Parse(string s)
                                   {
                                       var p = IntParser.Create()
                                           .Always(static (in string x, out int? r) => { if (int.TryParse(x, out var t)) { r = t; return true; } r = null; return false; })
                                           .Finally(static (in string _, out int? r) => { r = 0; return true; })
                                           .Build();
                                       var ok = p.Execute(s, out var v);
                                       return (ok, v ?? 0);
                                   }
                               }
                           """;
        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(IntParser_Try_Handler_Signature_Works),
            extra: [coreRef, commonRef]);
        var gen = new StrategyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var parse = asm.GetType("PatternKit.Examples.Generators.ParseDemo")!
            .GetMethod("Parse")!;
        Assert.Equal((true, 42), (ValueTuple<bool, int>)parse.Invoke(null, ["42"])!);
        Assert.Equal((true, 0), (ValueTuple<bool, int>)parse.Invoke(null, ["x"])!);
    }

    [Fact]
    public void ScoreLabeler_Result_Strategy_Works()
    {
        var coreRef = MetadataReference.CreateFromFile(typeof(BranchBuilder<,>).Assembly.Location);
        var commonRef = MetadataReference.CreateFromFile(typeof(Throw).Assembly.Location);

        var user = Specs + """
                               public static class ScoreDemo
                               {
                                   public static string Run()
                                   {
                                       var labeler = new ScoreLabeler.Builder()
                                           .When((in int score) => score >= 90).Then((in int _) => "A")
                                           .When((in int score) => score >= 80).Then((in int _) => "B")
                                           .When((in int score) => score >= 70).Then((in int _) => "C")
                                           .Default((in int _) => "F")
                                           .Build();

                                       return $"{labeler.Execute(95)},{labeler.Execute(85)},{labeler.Execute(75)},{labeler.Execute(50)}";
                                   }
                               }
                           """;
        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(ScoreLabeler_Result_Strategy_Works),
            extra: [coreRef, commonRef]);
        var gen = new StrategyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var run = asm.GetType("PatternKit.Examples.Generators.ScoreDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;

        Assert.Equal("A,B,C,F", run);
    }

    [Fact]
    public void TryExecute_Returns_False_When_No_Match()
    {
        var coreRef = MetadataReference.CreateFromFile(typeof(BranchBuilder<,>).Assembly.Location);
        var commonRef = MetadataReference.CreateFromFile(typeof(Throw).Assembly.Location);

        var user = Specs + """
                               public static class TryExecuteDemo
                               {
                                   public static string Run()
                                   {
                                       var router = new OrderRouter.Builder()
                                           .When((in char c) => c == 'A').Then((in char c) => { })
                                           .Build();

                                       var matchedA = router.TryExecute('A');
                                       var matchedB = router.TryExecute('B');

                                       return $"{matchedA},{matchedB}";
                                   }
                               }
                           """;
        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(TryExecute_Returns_False_When_No_Match),
            extra: [coreRef, commonRef]);
        var gen = new StrategyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var run = asm.GetType("PatternKit.Examples.Generators.TryExecuteDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;

        Assert.Equal("True,False", run);
    }

    [Fact]
    public void Simple_Namespace_Strategy_Works()
    {
        var coreRef = MetadataReference.CreateFromFile(typeof(BranchBuilder<,>).Assembly.Location);
        var commonRef = MetadataReference.CreateFromFile(typeof(Throw).Assembly.Location);

        const string source = """
            using PatternKit.Generators;

            namespace SimpleNamespace;

            [GenerateStrategy(nameof(SimpleRouter), typeof(int), StrategyKind.Action)]
            public partial class SimpleRouter { }

            public static class SimpleDemo
            {
                public static string Run()
                {
                    var log = new System.Collections.Generic.List<string>();
                    var router = new SimpleRouter.Builder()
                        .When((in int x) => x > 0).Then((in int x) => log.Add($"+{x}"))
                        .Default((in int x) => log.Add($"={x}"))
                        .Build();

                    router.Execute(5);
                    router.Execute(0);
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Simple_Namespace_Strategy_Works),
            extra: [coreRef, commonRef]);
        var gen = new StrategyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);
        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var result = asm.GetType("SimpleNamespace.SimpleDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;

        Assert.Equal("+5|=0", result);
    }

    [Fact]
    public void TryStrategy_When_Conditional_Chain_Works()
    {
        var coreRef = MetadataReference.CreateFromFile(typeof(ChainBuilder<>).Assembly.Location);
        var commonRef = MetadataReference.CreateFromFile(typeof(Throw).Assembly.Location);

        var user = Specs + """
                               public static class ConditionalDemo
                               {
                                   public static string Run(bool enableFallback)
                                   {
                                       var parser = IntParser.Create()
                                           .When(enableFallback)
                                               .Add(static (in string x, out int? r) => { r = -1; return true; })
                                               .End
                                           .Build();

                                       parser.Execute("abc", out var v);
                                       return v?.ToString() ?? "null";
                                   }
                               }
                           """;
        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(TryStrategy_When_Conditional_Chain_Works),
            extra: [coreRef, commonRef]);
        var gen = new StrategyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var run = asm.GetType("PatternKit.Examples.Generators.ConditionalDemo")!
            .GetMethod("Run")!;

        // With fallback enabled
        var withFallback = run.Invoke(null, [true]) as string;
        Assert.Equal("-1", withFallback);

        // Without fallback - returns default (null result)
        var withoutFallback = run.Invoke(null, [false]) as string;
        Assert.Equal("null", withoutFallback);
    }

    [Fact]
    public void Strategies_On_Separate_Classes_Work()
    {
        var coreRef = MetadataReference.CreateFromFile(typeof(BranchBuilder<,>).Assembly.Location);
        var commonRef = MetadataReference.CreateFromFile(typeof(Throw).Assembly.Location);

        const string source = """
            using PatternKit.Generators;

            namespace PatternKit.Examples.Generators;

            [GenerateStrategy("ProcessInt", typeof(int), StrategyKind.Action)]
            public partial class IntProcessor { }

            [GenerateStrategy("ProcessString", typeof(string), StrategyKind.Action)]
            public partial class StringProcessor { }

            public static class MultiDemo
            {
                public static string Run()
                {
                    var log = new System.Collections.Generic.List<string>();

                    var intProcessor = new ProcessInt.Builder()
                        .When((in int x) => x > 0).Then((in int x) => log.Add($"int:{x}"))
                        .Default((in int _) => log.Add("int:default"))
                        .Build();

                    var strProcessor = new ProcessString.Builder()
                        .When((in string s) => !string.IsNullOrEmpty(s)).Then((in string s) => log.Add($"str:{s}"))
                        .Default((in string _) => log.Add("str:default"))
                        .Build();

                    intProcessor.Execute(42);
                    strProcessor.Execute("hello");

                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Strategies_On_Separate_Classes_Work),
            extra: [coreRef, commonRef]);
        var gen = new StrategyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // Should generate both ProcessInt.g.cs and ProcessString.g.cs
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("ProcessInt.g.cs", names);
        Assert.Contains("ProcessString.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var result = asm.GetType("PatternKit.Examples.Generators.MultiDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;

        Assert.Equal("int:42|str:hello", result);
    }
}