using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using PatternKit.Common;
using PatternKit.Creational.Builder;

namespace PatternKit.Generators.Tests;

public class StrategyGeneratorTests
{
    // A small user file that triggers all 3 strategies
    private const string Specs ="""
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
}