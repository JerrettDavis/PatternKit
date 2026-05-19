using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public class VisitorGeneratorTests
{
    // Test AST hierarchy used across tests
    private const string AstHierarchy = """
                                        using PatternKit.Generators.Visitors;

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

    [Scenario("Generates Visitor Infrastructure Without Diagnostics")]
    [Fact]
    public void Generates_Visitor_Infrastructure_Without_Diagnostics()
    {

        var comp = RoslynTestHelpers.CreateCompilation(
            AstHierarchy,
            assemblyName: nameof(Generates_Visitor_Infrastructure_Without_Diagnostics));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // No generator diagnostics
        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Confirm we generated expected files
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();

        // Interfaces
        ScenarioExpect.Contains("IAstNodeVisitor.Interfaces.g.cs", names);

        // Accept methods for each type
        ScenarioExpect.Contains("AstNode.Accept.g.cs", names);
        ScenarioExpect.Contains("Expression.Accept.g.cs", names);
        ScenarioExpect.Contains("Statement.Accept.g.cs", names);
        ScenarioExpect.Contains("NumberExpression.Accept.g.cs", names);
        ScenarioExpect.Contains("AddExpression.Accept.g.cs", names);

        var generatedSources = run.Results.SelectMany(r => r.GeneratedSources).ToDictionary(
            gs => gs.HintName,
            gs => gs.SourceText.ToString());
        ScenarioExpect.Contains("public TResult Accept<TResult>", generatedSources["AstNode.Accept.g.cs"]);
        ScenarioExpect.Contains("public new TResult Accept<TResult>", generatedSources["Expression.Accept.g.cs"]);
        ScenarioExpect.Contains("public new System.Threading.Tasks.ValueTask<TResult> AcceptAsync<TResult>", generatedSources["NumberExpression.Accept.g.cs"]);

        // Builders
        ScenarioExpect.Contains("AstNodeVisitorBuilder.g.cs", names);
        ScenarioExpect.Contains("AstNodeActionVisitorBuilder.g.cs", names);
        ScenarioExpect.Contains("AstNodeAsyncVisitorBuilder.g.cs", names);
        ScenarioExpect.Contains("AstNodeAsyncActionVisitorBuilder.g.cs", names);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Sync Result Visitor Dispatches Correctly")]
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
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        ScenarioExpect.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var run = asm.GetType("PatternKit.Examples.Ast.Demo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;

        ScenarioExpect.Equal("Num:42|Add|Expr|Stmt", run);
    }

    [Scenario("Sync Action Visitor Executes Side Effects")]
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
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        ScenarioExpect.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var run = asm.GetType("PatternKit.Examples.Ast.ActionDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;

        ScenarioExpect.Equal("N:7|A", run);
    }

    [Scenario("Async Result Visitor Supports ValueTask")]
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
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        ScenarioExpect.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var run = asm.GetType("PatternKit.Examples.Ast.AsyncDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as System.Threading.Tasks.Task<string>;

        ScenarioExpect.NotNull(run);
        var result = run.GetAwaiter().GetResult();
        ScenarioExpect.Equal("Async:99|AsyncExpr", result);
    }

    [Scenario("Async Action Visitor Supports ValueTask")]
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
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        ScenarioExpect.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var run = asm.GetType("PatternKit.Examples.Ast.AsyncActionDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as System.Threading.Tasks.Task<string>;

        ScenarioExpect.NotNull(run);
        var result = run.GetAwaiter().GetResult();
        ScenarioExpect.Equal("AN:123|AS", result);
    }

    [Scenario("Throws When No Handler Matches And No Default")]
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
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        ScenarioExpect.True(res.Success);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var runMethod = asm.GetType("PatternKit.Examples.Ast.NoDefaultDemo")!.GetMethod("Run")!;

        ScenarioExpect.Throws<System.Reflection.TargetInvocationException>(() => runMethod.Invoke(null, null));
    }

    [Scenario("Custom Visitor Interface Name Works")]
    [Fact]
    public void Custom_Visitor_Interface_Name_Works()
    {

        var code = """
                   using PatternKit.Generators.Visitors;

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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("INodeProcessor.Interfaces.g.cs", names);
        ScenarioExpect.Contains("NodeVisitorBuilder.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Disable Async Generation Works")]
    [Fact]
    public void Disable_Async_Generation_Works()
    {

        var code = """
                   using PatternKit.Generators.Visitors;

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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();

        // Should have sync builders
        ScenarioExpect.Contains("SyncNodeVisitorBuilder.g.cs", names);
        ScenarioExpect.Contains("SyncNodeActionVisitorBuilder.g.cs", names);

        // Should NOT have async builders
        ScenarioExpect.DoesNotContain("SyncNodeAsyncVisitorBuilder.g.cs", names);
        ScenarioExpect.DoesNotContain("SyncNodeAsyncActionVisitorBuilder.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Disable Actions Generation Works")]
    [Fact]
    public void Disable_Actions_Generation_Works()
    {

        var code = """
                   using PatternKit.Generators.Visitors;

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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();

        // Should have result builders
        ScenarioExpect.Contains("ResultNodeVisitorBuilder.g.cs", names);
        ScenarioExpect.Contains("ResultNodeAsyncVisitorBuilder.g.cs", names);

        // Should NOT have action builders
        ScenarioExpect.DoesNotContain("ResultNodeActionVisitorBuilder.g.cs", names);
        ScenarioExpect.DoesNotContain("ResultNodeAsyncActionVisitorBuilder.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Generates Visitor For Interface Hierarchy")]
    [Fact]
    public void Generates_Visitor_For_Interface_Hierarchy()
    {
        const string interfaceHierarchy = """
            using PatternKit.Generators.Visitors;

            namespace PatternKit.Examples.Shapes;

            [GenerateVisitor]
            public partial interface IShape { }

            public partial class Circle : IShape 
            {
                public double Radius { get; init; }
            }

            public partial class Rectangle : IShape 
            {
                public double Width { get; init; }
                public double Height { get; init; }
            }

            public partial class Triangle : IShape 
            {
                public double Base { get; init; }
                public double Height { get; init; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            interfaceHierarchy,
            assemblyName: nameof(Generates_Visitor_For_Interface_Hierarchy));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // No generator diagnostics
        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Confirm we generated expected files
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();

        // Interfaces
        ScenarioExpect.Contains("IShapeVisitor.Interfaces.g.cs", names);

        // Accept methods for each type (interface + concrete classes)
        ScenarioExpect.Contains("IShape.Accept.g.cs", names);
        ScenarioExpect.Contains("Circle.Accept.g.cs", names);
        ScenarioExpect.Contains("Rectangle.Accept.g.cs", names);
        ScenarioExpect.Contains("Triangle.Accept.g.cs", names);

        // Builders
        ScenarioExpect.Contains("IShapeVisitorBuilder.g.cs", names);

        // Verify compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Interface Hierarchy Visitor Dispatches Correctly")]
    [Fact]
    public void Interface_Hierarchy_Visitor_Dispatches_Correctly()
    {
        const string interfaceHierarchyWithUsage = """
            using PatternKit.Generators.Visitors;

            namespace PatternKit.Examples.Shapes;

            [GenerateVisitor]
            public partial interface IShape { }

            public partial class Circle : IShape 
            {
                public double Radius { get; init; }
            }

            public partial class Rectangle : IShape 
            {
                public double Width { get; init; }
                public double Height { get; init; }
            }

            public static class Demo
            {
                public static double Run()
                {
                    var areaCalculator = new IShapeVisitorBuilder<double>()
                        .When<Circle>(c => 3.14159 * c.Radius * c.Radius)
                        .When<Rectangle>(r => r.Width * r.Height)
                        .Default(_ => 0.0)
                        .Build();

                    var circle = new Circle { Radius = 5.0 };
                    var rectangle = new Rectangle { Width = 4.0, Height = 6.0 };

                    var circleArea = circle.Accept(areaCalculator);
                    var rectangleArea = rectangle.Accept(areaCalculator);

                    return circleArea + rectangleArea;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            interfaceHierarchyWithUsage,
            assemblyName: nameof(Interface_Hierarchy_Visitor_Dispatches_Correctly));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Emit and execute
        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        ScenarioExpect.True(res.Success, string.Join("\n", res.Diagnostics));
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var demo = asm.GetType("PatternKit.Examples.Shapes.Demo")!;
        var runMethod = demo.GetMethod("Run")!;
        var result = (double)runMethod.Invoke(null, null)!;

        // Circle area: π * 5^2 ≈ 78.54
        // Rectangle area: 4 * 6 = 24
        // Total ≈ 102.54
        ScenarioExpect.True(result > 100 && result < 105, $"Expected ~102.54, got {result}");
    }

    [Scenario("Generates Visitor For Struct Hierarchy")]
    [Fact]
    public void Generates_Visitor_For_Struct_Hierarchy()
    {
        const string structHierarchy = """
            using PatternKit.Generators.Visitors;

            namespace PatternKit.Examples.Values;

            [GenerateVisitor]
            public partial interface IValue { }

            public partial struct IntValue : IValue 
            {
                public int Value { get; init; }
            }

            public partial struct DoubleValue : IValue 
            {
                public double Value { get; init; }
            }

            public partial struct StringValue : IValue 
            {
                public string Value { get; init; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            structHierarchy,
            assemblyName: nameof(Generates_Visitor_For_Struct_Hierarchy));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // No generator diagnostics
        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Confirm we generated expected files
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();

        // Interfaces
        ScenarioExpect.Contains("IValueVisitor.Interfaces.g.cs", names);

        // Accept methods for interface and structs
        ScenarioExpect.Contains("IValue.Accept.g.cs", names);
        ScenarioExpect.Contains("IntValue.Accept.g.cs", names);
        ScenarioExpect.Contains("DoubleValue.Accept.g.cs", names);
        ScenarioExpect.Contains("StringValue.Accept.g.cs", names);

        // Verify compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Struct Visitor Dispatches Without Boxing")]
    [Fact]
    public void Struct_Visitor_Dispatches_Without_Boxing()
    {
        const string structHierarchyWithUsage = """
            using PatternKit.Generators.Visitors;

            namespace PatternKit.Examples.Values;

            [GenerateVisitor]
            public partial interface IValue { }

            public partial struct IntValue : IValue 
            {
                public int Value { get; init; }
            }

            public partial struct DoubleValue : IValue 
            {
                public double Value { get; init; }
            }

            public static class Demo
            {
                public static string Run()
                {
                    var formatter = new IValueVisitorBuilder<string>()
                        .When<IntValue>(i => $"Int:{i.Value}")
                        .When<DoubleValue>(d => $"Double:{d.Value:F2}")
                        .Default(_ => "Unknown")
                        .Build();

                    var intVal = new IntValue { Value = 42 };
                    var doubleVal = new DoubleValue { Value = 3.14159 };

                    var intStr = intVal.Accept(formatter);
                    var doubleStr = doubleVal.Accept(formatter);

                    return $"{intStr},{doubleStr}";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            structHierarchyWithUsage,
            assemblyName: nameof(Struct_Visitor_Dispatches_Without_Boxing));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Emit and execute
        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        ScenarioExpect.True(res.Success, string.Join("\n", res.Diagnostics));
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var demo = asm.GetType("PatternKit.Examples.Values.Demo")!;
        var runMethod = demo.GetMethod("Run")!;
        var result = (string)runMethod.Invoke(null, null)!;

        ScenarioExpect.Equal("Int:42,Double:3.14", result);
    }

    [Scenario("Diagnostic PKVIS001 EmittedWhenNoConcretTypesFound")]
    [Fact]
    public void Diagnostic_PKVIS001_EmittedWhenNoConcretTypesFound()
    {
        const string noDerivedTypes = """
            using PatternKit.Generators.Visitors;

            namespace PatternKit.Examples;

            [GenerateVisitor]
            public partial interface IEmptyHierarchy { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            noDerivedTypes,
            assemblyName: nameof(Diagnostic_PKVIS001_EmittedWhenNoConcretTypesFound));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // Should have PKVIS001 warning
        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKVIS001");

        var pkvis001 = diagnostics.First(d => d.Id == "PKVIS001");
        ScenarioExpect.Contains("IEmptyHierarchy", pkvis001.GetMessage());
    }

    [Scenario("Diagnostic PKVIS002 EmittedWhenBaseTypeNotPartial")]
    [Fact]
    public void Diagnostic_PKVIS002_EmittedWhenBaseTypeNotPartial()
    {
        const string nonPartialBase = """
            using PatternKit.Generators.Visitors;

            namespace PatternKit.Examples;

            [GenerateVisitor]
            public class NonPartialBase { }
            
            public partial class DerivedType : NonPartialBase { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            nonPartialBase,
            assemblyName: nameof(Diagnostic_PKVIS002_EmittedWhenBaseTypeNotPartial));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // Should have PKVIS002 error
        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKVIS002");

        var pkvis002 = diagnostics.First(d => d.Id == "PKVIS002");
        ScenarioExpect.Contains("NonPartialBase", pkvis002.GetMessage());
    }

    [Scenario("Diagnostic PKVIS004 EmittedWhenDerivedTypeNotPartial")]
    [Fact]
    public void Diagnostic_PKVIS004_EmittedWhenDerivedTypeNotPartial()
    {
        const string nonPartialDerived = """
            using PatternKit.Generators.Visitors;

            namespace PatternKit.Examples;

            [GenerateVisitor]
            public partial class Base { }
            
            public class NonPartialDerived : Base { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            nonPartialDerived,
            assemblyName: nameof(Diagnostic_PKVIS004_EmittedWhenDerivedTypeNotPartial));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // Should have PKVIS004 error
        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKVIS004");

        var pkvis004 = diagnostics.First(d => d.Id == "PKVIS004");
        ScenarioExpect.Contains("NonPartialDerived", pkvis004.GetMessage());
    }

    [Scenario("No Diagnostics For Valid Hierarchy")]
    [Fact]
    public void No_Diagnostics_For_Valid_Hierarchy()
    {
        const string validHierarchy = """
            using PatternKit.Generators.Visitors;

            namespace PatternKit.Examples;

            [GenerateVisitor]
            public partial class ValidBase { }
            
            public partial class ValidDerived : ValidBase { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            validHierarchy,
            assemblyName: nameof(No_Diagnostics_For_Valid_Hierarchy));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // Should have no generator diagnostics
        var diagnostics = run.Results.SelectMany(r => r.Diagnostics)
            .Where(d => d.Id.StartsWith("PKVIS"))
            .ToArray();
        ScenarioExpect.Empty(diagnostics);
    }

    [Scenario("Generates Visitor For Record Hierarchy")]
    [Fact]
    public void Generates_Visitor_For_Record_Hierarchy()
    {
        const string recordHierarchy = """
            using PatternKit.Generators.Visitors;

            namespace PatternKit.Examples.Records;

            [GenerateVisitor]
            public abstract partial record Message;

            public partial record TextMessage(string Content) : Message;

            public partial record ImageMessage(byte[] Data, string Format) : Message;

            public partial record AudioMessage(string Url, int DurationSeconds) : Message;
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            recordHierarchy,
            assemblyName: nameof(Generates_Visitor_For_Record_Hierarchy));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // No generator diagnostics
        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Confirm we generated expected files
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();

        // Interfaces
        ScenarioExpect.Contains("IMessageVisitor.Interfaces.g.cs", names);

        // Accept methods for each record type
        ScenarioExpect.Contains("Message.Accept.g.cs", names);
        ScenarioExpect.Contains("TextMessage.Accept.g.cs", names);
        ScenarioExpect.Contains("ImageMessage.Accept.g.cs", names);
        ScenarioExpect.Contains("AudioMessage.Accept.g.cs", names);

        // Builders
        ScenarioExpect.Contains("MessageVisitorBuilder.g.cs", names);

        // Verify compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Record Visitor Dispatches Correctly")]
    [Fact]
    public void Record_Visitor_Dispatches_Correctly()
    {
        const string recordHierarchyWithUsage = """
            using PatternKit.Generators.Visitors;

            namespace PatternKit.Examples.Records;

            [GenerateVisitor]
            public abstract partial record Message;

            public partial record TextMessage(string Content) : Message;

            public partial record ImageMessage(byte[] Data, string Format) : Message;

            public static class Demo
            {
                public static string Run()
                {
                    var formatter = new MessageVisitorBuilder<string>()
                        .When<TextMessage>(m => $"Text: {m.Content}")
                        .When<ImageMessage>(m => $"Image: {m.Format}")
                        .Default(_ => "Unknown")
                        .Build();

                    var text = new TextMessage("Hello World");
                    var image = new ImageMessage(new byte[] { 1, 2, 3 }, "PNG");

                    var textStr = text.Accept(formatter);
                    var imageStr = image.Accept(formatter);

                    return $"{textStr}|{imageStr}";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            recordHierarchyWithUsage,
            assemblyName: nameof(Record_Visitor_Dispatches_Correctly));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Emit and execute
        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        ScenarioExpect.True(res.Success, string.Join("\n", res.Diagnostics));
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var demo = asm.GetType("PatternKit.Examples.Records.Demo")!;
        var runMethod = demo.GetMethod("Run")!;
        var result = (string)runMethod.Invoke(null, null)!;

        ScenarioExpect.Equal("Text: Hello World|Image: PNG", result);
    }

    [Scenario("Diagnostic PKVIS002 EmittedWhenInterfaceBaseTypeNotPartial")]
    [Fact]
    public void Diagnostic_PKVIS002_EmittedWhenInterfaceBaseTypeNotPartial()
    {
        const string nonPartialInterface = """
            using PatternKit.Generators.Visitors;

            namespace PatternKit.Examples;

            [GenerateVisitor]
            public interface INotPartial { }
            
            public partial class DerivedType : INotPartial { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            nonPartialInterface,
            assemblyName: nameof(Diagnostic_PKVIS002_EmittedWhenInterfaceBaseTypeNotPartial));

        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // Should have PKVIS002 error
        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKVIS002");

        var pkvis002 = diagnostics.First(d => d.Id == "PKVIS002");
        ScenarioExpect.Contains("INotPartial", pkvis002.GetMessage());
    }
}
