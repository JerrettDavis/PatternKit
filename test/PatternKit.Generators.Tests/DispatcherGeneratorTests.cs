using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

public class DispatcherGeneratorTests
{
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
        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        // Confirm we generated expected files
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("AppDispatcher.g.cs", names);
        Assert.Contains("AppDispatcher.Builder.g.cs", names);
        Assert.Contains("AppDispatcher.Contracts.g.cs", names);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

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
            Assert.DoesNotContain("using PatternKit", text);
            Assert.DoesNotContain("PatternKit.", text);
        }
    }

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
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Load and run the demo
        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        Assert.True(emitResult.Success);
        
        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;
        
        Assert.Equal("Echo: Hello", result);
    }

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
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Load and run
        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        Assert.True(emitResult.Success);
        
        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;
        
        Assert.Equal("Email: alice|Audit: alice", result);
    }

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
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Load and run
        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        Assert.True(emitResult.Success);
        
        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<bool>)run!.Invoke(null, null)!;
        var result = task.Result;
        
        Assert.True(result);
    }

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
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Load and run
        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        Assert.True(emitResult.Success);
        
        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;
        
        Assert.Equal("Pre|Handler|Post:10", result);
    }

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
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Load and run
        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        Assert.True(emitResult.Success);
        
        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;
        
        Assert.Equal("ExpectedException", result);
    }

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
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Load and run
        using var pe = new MemoryStream();
        var emitResult = updated.Emit(pe);
        Assert.True(emitResult.Success);
        
        pe.Seek(0, SeekOrigin.Begin);
        var asm = System.Reflection.Assembly.Load(pe.ToArray());
        var demo = asm.GetType("MyApp.Demo");
        var run = demo!.GetMethod("Run");
        var task = (Task<string>)run!.Invoke(null, null)!;
        var result = task.Result;
        
        Assert.Equal("1,2,3,4,5", result);
    }

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

        Assert.NotNull(contractsFile);
        
        var text = contractsFile.SourceText.ToString();
        Assert.Contains("interface ICommandHandler<TRequest, TResponse>", text);
        Assert.Contains("interface INotificationHandler<TNotification>", text);
        Assert.Contains("interface IStreamHandler<TRequest, TItem>", text);
        Assert.Contains("delegate ValueTask<TResponse> CommandNext<TResponse>", text);
    }
}
