using Microsoft.CodeAnalysis;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public class DispatcherGeneratorTests
{
    [Scenario("GeneratesDispatcherWithoutDiagnostics")]
    [Fact]
    public void GeneratesDispatcherWithoutDiagnostics()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(GeneratesDispatcherWithoutDiagnostics));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // No generator diagnostics
        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Confirm we generated expected files
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("AppDispatcher.g.cs", names);
        ScenarioExpect.Contains("AppDispatcher.Builder.g.cs", names);
        ScenarioExpect.Contains("AppDispatcher.Contracts.g.cs", names);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GeneratedCodeHasNoPatternKitDependency")]
    [Fact]
    public void GeneratedCodeHasNoPatternKitDependency()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(GeneratedCodeHasNoPatternKitDependency));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        // Check that generated source doesn't reference PatternKit
        foreach (var text in run.Results.SelectMany(result => result.GeneratedSources.Select(generated => generated.SourceText.ToString())))
        {
            ScenarioExpect.DoesNotContain("using PatternKit", text);
            ScenarioExpect.DoesNotContain("PatternKit.", text);
        }
    }

    [Scenario("CommandRegistration HappyPath")]
    [Fact]
    public void CommandRegistration_HappyPath()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record Ping(string Message);
            public record Pong(string Reply);
            
            public static class Demo
            {
                public static async Task<string> Run()
                {
                    var dispatcher = AppDispatcher.Create()
                        .Command<Ping, Pong>((req, ct) => new ValueTask<Pong>(new Pong($"Echo: {req.Message}")))
                        .Build();
                    
                    var response = await dispatcher.Send<Ping, Pong>(new Ping("Hello"), default);
                    return response.Reply;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(CommandRegistration_HappyPath));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Load and run the demo
        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        ScenarioExpect.Equal("Echo: Hello", result);
    }

    [Scenario("NotificationRegistration MultipleHandlers")]
    [Fact]
    public void NotificationRegistration_MultipleHandlers()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record UserCreated(string Username);
            
            public static class Demo
            {
                private static List<string> log = new();
                
                public static async Task<string> Run()
                {
                    log.Clear();
                    var dispatcher = AppDispatcher.Create()
                        .Notification<UserCreated>((n, ct) => { log.Add($"Email: {n.Username}"); return ValueTask.CompletedTask; })
                        .Notification<UserCreated>((n, ct) => { log.Add($"Audit: {n.Username}"); return ValueTask.CompletedTask; })
                        .Build();
                    
                    await dispatcher.Publish(new UserCreated("alice"), default);
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(NotificationRegistration_MultipleHandlers));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Load and run
        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        ScenarioExpect.Equal("Email: alice|Audit: alice", result);
    }

    [Scenario("NotificationRegistration ZeroHandlers NoOp")]
    [Fact]
    public void NotificationRegistration_ZeroHandlers_NoOp()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record UserDeleted(string Username);
            
            public static class Demo
            {
                public static async Task<bool> Run()
                {
                    var dispatcher = AppDispatcher.Create().Build();
                    await dispatcher.Publish(new UserDeleted("bob"), default);
                    return true; // Should not throw
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(NotificationRegistration_ZeroHandlers_NoOp));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Load and run
        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<bool>)run!.Invoke(null, null)!;
        var result = task.Result;

        ScenarioExpect.True(result);
    }

    [Scenario("CommandPipeline PreAndPost")]
    [Fact]
    public void CommandPipeline_PreAndPost()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record Calculate(int Value);
            public record Result(int Value);
            
            public static class Demo
            {
                private static List<string> log = new();
                
                public static async Task<string> Run()
                {
                    log.Clear();
                    var dispatcher = AppDispatcher.Create()
                        .Pre<Calculate>((req, ct) => { log.Add("Pre"); return ValueTask.CompletedTask; })
                        .Command<Calculate, Result>((req, ct) => { log.Add("Handler"); return new ValueTask<Result>(new Result(req.Value * 2)); })
                        .Post<Calculate, Result>((req, res, ct) => { log.Add($"Post:{res.Value}"); return ValueTask.CompletedTask; })
                        .Build();
                    
                    await dispatcher.Send<Calculate, Result>(new Calculate(5), default);
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(CommandPipeline_PreAndPost));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Load and run
        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        ScenarioExpect.Equal("Pre|Handler|Post:10", result);
    }

    [Scenario("MissingCommandHandler ThrowsException")]
    [Fact]
    public void MissingCommandHandler_ThrowsException()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record UnhandledCommand(string Data);
            public record Response(string Data);
            
            public static class Demo
            {
                public static async Task<string> Run()
                {
                    var dispatcher = AppDispatcher.Create().Build();
                    try
                    {
                        await dispatcher.Send<UnhandledCommand, Response>(new UnhandledCommand("test"), default);
                        return "NoException";
                    }
                    catch (InvalidOperationException ex)
                    {
                        return ex.Message.Contains("No handler") ? "ExpectedException" : "WrongException";
                    }
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(MissingCommandHandler_ThrowsException));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Load and run
        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        ScenarioExpect.Equal("ExpectedException", result);
    }

    [Scenario("StreamRegistration LazyEnumeration")]
    [Fact]
    public void StreamRegistration_LazyEnumeration()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record RangeQuery(int Start, int End);
            
            public static class Demo
            {
                private static async IAsyncEnumerable<int> GenerateRange(RangeQuery req, [EnumeratorCancellation] CancellationToken ct)
                {
                    for (int i = req.Start; i <= req.End; i++)
                    {
                        yield return i;
                    }
                }
                
                public static async Task<string> Run()
                {
                    var dispatcher = AppDispatcher.Create()
                        .Stream<RangeQuery, int>(GenerateRange)
                        .Build();
                    
                    var items = new List<int>();
                    await foreach (var item in dispatcher.Stream<RangeQuery, int>(new RangeQuery(1, 5), default))
                    {
                        items.Add(item);
                    }
                    
                    return string.Join(",", items);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(StreamRegistration_LazyEnumeration));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Load and run
        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        ScenarioExpect.Equal("1,2,3,4,5", result);
    }

    [Scenario("ContractsFile DefinesInterfaces")]
    [Fact]
    public void ContractsFile_DefinesInterfaces()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(ContractsFile_DefinesInterfaces));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var contractsFile = run.Results
            .SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(gs => gs.HintName == "AppDispatcher.Contracts.g.cs");

        ScenarioExpect.NotNull(contractsFile);

        var text = contractsFile.SourceText.ToString();
        ScenarioExpect.Contains("interface ICommandHandler<TRequest, TResponse>", text);
        ScenarioExpect.Contains("interface INotificationHandler<TNotification>", text);
        ScenarioExpect.Contains("interface IStreamHandler<TRequest, TItem>", text);
        ScenarioExpect.Contains("delegate ValueTask<TResponse> CommandNext<TResponse>", text);
    }

    #region Around Middleware Tests

    [Scenario("AroundMiddleware SingleBehavior WrapsHandler")]
    [Fact]
    public void AroundMiddleware_SingleBehavior_WrapsHandler()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record Multiply(int Value);
            public record Result(int Value);
            
            public static class Demo
            {
                private static List<string> log = new();
                
                public static async Task<string> Run()
                {
                    log.Clear();
                    var dispatcher = AppDispatcher.Create()
                        .Command<Multiply, Result>((req, ct) => 
                        {
                            log.Add($"Handler:{req.Value}");
                            return new ValueTask<Result>(new Result(req.Value * 2));
                        })
                        .Around<Multiply, Result>(async (req, ct, next) =>
                        {
                            log.Add("Around:Before");
                            var result = await next();
                            log.Add($"Around:After:{result.Value}");
                            return result;
                        })
                        .Build();
                    
                    var response = await dispatcher.Send<Multiply, Result>(new Multiply(5), default);
                    log.Add($"Final:{response.Value}");
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(AroundMiddleware_SingleBehavior_WrapsHandler));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        ScenarioExpect.Equal("Around:Before|Handler:5|Around:After:10|Final:10", result);
    }

    [Scenario("AroundMiddleware MultipleBehaviors ComposesInOrder")]
    [Fact]
    public void AroundMiddleware_MultipleBehaviors_ComposesInOrder()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record Add(int Value);
            public record Result(int Value);
            
            public static class Demo
            {
                private static List<string> log = new();
                
                public static async Task<string> Run()
                {
                    log.Clear();
                    var dispatcher = AppDispatcher.Create()
                        .Command<Add, Result>((req, ct) => 
                        {
                            log.Add("Handler");
                            return new ValueTask<Result>(new Result(req.Value + 10));
                        })
                        .Around<Add, Result>(async (req, ct, next) =>
                        {
                            log.Add("Around1:Before");
                            var result = await next();
                            log.Add("Around1:After");
                            return result;
                        }, order: 1)
                        .Around<Add, Result>(async (req, ct, next) =>
                        {
                            log.Add("Around2:Before");
                            var result = await next();
                            log.Add("Around2:After");
                            return result;
                        }, order: 2)
                        .Build();
                    
                    await dispatcher.Send<Add, Result>(new Add(5), default);
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(AroundMiddleware_MultipleBehaviors_ComposesInOrder));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        // Order: 1 (outer) wraps 2 (inner)
        // Execution: Around1:Before -> Around2:Before -> Handler -> Around2:After -> Around1:After
        ScenarioExpect.Equal("Around1:Before|Around2:Before|Handler|Around2:After|Around1:After", result);
    }

    [Scenario("AroundMiddleware ModifiesRequestAndResponse VerifiesNesting")]
    [Fact]
    public void AroundMiddleware_ModifiesRequestAndResponse_VerifiesNesting()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record Request(int Value);
            public record Response(int Value);
            
            public static class Demo
            {
                public static async Task<int> Run()
                {
                    var dispatcher = AppDispatcher.Create()
                        .Command<Request, Response>((req, ct) => 
                            new ValueTask<Response>(new Response(req.Value)))
                        .Around<Request, Response>(async (req, ct, next) =>
                        {
                            // Outer Around adds 10 after handler
                            var result = await next();
                            return new Response(result.Value + 10);
                        }, order: 1)
                        .Around<Request, Response>(async (req, ct, next) =>
                        {
                            // Inner Around multiplies by 2 after handler
                            var result = await next();
                            return new Response(result.Value * 2);
                        }, order: 2)
                        .Build();
                    
                    var response = await dispatcher.Send<Request, Response>(new Request(5), default);
                    return response.Value;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(AroundMiddleware_ModifiesRequestAndResponse_VerifiesNesting));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<int>)run!.Invoke(null, null)!;
        var result = task.Result;

        // Flow: 5 -> handler(5) -> inner(*2=10) -> outer(+10=20)
        ScenarioExpect.Equal(20, result);
    }

    [Scenario("AroundMiddleware WithPreAndPost ExecutesInCorrectOrder")]
    [Fact]
    public void AroundMiddleware_WithPreAndPost_ExecutesInCorrectOrder()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record DoWork(int Value);
            public record Result(int Value);
            
            public static class Demo
            {
                private static List<string> log = new();
                
                public static async Task<string> Run()
                {
                    log.Clear();
                    var dispatcher = AppDispatcher.Create()
                        .Pre<DoWork>((req, ct) => { log.Add("Pre"); return ValueTask.CompletedTask; })
                        .Command<DoWork, Result>((req, ct) => 
                        {
                            log.Add("Handler");
                            return new ValueTask<Result>(new Result(req.Value));
                        })
                        .Around<DoWork, Result>(async (req, ct, next) =>
                        {
                            log.Add("Around:Before");
                            var result = await next();
                            log.Add("Around:After");
                            return result;
                        })
                        .Post<DoWork, Result>((req, res, ct) => { log.Add("Post"); return ValueTask.CompletedTask; })
                        .Build();
                    
                    await dispatcher.Send<DoWork, Result>(new DoWork(1), default);
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(AroundMiddleware_WithPreAndPost_ExecutesInCorrectOrder));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        // Expected: Pre -> Around Before -> Handler -> Around After -> Post
        ScenarioExpect.Equal("Pre|Around:Before|Handler|Around:After|Post", result);
    }

    #endregion

    #region OnError Handling Tests

    [Scenario("OnError HandlerThrows ExecutesErrorHandler")]
    [Fact]
    public void OnError_HandlerThrows_ExecutesErrorHandler()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record FailingCommand(string Message);
            public record Result(string Data);
            
            public static class Demo
            {
                private static List<string> log = new();
                
                public static async Task<string> Run()
                {
                    log.Clear();
                    var dispatcher = AppDispatcher.Create()
                        .Command<FailingCommand, Result>((req, ct) => 
                        {
                            log.Add("Handler:Throwing");
                            throw new InvalidOperationException(req.Message);
                        })
                        .OnError<FailingCommand, Result>((req, ex, ct) => 
                        {
                            log.Add($"OnError:{ex.Message}");
                            return ValueTask.CompletedTask;
                        })
                        .Build();
                    
                    try
                    {
                        await dispatcher.Send<FailingCommand, Result>(new FailingCommand("TestError"), default);
                    }
                    catch (InvalidOperationException)
                    {
                        log.Add("Caught");
                    }
                    
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(OnError_HandlerThrows_ExecutesErrorHandler));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        ScenarioExpect.Equal("Handler:Throwing|OnError:TestError|Caught", result);
    }

    [Scenario("OnError PrePostAndOnError ExecutesCorrectly")]
    [Fact]
    public void OnError_PrePostAndOnError_ExecutesCorrectly()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record FailCommand(string Message);
            public record Result(string Data);
            
            public static class Demo
            {
                private static List<string> log = new();
                
                public static async Task<string> Run()
                {
                    log.Clear();
                    var dispatcher = AppDispatcher.Create()
                        .Pre<FailCommand>((req, ct) => { log.Add("Pre"); return ValueTask.CompletedTask; })
                        .Command<FailCommand, Result>((req, ct) => throw new Exception("Fail"))
                        .Post<FailCommand, Result>((req, res, ct) => { log.Add("Post:ShouldNotRun"); return ValueTask.CompletedTask; })
                        .OnError<FailCommand, Result>((req, ex, ct) => { log.Add("OnError"); return ValueTask.CompletedTask; })
                        .Build();
                    
                    try
                    {
                        await dispatcher.Send<FailCommand, Result>(new FailCommand("Test"), default);
                    }
                    catch
                    {
                        log.Add("Caught");
                    }
                    
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(OnError_PrePostAndOnError_ExecutesCorrectly));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        // Pre runs, handler throws, OnError runs, Post does NOT run
        ScenarioExpect.Equal("Pre|OnError|Caught", result);
    }

    #endregion

    #region Stream Pipeline Tests

    [Scenario("StreamPipeline PreHook ExecutesBeforeStream")]
    [Fact]
    public void StreamPipeline_PreHook_ExecutesBeforeStream()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record GetNumbers(int Count);
            
            public static class Demo
            {
                private static List<string> log = new();
                
                private static async IAsyncEnumerable<int> GenerateNumbers(GetNumbers req, [EnumeratorCancellation] CancellationToken ct)
                {
                    for (int i = 1; i <= req.Count; i++)
                    {
                        log.Add($"Item:{i}");
                        yield return i;
                    }
                }
                
                public static async Task<string> Run()
                {
                    log.Clear();
                    var dispatcher = AppDispatcher.Create()
                        .PreStream<GetNumbers>((req, ct) => { log.Add("PreStream"); return ValueTask.CompletedTask; })
                        .Stream<GetNumbers, int>(GenerateNumbers)
                        .Build();
                    
                    await foreach (var num in dispatcher.Stream<GetNumbers, int>(new GetNumbers(3), default))
                    {
                        // Consume
                    }
                    
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(StreamPipeline_PreHook_ExecutesBeforeStream));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        ScenarioExpect.Equal("PreStream|Item:1|Item:2|Item:3", result);
    }

    #endregion

    #region Object Overload Tests

    [Scenario("ObjectOverloads Send DispatchesCorrectly")]
    [Fact]
    public void ObjectOverloads_Send_DispatchesCorrectly()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(
                Namespace = "MyApp.Messaging", 
                Name = "AppDispatcher",
                IncludeObjectOverloads = true)]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record GetValue(int Id);
            public record ValueResult(int Value);
            
            public static class Demo
            {
                public static async Task<string> Run()
                {
                    var dispatcher = AppDispatcher.Create()
                        .Command<GetValue, ValueResult>((req, ct) => 
                            new ValueTask<ValueResult>(new ValueResult(req.Id * 10)))
                        .Build();
                    
                    object request = new GetValue(5);
                    var response = await dispatcher.Send(request, default);
                    
                    if (response is ValueResult vr)
                        return $"Result:{vr.Value}";
                    
                    return "Failed";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(ObjectOverloads_Send_DispatchesCorrectly));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        ScenarioExpect.Equal("Result:50", result);
    }

    [Scenario("ObjectOverloads Publish DispatchesCorrectly")]
    [Fact]
    public void ObjectOverloads_Publish_DispatchesCorrectly()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(
                Namespace = "MyApp.Messaging", 
                Name = "AppDispatcher",
                IncludeObjectOverloads = true)]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record SomethingHappened(string Message);
            
            public static class Demo
            {
                private static List<string> log = new();
                
                public static async Task<string> Run()
                {
                    log.Clear();
                    var dispatcher = AppDispatcher.Create()
                        .Notification<SomethingHappened>((evt, ct) => 
                        {
                            log.Add($"Handler:{evt.Message}");
                            return ValueTask.CompletedTask;
                        })
                        .Build();
                    
                    object notification = new SomethingHappened("Test");
                    await dispatcher.Publish(notification, default);
                    
                    return string.Join("|", log);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(ObjectOverloads_Publish_DispatchesCorrectly));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        ScenarioExpect.Equal("Handler:Test", result);
    }

    [Scenario("ObjectOverloads Stream DispatchesCorrectly")]
    [Fact]
    public void ObjectOverloads_Stream_DispatchesCorrectly()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(
                Namespace = "MyApp.Messaging", 
                Name = "AppDispatcher",
                IncludeStreaming = true,
                IncludeObjectOverloads = true)]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record RangeRequest(int Start, int Count);
            
            public static class Demo
            {
                private static async IAsyncEnumerable<int> GenerateRange(RangeRequest req, [EnumeratorCancellation] CancellationToken ct)
                {
                    for (int i = req.Start; i < req.Start + req.Count; i++)
                    {
                        yield return i;
                    }
                }
                
                public static async Task<string> Run()
                {
                    try
                    {
                        var dispatcher = AppDispatcher.Create()
                            .Stream<RangeRequest, int>(GenerateRange)
                            .Build();
                        
                        var items = new List<int>();
                        object request = new RangeRequest(10, 5);
                        
                        await foreach (var item in dispatcher.Stream(request, default))
                        {
                            items.Add((int)item!);
                        }
                        
                        return string.Join(",", items);
                    }
                    catch (Exception ex)
                    {
                        return $"ERROR:{ex.Message}";
                    }
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(ObjectOverloads_Stream_DispatchesCorrectly));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        // Should either be the expected result or an error message
        if (result.StartsWith("ERROR:"))
        {
            ScenarioExpect.Fail($"Test threw exception: {result}");
        }
        ScenarioExpect.Equal("10,11,12,13,14", result);
    }

    #endregion

    #region Module System Tests

    [Scenario("ModuleSystem AddModule RegistersHandlers")]
    [Fact]
    public void ModuleSystem_AddModule_RegistersHandlers()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using System.Threading;
            using System.Threading.Tasks;
            
            [assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
            
            namespace MyApp;
            
            using MyApp.Messaging;

            public record Ping(string Message);
            public record Pong(string Reply);
            
            public class TestModule : IModule
            {
                public void Register(IDispatcherBuilder builder)
                {
                    builder.Command<Ping, Pong>((req, ct) => 
                        new ValueTask<Pong>(new Pong($"Module:{req.Message}")));
                }
            }
            
            public static class Demo
            {
                public static async Task<string> Run()
                {
                    var dispatcher = AppDispatcher.Create()
                        .AddModule(new TestModule())
                        .Build();
                    
                    var response = await dispatcher.Send<Ping, Pong>(new Ping("Hello"), default);
                    return response.Reply;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(ModuleSystem_AddModule_RegistersHandlers));

        var gen = new PatternKit.Generators.Messaging.DispatcherGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        ScenarioExpect.True(emitResult.Success);

        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;

        ScenarioExpect.Equal("Module:Hello", result);
    }

    #endregion
}
