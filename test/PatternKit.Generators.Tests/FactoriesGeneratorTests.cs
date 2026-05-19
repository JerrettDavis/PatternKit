using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using PatternKit.Generators.Factories;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public class FactoriesGeneratorTests
{
    [Scenario("FactoryMethod Generates Sync Create And TryCreate")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.True(r.Diagnostics.IsEmpty));
        ScenarioExpect.Contains("MimeFactory.FactoryMethod.g.cs",
            run.Results.SelectMany(r => r.GeneratedSources).Select(g => g.HintName));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var emit = updated.Emit(pe, pdb);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
        pe.Position = 0;
        pdb.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var runResult = asm.GetType("Demo.Runner")!.GetMethod("Run")!.Invoke(null, null);
        ScenarioExpect.Equal("application/json|True|application/json|False|application/octet-stream", runResult);
    }

    [Scenario("FactoryMethod Async Methods Use ValueTask")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.True(r.Diagnostics.IsEmpty));
        var hintNames = run.Results.SelectMany(r => r.GeneratedSources).Select(g => g.HintName).ToArray();
        ScenarioExpect.Contains("NumberNames.FactoryMethod.g.cs", hintNames);

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var type = asm.GetType("Demo.NumberNames")!;
        var createAsync = type.GetMethod("CreateAsync")!;
        var tryCreateAsync = type.GetMethod("TryCreateAsync")!;

        var task = (ValueTask<string>)createAsync.Invoke(null, new object?[] { 1 })!;
        ScenarioExpect.Equal("one", await task);

        var tuple = await ((ValueTask<(bool Success, string Result)>)tryCreateAsync.Invoke(null, [2])!);
        ScenarioExpect.False(tuple.Success);
        ScenarioExpect.Equal("other", tuple.Result);
    }

    [Scenario("FactoryClass Generates Create And TryCreate")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.True(r.Diagnostics.IsEmpty));
        ScenarioExpect.Contains("MessageFactory.FactoryClass.g.cs",
            run.Results.SelectMany(r => r.GeneratedSources).Select(g => g.HintName));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var emit = updated.Emit(pe, pdb);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        pe.Position = 0;
        pdb.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var factoryType = asm.GetType("Demo.MessageFactory")!;
        dynamic factory = Activator.CreateInstance(factoryType)!;

        var email = factoryType.GetMethod("Create")!.Invoke(factory, new object?[] { "email" });
        ScenarioExpect.Equal("Demo.Email", email!.GetType().FullName);

        var args = new object?[] { "sms", null };
        var success = (bool)factoryType.GetMethod("TryCreate")!.Invoke(factory, args)!;
        ScenarioExpect.True(success);
        ScenarioExpect.Equal("Demo.Sms", args[1]!.GetType().FullName);
    }

    [Scenario("FactoryClass Emits Enum Keys And Overloads")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.True(r.Diagnostics.IsEmpty));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var emit = updated.Emit(pe, pdb);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

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
        ScenarioExpect.Equal("Demo.Email", email!.GetType().FullName);

        var tryCreateEnum = factoryType.GetMethods()
            .Single(m => m.Name == "TryCreate" && m.GetParameters()[0].ParameterType == enumType);
        object?[] args = [smsKey, null];
        var success = (bool)tryCreateEnum.Invoke(factory, args)!;
        ScenarioExpect.True(success);
        ScenarioExpect.Equal("Demo.Sms", args[1]!.GetType().FullName);
    }

    [Scenario("FactoryMethod Requires Static Partial")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKKF001");
    }

    [Scenario("FactoryMethod Detects Signature Mismatch And Duplicate Keys")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKKF002");
        ScenarioExpect.Contains(diags, d => d.Id == "PKKF003");
    }

    [Scenario("FactoryMethod Rejects NonStatic Methods")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKKF006");
    }

    [Scenario("FactoryMethod Honors String Case Sensitivity")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.True(r.Diagnostics.IsEmpty));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var runResult = asm.GetType("Demo.Runner")!.GetMethod("Run")!.Invoke(null, null);
        ScenarioExpect.Equal("False|<null>", runResult);
    }

    [Scenario("FactoryClass Requires Interface Or Abstract Base")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKCF001");
    }

    [Scenario("FactoryClass Detects Duplicate Keys And Invalid Types")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKCF004");
        ScenarioExpect.Contains(diags, d => d.Id == "PKCF005");
    }

    [Scenario("FactoryClass Flags Missing Ctor")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKCF006");
    }

    [Scenario("FactoryClass Respects GenerateTryCreate Flag")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.True(r.Diagnostics.IsEmpty));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var factoryType = asm.GetType("Demo.CommandFactory")!;
        var commandType = asm.GetType("Demo.ICommand")!;
        ScenarioExpect.NotNull(factoryType.GetMethod("Create", new[] { typeof(string) }));
        ScenarioExpect.Null(factoryType.GetMethod("TryCreate", new[] { typeof(string), commandType.MakeByRefType() }));
    }

    [Scenario("FactoryClass Uses CreateAsync When Available")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.True(r.Diagnostics.IsEmpty));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var factoryType = asm.GetType("Demo.ServiceFactory")!;
        dynamic factory = Activator.CreateInstance(factoryType)!;

        var sync = factoryType.GetMethod("Create")!;
        var asyncResult = sync.Invoke(factory, new object?[] { "async" });
        ScenarioExpect.Equal("Demo.AsyncService", asyncResult!.GetType().FullName);

        var createAsync = factoryType.GetMethod("CreateAsync", new[] { typeof(string) })!;
        var vtObj = createAsync.Invoke(factory, new object?[] { "sync" })!;
        var awaited = vtObj.GetAwaiter().GetResult();
        ScenarioExpect.Equal("Demo.SyncService", ((object)awaited).GetType().FullName);
    }

    [Scenario("FactoryGenerators Work In DI Orchestrator Scenario")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.True(r.Diagnostics.IsEmpty));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("FactoryMethod Async String NoDefault WithParameters EmitsNullAndCaseBranches")]
    [Fact]
    public void FactoryMethod_Async_String_NoDefault_WithParameters_EmitsNullAndCaseBranches()
    {
        const string user = """
                            using System.Threading.Tasks;
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryMethod(typeof(string), CreateMethodName = "Resolve", CaseInsensitiveStrings = true)]
                            public static partial class FormatterFactory
                            {
                                [FactoryCase("json")]
                                public static Task<string> JsonAsync(string payload, int indent) => Task.FromResult($"json:{payload}:{indent}");

                                [FactoryCase("xml")]
                                public static ValueTask<string> XmlAsync(string payload, int indent) => ValueTask.FromResult($"xml:{payload}:{indent}");
                            }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(user, assemblyName: nameof(FactoryMethod_Async_String_NoDefault_WithParameters_EmitsNullAndCaseBranches));
        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, r => ScenarioExpect.True(r.Diagnostics.IsEmpty));
        var generated = run.Results.SelectMany(r => r.GeneratedSources)
            .Single(g => g.HintName == "FormatterFactory.FactoryMethod.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("if (key is null) throw new global::System.ArgumentNullException", generated);
        ScenarioExpect.Contains("StringComparison.OrdinalIgnoreCase", generated);
        ScenarioExpect.Contains("ResolveAsync", generated);
        ScenarioExpect.Contains("return (false, default!);", generated);
        ScenarioExpect.Contains("JsonAsync(payload, indent)", generated);
        ScenarioExpect.Contains("XmlAsync(payload, indent)", generated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("FactoryMethod Async Numeric NoDefault EmitsSwitchBranches")]
    [Fact]
    public void FactoryMethod_Async_Numeric_NoDefault_EmitsSwitchBranches()
    {
        const string user = """
                            using System.Threading.Tasks;
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryMethod(typeof(int))]
                            public static partial class NumberFactory
                            {
                                [FactoryCase(1)]
                                public static Task<string> OneAsync() => Task.FromResult("one");

                                [FactoryCase(2)]
                                public static ValueTask<string> TwoAsync() => ValueTask.FromResult("two");
                            }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(user, assemblyName: nameof(FactoryMethod_Async_Numeric_NoDefault_EmitsSwitchBranches));
        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, r => ScenarioExpect.True(r.Diagnostics.IsEmpty));
        var generated = run.Results.SelectMany(r => r.GeneratedSources)
            .Single(g => g.HintName == "NumberFactory.FactoryMethod.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("switch (key)", generated);
        ScenarioExpect.Contains("case 1:", generated);
        ScenarioExpect.Contains("default:", generated);
        ScenarioExpect.Contains("throw new global::System.ArgumentOutOfRangeException(nameof(key));", generated);
        ScenarioExpect.Contains("return (false, default!);", generated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("FactoryClass AsyncEnumKeysAndFactoryMethods CoverCreationBranches")]
    [Fact]
    public void FactoryClass_AsyncEnumKeysAndFactoryMethods_CoverCreationBranches()
    {
        const string user = """
                            using System.Threading.Tasks;
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            public enum Channel { Sms = 2, Push = 7 }

                            [FactoryClass(typeof(Channel), GenerateEnumKeys = true, FactoryTypeName = "ChannelFactory")]
                            public abstract class ChannelHandler { }

                            [FactoryClassKey(Channel.Sms)]
                            public sealed class SmsHandler : ChannelHandler
                            {
                                public static Task<ChannelHandler> CreateAsync() => Task.FromResult<ChannelHandler>(new SmsHandler());
                            }

                            [FactoryClassKey(Channel.Push)]
                            public sealed class PushHandler : ChannelHandler
                            {
                                public static ValueTask<ChannelHandler> CreateAsync() => ValueTask.FromResult<ChannelHandler>(new PushHandler());
                            }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(user, assemblyName: nameof(FactoryClass_AsyncEnumKeysAndFactoryMethods_CoverCreationBranches));
        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, r => ScenarioExpect.True(r.Diagnostics.IsEmpty));
        var generated = run.Results.SelectMany(r => r.GeneratedSources)
            .Single(g => g.HintName == "ChannelFactory.FactoryClass.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("public global::System.Threading.Tasks.ValueTask<global::Demo.ChannelHandler> CreateAsync(Keys key)", generated);
        ScenarioExpect.Contains("TryCreateAsync(Keys key)", generated);
        ScenarioExpect.Contains("return CreateAsync(MapKey(key));", generated);
        ScenarioExpect.Contains("new global::System.Threading.Tasks.ValueTask<global::Demo.ChannelHandler>", generated);
        ScenarioExpect.Contains("SmsHandler.CreateAsync", generated);
        ScenarioExpect.Contains("PushHandler.CreateAsync", generated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("FactoryClass StringKeysGenerateStableEnumNamesAndNullComparers")]
    [Fact]
    public void FactoryClass_StringKeysGenerateStableEnumNamesAndNullComparers()
    {
        const string user = """
                            using PatternKit.Generators.Factories;

                            namespace Demo;

                            [FactoryClass(typeof(string), GenerateEnumKeys = true, FactoryTypeName = "TransportFactory")]
                            public interface ITransport { }

                            [FactoryClassKey("1-http")]
                            public sealed class Http1 : ITransport { }

                            [FactoryClassKey("1_http")]
                            public sealed class Http2 : ITransport { }

                            [FactoryClassKey("")]
                            public sealed class Empty : ITransport { }
                            """;

        var comp = RoslynTestHelpers.CreateCompilation(user, assemblyName: nameof(FactoryClass_StringKeysGenerateStableEnumNamesAndNullComparers));
        var gen = new FactoriesGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, r => ScenarioExpect.True(r.Diagnostics.IsEmpty));
        var generated = run.Results.SelectMany(r => r.GeneratedSources)
            .Single(g => g.HintName == "TransportFactory.FactoryClass.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("Key1Http", generated);
        ScenarioExpect.Contains("Key1Http2", generated);
        ScenarioExpect.Contains("Key", generated);
        ScenarioExpect.Contains("MapKey", generated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("InternalHelpers FormatKeysAsyncReturnsAndArguments")]
    [Fact]
    public void InternalHelpers_FormatKeysAsyncReturnsAndArguments()
    {
        const string user = """
            using System.Threading.Tasks;
            using PatternKit.Generators.Factories;

            namespace Demo;

            public enum Channel { Email = 1, Sms = 2 }
            public interface IMessage { }
            public abstract class MessageBase : IMessage { }
            public sealed class EmailMessage : MessageBase { }
            public sealed class Other { }

            public static class KeySamples
            {
                [FactoryCase("hello-world")]
                public static string StringKey() => "";

                [FactoryCase(true)]
                public static string BoolKey() => "";

                [FactoryCase(42)]
                public static string NumberKey() => "";

                [FactoryCase(Channel.Email)]
                public static string EnumKey() => "";
            }

            public static class SignatureSamples
            {
                public static Task<string> TaskResult(ref int count, in string name, out bool ok)
                {
                    ok = true;
                    return Task.FromResult(name + count);
                }
            }
            """;

        var compilation = RoslynTestHelpers.CreateCompilation(user, nameof(InternalHelpers_FormatKeysAsyncReturnsAndArguments));
        var keySamples = compilation.GetTypeByMetadataName("Demo.KeySamples")!;
        var signatureSamples = compilation.GetTypeByMetadataName("Demo.SignatureSamples")!;
        var message = compilation.GetTypeByMetadataName("Demo.IMessage")!;
        var messageBase = compilation.GetTypeByMetadataName("Demo.MessageBase")!;
        var email = compilation.GetTypeByMetadataName("Demo.EmailMessage")!;
        var other = compilation.GetTypeByMetadataName("Demo.Other")!;
        var stringType = compilation.GetSpecialType(SpecialType.System_String);
        var boolType = compilation.GetSpecialType(SpecialType.System_Boolean);
        var intType = compilation.GetSpecialType(SpecialType.System_Int32);

        var constants = keySamples.GetMembers().OfType<IMethodSymbol>()
            .ToDictionary(
                static method => method.Name,
                static method => method.GetAttributes().Single().ConstructorArguments.Single());

        ScenarioExpect.Equal("\"hello-world\"", InvokeFactoryHelper<string>("ToLiteral", constants["StringKey"]));
        ScenarioExpect.Equal("true", InvokeFactoryHelper<string>("ToLiteral", constants["BoolKey"]));
        ScenarioExpect.Equal("42", InvokeFactoryHelper<string>("ToLiteral", constants["NumberKey"]));
        ScenarioExpect.Equal("global::Demo.Channel.Email", InvokeFactoryHelper<string>("ToLiteral", constants["EnumKey"]));

        ScenarioExpect.True(InvokeFactoryHelper<bool>("IsKeyCompatible", compilation, stringType, constants["StringKey"]));
        ScenarioExpect.True(InvokeFactoryHelper<bool>("IsKeyCompatible", compilation, boolType, constants["BoolKey"]));
        ScenarioExpect.True(InvokeFactoryHelper<bool>("IsKeyCompatible", compilation, intType, constants["NumberKey"]));
        ScenarioExpect.False(InvokeFactoryHelper<bool>("IsKeyCompatible", compilation, stringType, constants["NumberKey"]));
        ScenarioExpect.True(InvokeFactoryHelper<bool>("NeedsNullCheck", stringType));
        ScenarioExpect.False(InvokeFactoryHelper<bool>("NeedsNullCheck", intType));
        ScenarioExpect.True(InvokeFactoryHelper<bool>("IsStringType", stringType));

        var existing = new HashSet<string>(StringComparer.Ordinal);
        ScenarioExpect.Equal("HelloWorld", InvokeFactoryHelper<string>("BuildEnumMemberName", constants["StringKey"], stringType, existing));
        ScenarioExpect.Equal("True", InvokeFactoryHelper<string>("BuildEnumMemberName", constants["BoolKey"], boolType, existing));
        ScenarioExpect.Equal("Key42", InvokeFactoryHelper<string>("BuildEnumMemberName", constants["NumberKey"], intType, existing));
        ScenarioExpect.Equal("Email", InvokeFactoryHelper<string>("BuildEnumMemberName", constants["EnumKey"], constants["EnumKey"].Type!, existing));
        ScenarioExpect.Equal("HelloWorld2", InvokeFactoryHelper<string>("BuildEnumMemberName", constants["StringKey"], stringType, existing));

        ScenarioExpect.True(InvokeFactoryHelper<bool>("Implements", email, message));
        ScenarioExpect.True(InvokeFactoryHelper<bool>("Implements", email, messageBase));
        ScenarioExpect.False(InvokeFactoryHelper<bool>("Implements", other, message));
        ScenarioExpect.Equal("MessageFactory", InvokeFactoryHelper<string>("BuildDefaultFactoryName", message));
        ScenarioExpect.Equal("MessageBaseFactory", InvokeFactoryHelper<string>("BuildDefaultFactoryName", messageBase));

        var taskMethod = (IMethodSymbol)signatureSamples.GetMembers("TaskResult").Single();
        var taskSignature = InvokeFactoryHelper<object>("BuildSignature", taskMethod, compilation);
        var parameterArray = (System.Collections.Immutable.ImmutableArray<IParameterSymbol>)taskSignature.GetType().GetProperty("Parameters")!.GetValue(taskSignature)!;
        ScenarioExpect.Contains("ref int count", InvokeFactoryHelper<string>("BuildParameterList", parameterArray));
        ScenarioExpect.Contains("in string name", InvokeFactoryHelper<string>("BuildParameterList", parameterArray));
        ScenarioExpect.Contains("out bool ok", InvokeFactoryHelper<string>("BuildParameterList", parameterArray));
        ScenarioExpect.Equal("ref count, in name, out ok", InvokeFactoryHelper<string>("BuildArgumentList", parameterArray));

        var asyncKindType = typeof(FactoriesGenerator).GetNestedType("AsyncKind", System.Reflection.BindingFlags.NonPublic)!;
        var sync = Enum.Parse(asyncKindType, "Sync");
        var task = Enum.Parse(asyncKindType, "Task");
        var valueTask = Enum.Parse(asyncKindType, "ValueTask");
        ScenarioExpect.Equal("return invocation();", InvokeFactoryHelper<string>("BuildAsyncReturn", valueTask, "string", "invocation()"));
        ScenarioExpect.Equal("return new global::System.Threading.Tasks.ValueTask<string>(invocation());", InvokeFactoryHelper<string>("BuildAsyncReturn", task, "string", "invocation()"));
        ScenarioExpect.Equal("return global::System.Threading.Tasks.ValueTask.FromResult<string>(invocation());", InvokeFactoryHelper<string>("BuildAsyncReturn", sync, "string", "invocation()"));
        ScenarioExpect.Equal("invocation()", InvokeFactoryHelper<string>("BuildSyncValue", sync, "invocation()"));
        ScenarioExpect.Equal("invocation().GetAwaiter().GetResult()", InvokeFactoryHelper<string>("BuildSyncValue", task, "invocation()"));
        ScenarioExpect.Equal("invocation()", InvokeFactoryHelper<string>("BuildAwaitedValue", sync, "invocation()"));
        ScenarioExpect.Equal("await invocation()", InvokeFactoryHelper<string>("BuildAwaitedValue", valueTask, "invocation()"));
    }

    private static T InvokeFactoryHelper<T>(string name, params object?[] args)
    {
        var method = typeof(FactoriesGenerator)
            .GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name == name && m.GetParameters().Length == args.Length)
            .Single();

        return (T)method.Invoke(null, args)!;
    }
}
