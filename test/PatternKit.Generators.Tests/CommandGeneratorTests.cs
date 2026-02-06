using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

public class CommandGeneratorTests
{
    [Fact]
    public void Generates_Command_Without_Diagnostics()
    {
        var source = """
            using PatternKit.Generators.Command;

            namespace TestNamespace;

            [Command]
            public partial class PrintCommand
            {
                public string Message { get; set; } = "";

                [CommandHandler]
                public void Handle(PrintCommand cmd)
                {
                    System.Console.WriteLine(cmd.Message);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Command_Without_Diagnostics));
        _ = RoslynTestHelpers.Run(comp, new CommandGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("PrintCommand.Command.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generated_Execute_Method_Compiles()
    {
        var source = """
            using PatternKit.Generators.Command;
            using System.Collections.Generic;

            namespace TestNamespace;

            [Command]
            public partial class LogCommand
            {
                public string Message { get; set; } = "";
                public List<string> Log { get; set; } = new();

                [CommandHandler]
                public void Handle(LogCommand cmd)
                {
                    cmd.Log.Add(cmd.Message);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generated_Execute_Method_Compiles));
        _ = RoslynTestHelpers.Run(comp, new CommandGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Command"))
            .SourceText.ToString();

        Assert.Contains("public static void Execute(", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Async_Execute_With_ForceAsync()
    {
        var source = """
            using PatternKit.Generators.Command;

            namespace TestNamespace;

            [Command(ForceAsync = true)]
            public partial class SyncCommand
            {
                public string Data { get; set; } = "";

                [CommandHandler]
                public void Handle(SyncCommand cmd)
                {
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Async_Execute_With_ForceAsync));
        _ = RoslynTestHelpers.Run(comp, new CommandGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Command"))
            .SourceText.ToString();

        Assert.Contains("ExecuteAsync(", generatedSource);
        Assert.Contains("ValueTask", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Async_Execute_From_Async_Handler()
    {
        var source = """
            using PatternKit.Generators.Command;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            [Command]
            public partial class AsyncCommand
            {
                public string Data { get; set; } = "";

                [CommandHandler]
                public ValueTask HandleAsync(AsyncCommand cmd, CancellationToken ct)
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Async_Execute_From_Async_Handler));
        _ = RoslynTestHelpers.Run(comp, new CommandGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Command"))
            .SourceText.ToString();

        Assert.Contains("async System.Threading.Tasks.ValueTask ExecuteAsync(", generatedSource);
        Assert.Contains("ConfigureAwait(false)", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Reports_Error_When_Type_Not_Partial()
    {
        var source = """
            using PatternKit.Generators.Command;

            namespace TestNamespace;

            [Command]
            public class PrintCommand
            {
                [CommandHandler]
                public void Handle(PrintCommand cmd) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Type_Not_Partial));
        _ = RoslynTestHelpers.Run(comp, new CommandGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKCMD001");
    }

    [Fact]
    public void Reports_Error_When_No_Handler()
    {
        var source = """
            using PatternKit.Generators.Command;

            namespace TestNamespace;

            [Command]
            public partial class PrintCommand
            {
                public string Message { get; set; } = "";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_No_Handler));
        _ = RoslynTestHelpers.Run(comp, new CommandGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKCMD002");
    }

    [Fact]
    public void Reports_Error_When_Multiple_Handlers()
    {
        var source = """
            using PatternKit.Generators.Command;

            namespace TestNamespace;

            [Command]
            public partial class PrintCommand
            {
                [CommandHandler]
                public void Handle(PrintCommand cmd) { }

                [CommandHandler]
                public void HandleAlt(PrintCommand cmd) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Multiple_Handlers));
        _ = RoslynTestHelpers.Run(comp, new CommandGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKCMD003");
    }

    [Fact]
    public void Reports_Error_When_Invalid_Handler_Signature()
    {
        var source = """
            using PatternKit.Generators.Command;

            namespace TestNamespace;

            [Command]
            public partial class PrintCommand
            {
                [CommandHandler]
                public void Handle() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Invalid_Handler_Signature));
        _ = RoslynTestHelpers.Run(comp, new CommandGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKCMD004");
    }

    [Fact]
    public void Generates_CommandHost_Without_Diagnostics()
    {
        var source = """
            using PatternKit.Generators.Command;

            namespace TestNamespace;

            [CommandHost]
            public static partial class OrderCommands
            {
                [CommandCase]
                public static void CreateOrder(string orderId, decimal amount)
                {
                }

                [CommandCase]
                public static void CancelOrder(string orderId)
                {
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_CommandHost_Without_Diagnostics));
        _ = RoslynTestHelpers.Run(comp, new CommandGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("OrderCommands.Command.g.cs", names);

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Command"))
            .SourceText.ToString();

        Assert.Contains("ExecuteCancelOrder(", generatedSource);
        Assert.Contains("ExecuteCreateOrder(", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Reports_Error_When_Host_Not_Static_Partial()
    {
        var source = """
            using PatternKit.Generators.Command;

            namespace TestNamespace;

            [CommandHost]
            public class OrderCommands
            {
                [CommandCase]
                public static void CreateOrder(string orderId) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Host_Not_Static_Partial));
        _ = RoslynTestHelpers.Run(comp, new CommandGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKCMD006");
    }

    [Fact]
    public void Generates_Command_For_Struct()
    {
        var source = """
            using PatternKit.Generators.Command;

            namespace TestNamespace;

            [Command]
            public partial struct IncrementCommand
            {
                public int Amount { get; set; }

                [CommandHandler]
                public static void Handle(IncrementCommand cmd)
                {
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Command_For_Struct));
        _ = RoslynTestHelpers.Run(comp, new CommandGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
