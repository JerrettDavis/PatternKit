using PatternKit.Generators.Command;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public class CommandGeneratorTests
{
    [Scenario("GeneratesCommandExecutor")]
    [Fact]
    public void GeneratesCommandExecutor()
    {
        const string source = """
            using PatternKit.Generators.Command;
            using System;

            namespace TestNamespace;

            [Command]
            public readonly partial record struct RenameUser(Guid UserId, string NewName);

            public sealed class UserService
            {
                [CommandHandler]
                public void Handle(in RenameUser command) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GeneratesCommandExecutor));
        var gen = new CommandGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).Single(s => s.HintName == "RenameUser.Command.g.cs").SourceText.ToString();
        ScenarioExpect.Contains("public readonly partial struct RenameUserCommand", generated);
        ScenarioExpect.Contains("public static void Execute(global::TestNamespace.UserService handler, in global::TestNamespace.RenameUser command)", generated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ReportsMissingHandler")]
    [Fact]
    public void ReportsMissingHandler()
    {
        const string source = """
            using PatternKit.Generators.Command;

            namespace TestNamespace;

            [Command]
            public readonly partial record struct RenameUser(string NewName);
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReportsMissingHandler));
        var gen = new CommandGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);
        ScenarioExpect.Contains(result.Results.SelectMany(r => r.Diagnostics), d => d.Id == "PKCMD002");
    }

    [Scenario("GeneratesAsyncCommandExecutorWithCancellationToken")]
    [Fact]
    public void GeneratesAsyncCommandExecutorWithCancellationToken()
    {
        const string source = """
            using PatternKit.Generators.Command;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            [Command(CommandTypeName = "RefreshProjection")]
            public readonly partial record struct RefreshUser(string UserId);

            public sealed class ProjectionService
            {
                [CommandHandler]
                public ValueTask Handle(RefreshUser command, CancellationToken ct) => ValueTask.CompletedTask;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GeneratesAsyncCommandExecutorWithCancellationToken));
        var gen = new CommandGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).Single(s => s.HintName == "RefreshUser.Command.g.cs").SourceText.ToString();
        ScenarioExpect.Contains("public static global::System.Threading.Tasks.ValueTask ExecuteAsync", generated);
        ScenarioExpect.Contains("handler.Handle(command, ct)", generated);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("ReportsCommandShapeDiagnostics")]
    [Theory]
    [InlineData("public readonly record struct RenameUser(string NewName);", "PKCMD001")]
    [InlineData("""
        public readonly partial record struct RenameUser(string NewName);
        public sealed class UserService
        {
            [CommandHandler] public void Handle(RenameUser command) { }
            [CommandHandler] public void HandleAgain(RenameUser command) { }
        }
        """, "PKCMD003")]
    [InlineData("""
        public readonly partial record struct RenameUser(string NewName);
        public sealed class UserService
        {
            [CommandHandler] public string Handle(RenameUser command) => command.NewName;
        }
        """, "PKCMD004")]
    public void ReportsCommandShapeDiagnostics(string commandSource, string diagnosticId)
    {
        var source = $$"""
            using PatternKit.Generators.Command;

            namespace TestNamespace;

            [Command]
            {{commandSource}}
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReportsCommandShapeDiagnostics) + diagnosticId);
        var gen = new CommandGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        ScenarioExpect.Contains(result.Results.SelectMany(r => r.Diagnostics), d => d.Id == diagnosticId);
    }
}
