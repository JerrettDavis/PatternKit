using System.Runtime.Loader;
using PatternKit.Generators.Factories;

namespace PatternKit.Generators.Tests;

public class FactoriesGeneratorTests
{
    [Fact]
    public void FactoryMethod_Generates_Sync_Create_And_TryCreate()
    {
        const string user = """
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryMethod(typeof(string), CreateMethodName = "Make")]
                            public static partial class MimeFactory
                            {
                                [FactoryCase("json")]
                                public static string Json() => "application/json";

                                [FactoryDefault]
                                public static string Default() => "application/octet-stream";
                            }

                            public static class Runner
                            {
                                public static string Run()
                                {
                                    var okJson = MimeFactory.TryCreate("json", out var json);
                                    var okOther = MimeFactory.TryCreate("other", out var other);
                                    return $"{MimeFactory.Make("json")}|{okJson}|{json}|{okOther}|{other}";
                                }
                            }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(FactoryMethod_Generates_Sync_Create_And_TryCreate));

        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.True(r.Diagnostics.IsEmpty));
        Assert.Contains("MimeFactory.FactoryMethod.g.cs",
            run.Results.SelectMany(r => r.GeneratedSources).Select(g => g.HintName));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var emit = updated.Emit(pe, pdb);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        pe.Position = 0;
        pdb.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var runResult = asm.GetType("Demo.Runner")!.GetMethod("Run")!.Invoke(null, null);
        Assert.Equal("application/json|True|application/json|False|application/octet-stream", runResult);
    }

    [Fact]
    public async Task FactoryMethod_Async_Methods_Use_ValueTask()
    {
        const string user = """
                            using System.Threading.Tasks;
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryMethod(typeof(int))]
                            public static partial class NumberNames
                            {
                                [FactoryCase(1)]
                                public static ValueTask<string> OneAsync() => ValueTask.FromResult("one");

                                [FactoryDefault]
                                public static Task<string> DefaultAsync() => Task.FromResult("other");
                            }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(FactoryMethod_Async_Methods_Use_ValueTask));

        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.True(r.Diagnostics.IsEmpty));
        var hintNames = run.Results.SelectMany(r => r.GeneratedSources).Select(g => g.HintName).ToArray();
        Assert.Contains("NumberNames.FactoryMethod.g.cs", hintNames);

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var type = asm.GetType("Demo.NumberNames")!;
        var createAsync = type.GetMethod("CreateAsync")!;
        var tryCreateAsync = type.GetMethod("TryCreateAsync")!;

        var task = (ValueTask<string>)createAsync.Invoke(null, new object?[] { 1 })!;
        Assert.Equal("one", await task);

        var tuple = await ((ValueTask<(bool Success, string Result)>)tryCreateAsync.Invoke(null, [2])!);
        Assert.False(tuple.Success);
        Assert.Equal("other", tuple.Result);
    }

    [Fact]
    public void FactoryClass_Generates_Create_And_TryCreate()
    {
        const string user = """
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryClass(typeof(string))]
                            public interface IMessage { }

                            [FactoryClassKey("email")]
                            public class Email : IMessage { }

                            [FactoryClassKey("sms")]
                            public class Sms : IMessage { }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(FactoryClass_Generates_Create_And_TryCreate));

        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.True(r.Diagnostics.IsEmpty));
        Assert.Contains("MessageFactory.FactoryClass.g.cs",
            run.Results.SelectMany(r => r.GeneratedSources).Select(g => g.HintName));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var emit = updated.Emit(pe, pdb);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        pe.Position = 0;
        pdb.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var factoryType = asm.GetType("Demo.MessageFactory")!;
        dynamic factory = Activator.CreateInstance(factoryType)!;

        var email = factoryType.GetMethod("Create")!.Invoke(factory, new object?[] { "email" });
        Assert.Equal("Demo.Email", email!.GetType().FullName);

        var args = new object?[] { "sms", null };
        var success = (bool)factoryType.GetMethod("TryCreate")!.Invoke(factory, args)!;
        Assert.True(success);
        Assert.Equal("Demo.Sms", args[1]!.GetType().FullName);
    }

    [Fact]
    public void FactoryClass_Emits_Enum_Keys_And_Overloads()
    {
        const string user = """
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryClass(typeof(string), GenerateEnumKeys = true, FactoryTypeName = "NotificationFactory")]
                            public interface INotification { }

                            [FactoryClassKey("email")]
                            public class Email : INotification { }

                            [FactoryClassKey("sms")]
                            public class Sms : INotification { }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(FactoryClass_Emits_Enum_Keys_And_Overloads));

        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.True(r.Diagnostics.IsEmpty));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var emit = updated.Emit(pe, pdb);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        pe.Position = 0;
        pdb.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var factoryType = asm.GetType("Demo.NotificationFactory")!;
        var enumType = factoryType.GetNestedType("Keys")!;
        var baseType = asm.GetType("Demo.INotification")!;
        dynamic factory = Activator.CreateInstance(factoryType)!;

        var emailKey = Enum.Parse(enumType, "Email");
        var smsKey = Enum.Parse(enumType, "Sms");

        var createEnum = factoryType.GetMethod("Create", new[] { enumType })!;
        var email = createEnum.Invoke(factory, new[] { emailKey });
        Assert.Equal("Demo.Email", email!.GetType().FullName);

        var tryCreateEnum = factoryType.GetMethods()
            .Single(m => m.Name == "TryCreate" && m.GetParameters()[0].ParameterType == enumType);
        object?[] args = [smsKey, null];
        var success = (bool)tryCreateEnum.Invoke(factory, args)!;
        Assert.True(success);
        Assert.Equal("Demo.Sms", args[1]!.GetType().FullName);
    }

    [Fact]
    public void FactoryMethod_Requires_Static_Partial()
    {
        const string user = """
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryMethod(typeof(string))]
                            public class NotStatic
                            {
                                [FactoryCase("a")]
                                public static string A() => "a";
                            }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(FactoryMethod_Requires_Static_Partial));

        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diags = run.Results.Single().Diagnostics;
        Assert.Contains(diags, d => d.Id == "PKKF001");
    }

    [Fact]
    public void FactoryMethod_Detects_Signature_Mismatch_And_Duplicate_Keys()
    {
        const string user = """
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryMethod(typeof(int))]
                            public static partial class BadFactory
                            {
                                [FactoryCase(1)]
                                public static string One() => "one";

                                [FactoryCase(2)]
                                public static int Two() => 2;

                                [FactoryCase(1)]
                                public static string Duplicate() => "dup";
                            }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(FactoryMethod_Detects_Signature_Mismatch_And_Duplicate_Keys));

        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diags = run.Results.Single().Diagnostics;
        Assert.Contains(diags, d => d.Id == "PKKF002");
        Assert.Contains(diags, d => d.Id == "PKKF003");
    }

    [Fact]
    public void FactoryMethod_Rejects_NonStatic_Methods()
    {
        const string user = """
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryMethod(typeof(int))]
                            public static partial class BadMethods
                            {
                                [FactoryCase(1)]
                                public string Instance() => "oops";
                            }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(FactoryMethod_Rejects_NonStatic_Methods));

        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diags = run.Results.Single().Diagnostics;
        Assert.Contains(diags, d => d.Id == "PKKF006");
    }

    [Fact]
    public void FactoryMethod_Honors_String_Case_Sensitivity()
    {
        const string user = """
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryMethod(typeof(string), CaseInsensitiveStrings = false)]
                            public static partial class MimeFactory
                            {
                                [FactoryCase("json")]
                                public static string Json() => "application/json";
                            }

                            public static class Runner
                            {
                                public static string Run()
                                {
                                    var foundInsensitive = MimeFactory.TryCreate("JSON", out var value);
                                    return $"{foundInsensitive}|{value ?? "<null>"}";
                                }
                            }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(FactoryMethod_Honors_String_Case_Sensitivity));

        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.True(r.Diagnostics.IsEmpty));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var runResult = asm.GetType("Demo.Runner")!.GetMethod("Run")!.Invoke(null, null);
        Assert.Equal("False|<null>", runResult);
    }

    [Fact]
    public void FactoryClass_Requires_Interface_Or_Abstract_Base()
    {
        const string user = """
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryClass(typeof(int))]
                            public class ConcreteBase { }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(FactoryClass_Requires_Interface_Or_Abstract_Base));

        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diags = run.Results.Single().Diagnostics;
        Assert.Contains(diags, d => d.Id == "PKCF001");
    }

    [Fact]
    public void FactoryClass_Detects_Duplicate_Keys_And_Invalid_Types()
    {
        const string user = """
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryClass(typeof(int))]
                            public interface IThing { }

                            [FactoryClassKey(1)]
                            public class One : IThing { }

                            [FactoryClassKey(1)]
                            public class OneB : IThing { }

                            [FactoryClassKey("wrong")]
                            public class WrongType : IThing { }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(FactoryClass_Detects_Duplicate_Keys_And_Invalid_Types));

        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diags = run.Results.Single().Diagnostics;
        Assert.Contains(diags, d => d.Id == "PKCF004");
        Assert.Contains(diags, d => d.Id == "PKCF005");
    }

    [Fact]
    public void FactoryClass_Flags_Missing_Ctor()
    {
        const string user = """
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryClass(typeof(string))]
                            public interface IService { }

                            [FactoryClassKey("bad")]
                            public class MissingCtor : IService
                            {
                                private MissingCtor() { }
                            }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(FactoryClass_Flags_Missing_Ctor));

        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diags = run.Results.Single().Diagnostics;
        Assert.Contains(diags, d => d.Id == "PKCF006");
    }

    [Fact]
    public void FactoryClass_Respects_GenerateTryCreate_Flag()
    {
        const string user = """
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryClass(typeof(string), GenerateTryCreate = false)]
                            public interface ICommand { }

                            [FactoryClassKey("ping")]
                            public class Ping : ICommand { }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(FactoryClass_Respects_GenerateTryCreate_Flag));

        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.True(r.Diagnostics.IsEmpty));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var factoryType = asm.GetType("Demo.CommandFactory")!;
        var commandType = asm.GetType("Demo.ICommand")!;
        Assert.NotNull(factoryType.GetMethod("Create", new[] { typeof(string) }));
        Assert.Null(factoryType.GetMethod("TryCreate", new[] { typeof(string), commandType.MakeByRefType() }));
    }

    [Fact]
    public void FactoryClass_Uses_CreateAsync_When_Available()
    {
        const string user = """
                            using System.Threading.Tasks;
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryClass(typeof(string))]
                            public abstract class Service { }

                            [FactoryClassKey("async")]
                            public class AsyncService : Service
                            {
                                public static ValueTask<Service> CreateAsync() => new ValueTask<Service>(new AsyncService());
                            }

                            [FactoryClassKey("sync")]
                            public class SyncService : Service { }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(FactoryClass_Uses_CreateAsync_When_Available));

        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.True(r.Diagnostics.IsEmpty));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var factoryType = asm.GetType("Demo.ServiceFactory")!;
        dynamic factory = Activator.CreateInstance(factoryType)!;

        var sync = factoryType.GetMethod("Create")!;
        var asyncResult = sync.Invoke(factory, new object?[] { "async" });
        Assert.Equal("Demo.AsyncService", asyncResult!.GetType().FullName);

        var createAsync = factoryType.GetMethod("CreateAsync", new[] { typeof(string) })!;
        var vtObj = createAsync.Invoke(factory, new object?[] { "sync" })!;
        var awaited = vtObj.GetAwaiter().GetResult();
        Assert.Equal("Demo.SyncService", ((object)awaited).GetType().FullName);
    }

    [Fact]
    public void FactoryGenerators_Work_In_DI_Orchestrator_Scenario()
    {
        const string user = """
                            using System;
                            using System.Threading;
                            using System.Threading.Tasks;
                            using PatternKit.Generators.Factories;

                            public interface IServiceCollection
                            {
                                IServiceCollection Add(Type serviceType, Type implType);
                            }

                            public class Services : IServiceCollection
                            {
                                public IServiceCollection Add(Type serviceType, Type implType) => this;
                            }

                            [FactoryMethod(typeof(string), CreateMethodName = "ConfigureModule")]
                            public static partial class ServiceModules
                            {
                                [FactoryCase("metrics")]
                                public static IServiceCollection AddMetrics(IServiceCollection services) => services.Add(typeof(IMetrics), typeof(ConsoleMetrics));

                                [FactoryCase("workers")]
                                public static IServiceCollection AddWorkers(IServiceCollection services) => services.Add(typeof(IWorker), typeof(Worker));

                                [FactoryDefault]
                                public static IServiceCollection AddDefaults(IServiceCollection services) => services.Add(typeof(IWorker), typeof(Worker));
                            }

                            [FactoryClass(typeof(string))]
                            public interface IOrchestratorStep
                            {
                                ValueTask ExecuteAsync(IServiceCollection services, CancellationToken cancellationToken = default);
                            }

                            [FactoryClassKey("seed")]
                            public sealed class SeedStep : IOrchestratorStep
                            {
                                public ValueTask ExecuteAsync(IServiceCollection services, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
                            }

                            [FactoryClassKey("start")]
                            public sealed class StartStep : IOrchestratorStep
                            {
                                public ValueTask ExecuteAsync(IServiceCollection services, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
                            }

                            public interface IMetrics { }
                            public sealed class ConsoleMetrics : IMetrics { }
                            public interface IWorker { }
                            public sealed class Worker : IWorker { }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            user,
            assemblyName: nameof(FactoryGenerators_Work_In_DI_Orchestrator_Scenario));

        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.True(r.Diagnostics.IsEmpty));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
