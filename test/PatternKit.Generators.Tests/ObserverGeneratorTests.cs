using Microsoft.CodeAnalysis;
using System.Runtime.Loader;

namespace PatternKit.Generators.Tests;

public class ObserverGeneratorTests
{
    private const string SimpleObserver = """
        using PatternKit.Generators.Observer;

        namespace PatternKit.Examples.Generators;

        public record Temperature(double Celsius);

        [Observer(typeof(Temperature))]
        public partial class TemperatureChanged
        {
        }
        """;

    [Fact]
    public void Generates_Observer_Without_Diagnostics()
    {
        var comp = RoslynTestHelpers.CreateCompilation(
            SimpleObserver,
            assemblyName: nameof(Generates_Observer_Without_Diagnostics));

        var gen = new Observer.ObserverGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // No generator diagnostics
        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        // Confirm we generated expected file
        var sources = run.Results.SelectMany(r => r.GeneratedSources).ToArray();
        Assert.Single(sources);
        Assert.Contains("Observer.g.cs", sources[0].HintName);

        // The updated compilation should compile
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Reports_Error_When_Type_Not_Partial()
    {
        var code = """
            using PatternKit.Generators.Observer;
            namespace Test;
            public record Temperature(double Celsius);
            
            [Observer(typeof(Temperature))]
            public class TemperatureChanged { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            code,
            assemblyName: nameof(Reports_Error_When_Type_Not_Partial));

        var gen = new Observer.ObserverGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKOBS001");
    }

    [Fact]
    public void Subscribe_And_Publish_Works()
    {
        var user = SimpleObserver + """

            public static class Demo
            {
                public static string Run()
                {
                    var log = new System.Collections.Generic.List<string>();
                    var evt = new TemperatureChanged();
                    
                    evt.Subscribe((Temperature t) => log.Add($"Handler1:{t.Celsius}"));
                    evt.Subscribe((Temperature t) => log.Add($"Handler2:{t.Celsius}"));
                    
                    evt.Publish(new Temperature(23.5));
                    
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Subscribe_And_Publish_Works));

        var gen = new Observer.ObserverGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Load and invoke Demo.Run
        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success);

        pe.Position = 0;
        pdb.Position = 0;

        var alc = new AssemblyLoadContext("ObserverTest", isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(pe, pdb);
            var demoType = asm.GetType("PatternKit.Examples.Generators.Demo");
            Assert.NotNull(demoType);

            var runMethod = demoType.GetMethod("Run");
            Assert.NotNull(runMethod);

            var result = (string)runMethod.Invoke(null, null)!;
            Assert.Equal("Handler1:23.5|Handler2:23.5", result);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public void Dispose_Removes_Subscription()
    {
        var user = SimpleObserver + """

            public static class Demo
            {
                public static string Run()
                {
                    var log = new System.Collections.Generic.List<string>();
                    var evt = new TemperatureChanged();
                    
                    var sub1 = evt.Subscribe((Temperature t) => log.Add($"H1:{t.Celsius}"));
                    var sub2 = evt.Subscribe((Temperature t) => log.Add($"H2:{t.Celsius}"));
                    
                    evt.Publish(new Temperature(10));
                    sub1.Dispose();
                    evt.Publish(new Temperature(20));
                    
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Dispose_Removes_Subscription));

        var gen = new Observer.ObserverGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        Assert.True(emitResult.Success, $"Compilation failed: {string.Join(Environment.NewLine, emitResult.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))}");
        pe.Position = 0;

        var alc = new AssemblyLoadContext("ObserverTest", isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(pe);
            var demoType = asm.GetType("PatternKit.Examples.Generators.Demo");
            var runMethod = demoType!.GetMethod("Run");
            var result = (string)runMethod!.Invoke(null, null)!;
            
            // After first publish: both handlers; after second: only H2
            Assert.Equal("H1:10|H2:10|H2:20", result);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public void Registration_Order_Preserved()
    {
        var user = SimpleObserver + """

            public static class Demo
            {
                public static string Run()
                {
                    var log = new System.Collections.Generic.List<string>();
                    var evt = new TemperatureChanged();
                    
                    evt.Subscribe((Temperature t) => log.Add("A"));
                    evt.Subscribe((Temperature t) => log.Add("B"));
                    evt.Subscribe((Temperature t) => log.Add("C"));
                    
                    evt.Publish(new Temperature(0));
                    
                    return string.Join("", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Registration_Order_Preserved));

        var gen = new Observer.ObserverGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        Assert.True(emitResult.Success, $"Compilation failed: {string.Join(Environment.NewLine, emitResult.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))}");
        pe.Position = 0;

        var alc = new AssemblyLoadContext("ObserverTest", isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(pe);
            var demoType = asm.GetType("PatternKit.Examples.Generators.Demo");
            var runMethod = demoType!.GetMethod("Run");
            var result = (string)runMethod!.Invoke(null, null)!;
            Assert.Equal("ABC", result);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public void Async_Subscribe_And_PublishAsync_Works()
    {
        var user = SimpleObserver + """

            public static class Demo
            {
                public static async System.Threading.Tasks.Task<string> Run()
                {
                    var log = new System.Collections.Generic.List<string>();
                    var evt = new TemperatureChanged();
                    
                    evt.Subscribe(async (Temperature t) =>
                    {
                        await System.Threading.Tasks.Task.Delay(1);
                        log.Add($"AsyncHandler:{t.Celsius}");
                    });
                    
                    await evt.PublishAsync(new Temperature(42));
                    
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Async_Subscribe_And_PublishAsync_Works));

        var gen = new Observer.ObserverGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        Assert.True(emitResult.Success, $"Compilation failed: {string.Join(Environment.NewLine, emitResult.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))}");
        pe.Position = 0;

        var alc = new AssemblyLoadContext("ObserverTest", isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(pe);
            var demoType = asm.GetType("PatternKit.Examples.Generators.Demo");
            var runMethod = demoType!.GetMethod("Run");
            var task = (System.Threading.Tasks.Task<string>)runMethod!.Invoke(null, null)!;
            task.Wait();
            var result = task.Result;
            Assert.Equal("AsyncHandler:42", result);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public void Exception_Policy_Continue_Does_Not_Stop_Execution()
    {
        var user = """
            using PatternKit.Generators.Observer;

            namespace PatternKit.Examples.Generators;

            public record Temperature(double Celsius);

            [Observer(typeof(Temperature), Exceptions = ObserverExceptionPolicy.Continue)]
            public partial class TemperatureChanged
            {
                partial void OnSubscriberError(System.Exception ex)
                {
                    // Swallow the error
                }
            }

            public static class Demo
            {
                public static string Run()
                {
                    var log = new System.Collections.Generic.List<string>();
                    var evt = new TemperatureChanged();
                    
                    evt.Subscribe((Temperature t) => log.Add("H1"));
                    evt.Subscribe((Temperature t) => throw new System.Exception("Oops"));
                    evt.Subscribe((Temperature t) => log.Add("H3"));
                    
                    evt.Publish(new Temperature(0));
                    
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Exception_Policy_Continue_Does_Not_Stop_Execution));

        var gen = new Observer.ObserverGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        Assert.True(emitResult.Success, $"Compilation failed: {string.Join(Environment.NewLine, emitResult.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))}");
        pe.Position = 0;

        var alc = new AssemblyLoadContext("ObserverTest", isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(pe);
            var demoType = asm.GetType("PatternKit.Examples.Generators.Demo");
            var runMethod = demoType!.GetMethod("Run");
            var result = (string)runMethod!.Invoke(null, null)!;
            
            // All three handlers should execute (H1, exception, H3)
            Assert.Equal("H1|H3", result);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public void Exception_Policy_Stop_Throws_First_Exception()
    {
        var user = """
            using PatternKit.Generators.Observer;

            namespace PatternKit.Examples.Generators;

            public record Temperature(double Celsius);

            [Observer(typeof(Temperature), Exceptions = ObserverExceptionPolicy.Stop)]
            public partial class TemperatureChanged
            {
            }

            public static class Demo
            {
                public static string Run()
                {
                    var evt = new TemperatureChanged();
                    
                    evt.Subscribe((Temperature t) => { });
                    evt.Subscribe((Temperature t) => throw new System.Exception("Oops"));
                    evt.Subscribe((Temperature t) => { });
                    
                    try
                    {
                        evt.Publish(new Temperature(0));
                        return "No exception";
                    }
                    catch (System.Exception ex)
                    {
                        return ex.Message;
                    }
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Exception_Policy_Stop_Throws_First_Exception));

        var gen = new Observer.ObserverGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        Assert.True(emitResult.Success, $"Compilation failed: {string.Join(Environment.NewLine, emitResult.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))}");
        pe.Position = 0;

        var alc = new AssemblyLoadContext("ObserverTest", isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(pe);
            var demoType = asm.GetType("PatternKit.Examples.Generators.Demo");
            var runMethod = demoType!.GetMethod("Run");
            var result = (string)runMethod!.Invoke(null, null)!;
            Assert.Equal("Oops", result);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public void Exception_Policy_Aggregate_Throws_AggregateException()
    {
        var user = """
            using PatternKit.Generators.Observer;

            namespace PatternKit.Examples.Generators;

            public record Temperature(double Celsius);

            [Observer(typeof(Temperature), Exceptions = ObserverExceptionPolicy.Aggregate)]
            public partial class TemperatureChanged
            {
            }

            public static class Demo
            {
                public static string Run()
                {
                    var evt = new TemperatureChanged();
                    
                    evt.Subscribe((Temperature t) => throw new System.Exception("Error1"));
                    evt.Subscribe((Temperature t) => throw new System.Exception("Error2"));
                    
                    try
                    {
                        evt.Publish(new Temperature(0));
                        return "No exception";
                    }
                    catch (System.AggregateException ex)
                    {
                        return $"{ex.InnerExceptions.Count}:{ex.InnerExceptions[0].Message}:{ex.InnerExceptions[1].Message}";
                    }
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Exception_Policy_Aggregate_Throws_AggregateException));

        var gen = new Observer.ObserverGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        Assert.True(emitResult.Success, $"Compilation failed: {string.Join(Environment.NewLine, emitResult.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))}");
        pe.Position = 0;

        var alc = new AssemblyLoadContext("ObserverTest", isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(pe);
            var demoType = asm.GetType("PatternKit.Examples.Generators.Demo");
            var runMethod = demoType!.GetMethod("Run");
            var result = (string)runMethod!.Invoke(null, null)!;
            Assert.Equal("2:Error1:Error2", result);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public void Struct_Types_Are_Not_Supported()
    {
        var user = """
            using PatternKit.Generators.Observer;

            namespace PatternKit.Examples.Generators;

            public record Temperature(double Celsius);

            [Observer(typeof(Temperature))]
            public partial struct TemperatureChanged
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Struct_Types_Are_Not_Supported));

        var gen = new Observer.ObserverGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        // Should report PKOBS003 diagnostic for struct types
        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKOBS003" && d.GetMessage().Contains("Struct observer types are not currently supported"));
    }

    [Fact]
    public void Supports_Record_Class()
    {
        var user = """
            using PatternKit.Generators.Observer;

            namespace PatternKit.Examples.Generators;

            public record Temperature(double Celsius);

            [Observer(typeof(Temperature))]
            public partial record class TemperatureChanged
            {
            }

            public static class Demo
            {
                public static string Run()
                {
                    var log = new System.Collections.Generic.List<string>();
                    var evt = new TemperatureChanged();
                    
                    evt.Subscribe((Temperature t) => log.Add("OK"));
                    evt.Publish(new Temperature(0));
                    
                    return string.Join("", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Supports_Record_Class));

        var gen = new Observer.ObserverGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Record_Struct_Types_Are_Not_Supported()
    {
        var user = """
            using PatternKit.Generators.Observer;

            namespace PatternKit.Examples.Generators;

            public record Temperature(double Celsius);

            [Observer(typeof(Temperature))]
            public partial record struct TemperatureChanged
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Record_Struct_Types_Are_Not_Supported));

        var gen = new Observer.ObserverGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        // Should report PKOBS003 diagnostic for record struct types
        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKOBS003" && d.GetMessage().Contains("Struct observer types are not currently supported"));
    }

    [Fact]
    public void Mixed_Sync_And_Async_Handlers_Both_Invoked()
    {
        var user = SimpleObserver + """

            public static class Demo
            {
                public static async System.Threading.Tasks.Task<string> Run()
                {
                    var log = new System.Collections.Generic.List<string>();
                    var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
                    var evt = new TemperatureChanged();
                    
                    // Subscribe sync handler
                    evt.Subscribe((Temperature t) => log.Add("Sync"));
                    
                    // Subscribe async handler
                    evt.Subscribe(async (Temperature t) =>
                    {
                        await System.Threading.Tasks.Task.Yield();
                        log.Add("Async");
                        tcs.TrySetResult(true);
                    });
                    
                    // Sync Publish should invoke async handlers fire-and-forget
                    evt.Publish(new Temperature(10));
                    
                    // Wait deterministically for async handler to complete
                    await tcs.Task.WaitAsync(System.TimeSpan.FromSeconds(5));
                    
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(Mixed_Sync_And_Async_Handlers_Both_Invoked));

        var gen = new Observer.ObserverGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        Assert.True(emitResult.Success, $"Compilation failed: {string.Join(Environment.NewLine, emitResult.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))}");
        pe.Position = 0;

        var alc = new AssemblyLoadContext("ObserverTest", isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(pe);
            var demoType = asm.GetType("PatternKit.Examples.Generators.Demo");
            var runMethod = demoType!.GetMethod("Run");
            var task = (System.Threading.Tasks.Task<string>)runMethod!.Invoke(null, null)!;
            task.Wait();
            var result = task.Result;
            
            // Both handlers should have been invoked
            Assert.Contains("Sync", result);
            Assert.Contains("Async", result);
        }
        finally
        {
            alc.Unload();
        }
    }
}
