using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

public class VisitorGeneratorTests
{
    // Test AST hierarchy used across tests
    private const string AstHierarchy = """
                                        using PatternKit.Generators;

                                        namespace PatternKit.Examples.Ast;

                                        [GenerateVisitor]
                                        public partial class AstNode
                                        {
                                        }

                                        public partial class Expression : AstNode
                                        {
                                        }

                                        public partial class Statement : AstNode
                                        {
                                        }

                                        public partial class NumberExpression : Expression
                                        {
                                            public int Value { get; init; }
                                        }

                                        public partial class AddExpression : Expression
                                        {
                                            public Expression Left { get; init; } = null!;
                                            public Expression Right { get; init; } = null!;
                                        }
                                        """;

    [Fact]
    public void Generates_Visitor_Infrastructure_Without_Diagnostics()
    {

        var comp = RoslynTestHelpers.CreateCompilation(
            AstHierarchy,
            assemblyName: nameof(Generates_Visitor_Infrastructure_Without_Diagnostics));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // No generator diagnostics
        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        // Confirm we generated expected files
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        
        // Interfaces
        Assert.Contains("IAstNodeVisitor.Interfaces.g.cs", names);
        
        // Accept methods for each type
        Assert.Contains("AstNode.Accept.g.cs", names);
        Assert.Contains("Expression.Accept.g.cs", names);
        Assert.Contains("Statement.Accept.g.cs", names);
        Assert.Contains("NumberExpression.Accept.g.cs", names);
        Assert.Contains("AddExpression.Accept.g.cs", names);
        
        // Builders
        Assert.Contains("AstNodeVisitorBuilder.g.cs", names);
        Assert.Contains("AstNodeActionVisitorBuilder.g.cs", names);
        Assert.Contains("AstNodeAsyncVisitorBuilder.g.cs", names);
        Assert.Contains("AstNodeAsyncActionVisitorBuilder.g.cs", names);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Sync_Result_Visitor_Dispatches_Correctly()
    {

        var user = AstHierarchy + """
                                       public static class Demo
                                       {
                                           public static string Run()
                                           {
                                               var visitor = new AstNodeVisitorBuilder<string>()
                                                   .When<NumberExpression>(n => $"Num:{n.Value}")
                                                   .When<AddExpression>(a => "Add")
                                                   .When<Expression>(e => "Expr")
                                                   .When<Statement>(s => "Stmt")
                                                   .Default(n => "Unknown")
                                                   .Build();

                                               var num = new NumberExpression { Value = 42 };
                                               var add = new AddExpression { Left = num, Right = num };
                                               var expr = new Expression();
                                               var stmt = new Statement();

                                               var r1 = num.Accept(visitor);
                                               var r2 = add.Accept(visitor);
                                               var r3 = expr.Accept(visitor);
                                               var r4 = stmt.Accept(visitor);

                                               return $"{r1}|{r2}|{r3}|{r4}";
                                           }
                                       }
                                   """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Sync_Result_Visitor_Dispatches_Correctly));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var run = asm.GetType("PatternKit.Examples.Ast.Demo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;

        Assert.Equal("Num:42|Add|Expr|Stmt", run);
    }

    [Fact]
    public void Sync_Action_Visitor_Executes_Side_Effects()
    {

        var user = AstHierarchy + """
                                       public static class ActionDemo
                                       {
                                           public static string Run()
                                           {
                                               var log = new System.Collections.Generic.List<string>();
                                               
                                               var visitor = new AstNodeActionVisitorBuilder()
                                                   .When<NumberExpression>(n => log.Add($"N:{n.Value}"))
                                                   .When<AddExpression>(a => log.Add("A"))
                                                   .When<Expression>(e => log.Add("E"))
                                                   .When<Statement>(s => log.Add("S"))
                                                   .Default(n => log.Add("D"))
                                                   .Build();

                                               var num = new NumberExpression { Value = 7 };
                                               var add = new AddExpression { Left = num, Right = num };
                                               
                                               num.Accept(visitor);
                                               add.Accept(visitor);

                                               return string.Join("|", log);
                                           }
                                       }
                                   """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Sync_Action_Visitor_Executes_Side_Effects));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var run = asm.GetType("PatternKit.Examples.Ast.ActionDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;

        Assert.Equal("N:7|A", run);
    }

    [Fact]
    public void Async_Result_Visitor_Supports_ValueTask()
    {

        var user = AstHierarchy + """
                                       public static class AsyncDemo
                                       {
                                           public static async System.Threading.Tasks.Task<string> Run()
                                           {
                                               var visitor = new AstNodeAsyncVisitorBuilder<string>()
                                                   .WhenAsync<NumberExpression>(async (n, ct) => {
                                                       await System.Threading.Tasks.Task.Delay(1, ct);
                                                       return $"Async:{n.Value}";
                                                   })
                                                   .WhenAsync<Expression>(async (e, ct) => {
                                                       await System.Threading.Tasks.Task.Delay(1, ct);
                                                       return "AsyncExpr";
                                                   })
                                                   .DefaultAsync(async (n, ct) => {
                                                       await System.Threading.Tasks.Task.Delay(1, ct);
                                                       return "Default";
                                                   })
                                                   .Build();

                                               var num = new NumberExpression { Value = 99 };
                                               var expr = new Expression();
                                               
                                               var r1 = await num.AcceptAsync(visitor);
                                               var r2 = await expr.AcceptAsync(visitor);

                                               return $"{r1}|{r2}";
                                           }
                                       }
                                   """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Async_Result_Visitor_Supports_ValueTask));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var run = asm.GetType("PatternKit.Examples.Ast.AsyncDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as System.Threading.Tasks.Task<string>;

        Assert.NotNull(run);
        var result = run.GetAwaiter().GetResult();
        Assert.Equal("Async:99|AsyncExpr", result);
    }

    [Fact]
    public void Async_Action_Visitor_Supports_ValueTask()
    {

        var user = AstHierarchy + """
                                       public static class AsyncActionDemo
                                       {
                                           public static async System.Threading.Tasks.Task<string> Run()
                                           {
                                               var log = new System.Collections.Generic.List<string>();
                                               
                                               var visitor = new AstNodeAsyncActionVisitorBuilder()
                                                   .WhenAsync<NumberExpression>(async (n, ct) => {
                                                       await System.Threading.Tasks.Task.Delay(1, ct);
                                                       log.Add($"AN:{n.Value}");
                                                   })
                                                   .WhenAsync<Statement>(async (s, ct) => {
                                                       await System.Threading.Tasks.Task.Delay(1, ct);
                                                       log.Add("AS");
                                                   })
                                                   .DefaultAsync(async (n, ct) => {
                                                       await System.Threading.Tasks.Task.Delay(1, ct);
                                                       log.Add("AD");
                                                   })
                                                   .Build();

                                               var num = new NumberExpression { Value = 123 };
                                               var stmt = new Statement();
                                               
                                               await num.AcceptAsync(visitor);
                                               await stmt.AcceptAsync(visitor);

                                               return string.Join("|", log);
                                           }
                                       }
                                   """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Async_Action_Visitor_Supports_ValueTask));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var run = asm.GetType("PatternKit.Examples.Ast.AsyncActionDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as System.Threading.Tasks.Task<string>;

        Assert.NotNull(run);
        var result = run.GetAwaiter().GetResult();
        Assert.Equal("AN:123|AS", result);
    }

    [Fact]
    public void Throws_When_No_Handler_Matches_And_No_Default()
    {

        var user = AstHierarchy + """
                                       public static class NoDefaultDemo
                                       {
                                           public static void Run()
                                           {
                                               var visitor = new AstNodeVisitorBuilder<string>()
                                                   .When<NumberExpression>(n => "Num")
                                                   .Build();

                                               var stmt = new Statement();
                                               
                                               try
                                               {
                                                   stmt.Accept(visitor);
                                               }
                                               catch (System.InvalidOperationException)
                                               {
                                                   throw;
                                               }
                                           }
                                       }
                                   """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Throws_When_No_Handler_Matches_And_No_Default));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var runMethod = asm.GetType("PatternKit.Examples.Ast.NoDefaultDemo")!.GetMethod("Run")!;

        Assert.Throws<System.Reflection.TargetInvocationException>(() => runMethod.Invoke(null, null));
    }

    [Fact]
    public void Custom_Visitor_Interface_Name_Works()
    {

        var code = """
                   using PatternKit.Generators;

                   namespace PatternKit.Examples.Custom;

                   [GenerateVisitor(VisitorInterfaceName = "INodeProcessor")]
                   public partial class Node
                   {
                   }

                   public partial class LeafNode : Node
                   {
                   }
                   """;

        var comp = RoslynTestHelpers.CreateCompilation(
            code,
            assemblyName: nameof(Custom_Visitor_Interface_Name_Works));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));
        
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("INodeProcessor.Interfaces.g.cs", names);
        Assert.Contains("NodeVisitorBuilder.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Disable_Async_Generation_Works()
    {

        var code = """
                   using PatternKit.Generators;

                   namespace PatternKit.Examples.SyncOnly;

                   [GenerateVisitor(GenerateAsync = false)]
                   public partial class SyncNode
                   {
                   }

                   public partial class SyncLeaf : SyncNode
                   {
                   }
                   """;

        var comp = RoslynTestHelpers.CreateCompilation(
            code,
            assemblyName: nameof(Disable_Async_Generation_Works));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));
        
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        
        // Should have sync builders
        Assert.Contains("SyncNodeVisitorBuilder.g.cs", names);
        Assert.Contains("SyncNodeActionVisitorBuilder.g.cs", names);
        
        // Should NOT have async builders
        Assert.DoesNotContain("SyncNodeAsyncVisitorBuilder.g.cs", names);
        Assert.DoesNotContain("SyncNodeAsyncActionVisitorBuilder.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Disable_Actions_Generation_Works()
    {

        var code = """
                   using PatternKit.Generators;

                   namespace PatternKit.Examples.ResultOnly;

                   [GenerateVisitor(GenerateActions = false)]
                   public partial class ResultNode
                   {
                   }

                   public partial class ResultLeaf : ResultNode
                   {
                   }
                   """;

        var comp = RoslynTestHelpers.CreateCompilation(
            code,
            assemblyName: nameof(Disable_Actions_Generation_Works));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));
        
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        
        // Should have result builders
        Assert.Contains("ResultNodeVisitorBuilder.g.cs", names);
        Assert.Contains("ResultNodeAsyncVisitorBuilder.g.cs", names);
        
        // Should NOT have action builders
        Assert.DoesNotContain("ResultNodeActionVisitorBuilder.g.cs", names);
        Assert.DoesNotContain("ResultNodeAsyncActionVisitorBuilder.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
