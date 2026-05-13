using PatternKit.Generators.Command;

namespace PatternKit.Generators.Tests;

public class CommandGeneratorTests
{
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

        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).Single(s => s.HintName == "RenameUser.Command.g.cs").SourceText.ToString();
        Assert.Contains("public readonly partial struct RenameUserCommand", generated);
        Assert.Contains("public static void Execute(global::TestNamespace.UserService handler, in global::TestNamespace.RenameUser command)", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

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
        Assert.Contains(result.Results.SelectMany(r => r.Diagnostics), d => d.Id == "PKCMD002");
    }
}
