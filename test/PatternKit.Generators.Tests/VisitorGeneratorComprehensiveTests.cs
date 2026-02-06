using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

/// <summary>
/// Comprehensive behavioral tests for the Visitor Pattern Generator covering:
/// - Double-dispatch verification
/// - Handler priority and fallback behavior  
/// - All four visitor types (sync/async × result/action)
/// - Configuration options
/// - Error conditions
/// - Type safety and compile-time checks
/// - Complex hierarchies
/// - Builder fluency
/// </summary>
public class VisitorGeneratorComprehensiveTests
{
    private const string SimpleHierarchy = """
        using PatternKit.Generators.Visitors;

        namespace Test;

        [GenerateVisitor]
        public partial class Animal
        {
            public string Name { get; init; } = "";
        }

        public partial class Dog : Animal
        {
            public string Breed { get; init; } = "";
        }

        public partial class Cat : Animal
        {
            public bool IsIndoor { get; init; }
        }
        """;

    #region Double-Dispatch Behavioral Tests

    [Fact]
    public void Behavior_Accept_Performs_Double_Dispatch_To_Visit()
    {
        var user = SimpleHierarchy + """
            public static class DoubleDispatchTest
            {
                public static string Run()
                {
                    var calls = new System.Collections.Generic.List<string>();
                    
                    var visitor = new AnimalVisitorBuilder<string>()
                        .When<Dog>(d => { calls.Add($"Visit(Dog)"); return "Dog"; })
                        .When<Cat>(c => { calls.Add($"Visit(Cat)"); return "Cat"; })
                        .Build();

                    var dog = new Dog { Name = "Buddy" };
                    var cat = new Cat { Name = "Whiskers" };
                    
                    // Accept should call Visit - testing double dispatch
                    dog.Accept(visitor);
                    cat.Accept(visitor);
                    
                    return string.Join("|", calls);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Accept_Performs_Double_Dispatch_To_Visit));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("Test.DoubleDispatchTest")!
            .GetMethod("Run")!.Invoke(null, null) as string;

        Assert.Equal("Visit(Dog)|Visit(Cat)", result);
    }

    [Fact]
    public void Behavior_Most_Specific_Handler_Is_Chosen_First()
    {
        var user = SimpleHierarchy + """
            public static class PriorityTest
            {
                public static string Run()
                {
                    var visitor = new AnimalVisitorBuilder<string>()
                        .When<Dog>(d => "SpecificDog")
                        .When<Animal>(a => "BaseAnimal")
                        .Build();

                    var dog = new Dog { Name = "Rex" };
                    return dog.Accept(visitor);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Most_Specific_Handler_Is_Chosen_First));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("Test.PriorityTest")!
            .GetMethod("Run")!.Invoke(null, null) as string;

        Assert.Equal("SpecificDog", result);
    }

    [Fact]
    public void Behavior_Default_Handler_Used_When_No_Specific_Match()
    {
        var user = SimpleHierarchy + """
            public static class DefaultTest
            {
                public static string Run()
                {
                    var visitor = new AnimalVisitorBuilder<string>()
                        .Default(a => $"Default:{a.Name}")
                        .Build();

                    var dog = new Dog { Name = "Unknown" };
                    return dog.Accept(visitor);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Default_Handler_Used_When_No_Specific_Match));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("Test.DefaultTest")!
            .GetMethod("Run")!.Invoke(null, null) as string;

        Assert.Equal("Default:Unknown", result);
    }

    [Fact]
    public void Behavior_Throws_Exception_When_No_Handler_And_No_Default()
    {
        var user = SimpleHierarchy + """
            public static class NoHandlerTest
            {
                public static void Run()
                {
                    var visitor = new AnimalVisitorBuilder<string>()
                        .When<Cat>(c => "Cat")
                        .Build();

                    var dog = new Dog { Name = "Unhandled" };
                    dog.Accept(visitor); // Should throw
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Throws_Exception_When_No_Handler_And_No_Default));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            asm.GetType("Test.NoHandlerTest")!
                .GetMethod("Run")!.Invoke(null, null));

        Assert.Contains("No handler registered", ex.InnerException!.Message);
    }

    #endregion

    #region Action Visitor Behavioral Tests

    [Fact]
    public void Behavior_Action_Visitor_Executes_Side_Effects_Without_Return()
    {
        var user = SimpleHierarchy + """
            public static class ActionTest
            {
                public static string Run()
                {
                    var log = new System.Collections.Generic.List<string>();
                    
                    var visitor = new AnimalActionVisitorBuilder()
                        .When<Dog>(d => log.Add($"Dog:{d.Breed}"))
                        .When<Cat>(c => log.Add($"Cat:{c.IsIndoor}"))
                        .When<Animal>(a => log.Add($"Animal:{a.Name}"))
                        .Build();

                    new Dog { Breed = "Lab" }.Accept(visitor);
                    new Cat { IsIndoor = false }.Accept(visitor);
                    new Animal { Name = "Generic" }.Accept(visitor);
                    
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Action_Visitor_Executes_Side_Effects_Without_Return));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("Test.ActionTest")!
            .GetMethod("Run")!.Invoke(null, null) as string;

        Assert.Equal("Dog:Lab|Cat:False|Animal:Generic", result);
    }

    [Fact]
    public void Behavior_Action_Visitor_Default_Handler_Works()
    {
        var user = SimpleHierarchy + """
            public static class ActionDefaultTest
            {
                public static string Run()
                {
                    var log = new System.Collections.Generic.List<string>();
                    
                    var visitor = new AnimalActionVisitorBuilder()
                        .Default(a => log.Add($"Default:{a.GetType().Name}"))
                        .Build();

                    new Dog().Accept(visitor);
                    new Cat().Accept(visitor);
                    
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Action_Visitor_Default_Handler_Works));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("Test.ActionDefaultTest")!
            .GetMethod("Run")!.Invoke(null, null) as string;

        Assert.Equal("Default:Dog|Default:Cat", result);
    }

    #endregion

    #region Async Visitor Behavioral Tests

    [Fact]
    public void Behavior_Async_Visitor_Supports_ValueTask_Return_Type()
    {
        var user = SimpleHierarchy + """
            public static class AsyncTest
            {
                public static async System.Threading.Tasks.Task<string> Run()
                {
                    var visitor = new AnimalAsyncVisitorBuilder<string>()
                        .WhenAsync<Dog>(async (d, ct) =>
                        {
                            await System.Threading.Tasks.Task.Delay(1, ct);
                            return $"AsyncDog:{d.Breed}";
                        })
                        .WhenAsync<Cat>(async (c, ct) =>
                        {
                            await System.Threading.Tasks.Task.Delay(1, ct);
                            return $"AsyncCat:{c.IsIndoor}";
                        })
                        .Build();

                    var dog = new Dog { Breed = "Poodle" };
                    var cat = new Cat { IsIndoor = true };
                    
                    var r1 = await dog.AcceptAsync(visitor);
                    var r2 = await cat.AcceptAsync(visitor);
                    
                    return $"{r1}|{r2}";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Async_Visitor_Supports_ValueTask_Return_Type));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var task = asm.GetType("Test.AsyncTest")!
            .GetMethod("Run")!.Invoke(null, null) as Task<string>;

        Assert.Equal("AsyncDog:Poodle|AsyncCat:True", task!.Result);
    }

    [Fact]
    public void Behavior_Async_Action_Visitor_Performs_Async_Side_Effects()
    {
        var user = SimpleHierarchy + """
            public static class AsyncActionTest
            {
                public static async System.Threading.Tasks.Task<string> Run()
                {
                    var log = new System.Collections.Generic.List<string>();
                    
                    var visitor = new AnimalAsyncActionVisitorBuilder()
                        .WhenAsync<Dog>(async (d, ct) =>
                        {
                            await System.Threading.Tasks.Task.Delay(1, ct);
                            log.Add($"AsyncDog:{d.Breed}");
                        })
                        .WhenAsync<Cat>(async (c, ct) =>
                        {
                            await System.Threading.Tasks.Task.Delay(1, ct);
                            log.Add($"AsyncCat:{c.IsIndoor}");
                        })
                        .Build();

                    await new Dog { Breed = "Husky" }.AcceptAsync(visitor);
                    await new Cat { IsIndoor = false }.AcceptAsync(visitor);
                    
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Async_Action_Visitor_Performs_Async_Side_Effects));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var task = asm.GetType("Test.AsyncActionTest")!
            .GetMethod("Run")!.Invoke(null, null) as Task<string>;

        Assert.Equal("AsyncDog:Husky|AsyncCat:False", task!.Result);
    }

    [Fact]
    public void Behavior_CancellationToken_Propagates_Through_Async_Visitor()
    {
        var user = SimpleHierarchy + """
            public static class CancellationTest
            {
                public static async System.Threading.Tasks.Task<string> Run()
                {
                    var cts = new System.Threading.CancellationTokenSource();
                    cts.Cancel();
                    
                    var visitor = new AnimalAsyncVisitorBuilder<string>()
                        .WhenAsync<Dog>(async (d, ct) =>
                        {
                            ct.ThrowIfCancellationRequested();
                            await System.Threading.Tasks.Task.Delay(1, ct);
                            return "NotCancelled";
                        })
                        .Build();

                    try
                    {
                        await new Dog().AcceptAsync(visitor, cts.Token);
                        return "NoCancellation";
                    }
                    catch (System.OperationCanceledException)
                    {
                        return "Cancelled";
                    }
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_CancellationToken_Propagates_Through_Async_Visitor));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var task = asm.GetType("Test.CancellationTest")!
            .GetMethod("Run")!.Invoke(null, null) as Task<string>;

        Assert.Equal("Cancelled", task!.Result);
    }

    [Fact]
    public void Behavior_Async_Default_Handler_Works_Correctly()
    {
        var user = SimpleHierarchy + """
            public static class AsyncDefaultTest
            {
                public static async System.Threading.Tasks.Task<string> Run()
                {
                    var visitor = new AnimalAsyncVisitorBuilder<string>()
                        .DefaultAsync(async (a, ct) =>
                        {
                            await System.Threading.Tasks.Task.Delay(1, ct);
                            return $"DefaultAsync:{a.GetType().Name}";
                        })
                        .Build();

                    var r1 = await new Dog().AcceptAsync(visitor);
                    var r2 = await new Cat().AcceptAsync(visitor);
                    
                    return $"{r1}|{r2}";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Async_Default_Handler_Works_Correctly));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var task = asm.GetType("Test.AsyncDefaultTest")!
            .GetMethod("Run")!.Invoke(null, null) as Task<string>;

        Assert.Equal("DefaultAsync:Dog|DefaultAsync:Cat", task!.Result);
    }

    #endregion

    #region Complex Hierarchy Behavioral Tests

    [Fact]
    public void Behavior_Deep_Hierarchy_Three_Levels_Handled_Correctly()
    {
        var user = """
            using PatternKit.Generators.Visitors;

            namespace Test;

            [GenerateVisitor]
            public partial class Root { }
            public partial class Branch : Root { }
            public partial class Twig : Branch { }
            
            public static class DeepTest
            {
                public static string Run()
                {
                    var visitor = new RootVisitorBuilder<string>()
                        .When<Twig>(l => "Twig")
                        .When<Branch>(m => "Branch")
                        .When<Root>(b => "Root")
                        .Build();

                    var twig = new Twig();
                    var branch = new Branch();
                    var root = new Root();
                    
                    return $"{twig.Accept(visitor)}|{branch.Accept(visitor)}|{root.Accept(visitor)}";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Deep_Hierarchy_Three_Levels_Handled_Correctly));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        Assert.True(emitResult.Success, string.Join("\n", emitResult.Diagnostics));
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("Test.DeepTest")!
            .GetMethod("Run")!.Invoke(null, null) as string;

        Assert.Equal("Twig|Branch|Root", result);
    }

    [Fact]
    public void Behavior_Multiple_Siblings_In_Hierarchy_All_Visitable()
    {
        var user = """
            using PatternKit.Generators.Visitors;

            namespace Test;

            [GenerateVisitor]
            public partial class Shape { }
            public partial class Circle : Shape { }
            public partial class Square : Shape { }
            public partial class Triangle : Shape { }
            
            public static class SiblingTest
            {
                public static string Run()
                {
                    var visitor = new ShapeVisitorBuilder<string>()
                        .When<Circle>(c => "Circle")
                        .When<Square>(s => "Square")
                        .When<Triangle>(t => "Triangle")
                        .Build();

                    var results = new System.Collections.Generic.List<string>();
                    results.Add(new Circle().Accept(visitor));
                    results.Add(new Square().Accept(visitor));
                    results.Add(new Triangle().Accept(visitor));
                    return string.Join("|", results);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Multiple_Siblings_In_Hierarchy_All_Visitable));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("Test.SiblingTest")!
            .GetMethod("Run")!.Invoke(null, null) as string;

        Assert.Equal("Circle|Square|Triangle", result);
    }

    #endregion

    #region Type Safety Behavioral Tests

    [Fact]
    public void Behavior_Generic_When_Enforces_Type_Safety_At_Compile_Time()
    {
        var user = SimpleHierarchy + """
            public static class TypeSafetyTest
            {
                public static string Run()
                {
                    var visitor = new AnimalVisitorBuilder<string>()
                        .When<Dog>(dog => dog.Breed) // Should have access to Dog properties
                        .When<Cat>(cat => cat.IsIndoor.ToString()) // Should have access to Cat properties
                        .Build();

                    return new Dog { Breed = "TypeSafe" }.Accept(visitor);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Generic_When_Enforces_Type_Safety_At_Compile_Time));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, "Type-safe code should compile");

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("Test.TypeSafetyTest")!
            .GetMethod("Run")!.Invoke(null, null) as string;

        Assert.Equal("TypeSafe", result);
    }

    #endregion

    #region Builder Fluency Behavioral Tests

    [Fact]
    public void Behavior_Builder_Supports_Method_Chaining()
    {
        var user = SimpleHierarchy + """
            public static class ChainingTest
            {
                public static bool Run()
                {
                    // This tests that builder methods return the builder for chaining
                    var visitor = new AnimalVisitorBuilder<string>()
                        .When<Dog>(d => "D")
                        .When<Cat>(c => "C")
                        .Default(a => "A")
                        .Build(); // Build returns visitor

                    return visitor != null;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Builder_Supports_Method_Chaining));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, "Chaining code should compile");
    }

    [Fact]
    public void Behavior_Multiple_Handlers_Can_Be_Registered()
    {
        var user = SimpleHierarchy + """
            public static class MultiHandlerTest
            {
                public static string Run()
                {
                    var count = 0;
                    
                    var visitor = new AnimalActionVisitorBuilder()
                        .When<Dog>(d => count++)
                        .When<Cat>(c => count++)
                        .When<Animal>(a => count++)
                        .Build();

                    new Dog().Accept(visitor);
                    new Cat().Accept(visitor);
                    new Animal().Accept(visitor);
                    
                    return count.ToString();
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Multiple_Handlers_Can_Be_Registered));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("Test.MultiHandlerTest")!
            .GetMethod("Run")!.Invoke(null, null) as string;

        Assert.Equal("3", result);
    }

    #endregion

    #region Edge Cases and Error Behavioral Tests

    [Fact]
    public void Behavior_Empty_Visitor_With_Default_Only_Works()
    {
        var user = SimpleHierarchy + """
            public static class EmptyWithDefaultTest
            {
                public static string Run()
                {
                    var visitor = new AnimalVisitorBuilder<string>()
                        .Default(a => "AllDefault")
                        .Build();

                    return new Dog().Accept(visitor);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Empty_Visitor_With_Default_Only_Works));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("Test.EmptyWithDefaultTest")!
            .GetMethod("Run")!.Invoke(null, null) as string;

        Assert.Equal("AllDefault", result);
    }

    [Fact]
    public void Behavior_Same_Type_Registered_Multiple_Times_Last_Wins()
    {
        var user = SimpleHierarchy + """
            public static class DuplicateTest
            {
                public static string Run()
                {
                    var visitor = new AnimalVisitorBuilder<string>()
                        .When<Dog>(d => "First")
                        .When<Dog>(d => "Second")  // Should overwrite
                        .Build();

                    return new Dog().Accept(visitor);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user,
            assemblyName: nameof(Behavior_Same_Type_Registered_Multiple_Times_Last_Wins));
        var gen = new VisitorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        updated.Emit(pe);
        pe.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("Test.DuplicateTest")!
            .GetMethod("Run")!.Invoke(null, null) as string;

        Assert.Equal("Second", result);
    }

    #endregion
}
