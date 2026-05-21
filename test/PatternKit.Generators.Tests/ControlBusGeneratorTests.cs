using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ControlBusGeneratorTests
{
    [Scenario("GeneratesControlBusFactory")]
    [Fact]
    public void GeneratesControlBusFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.ControlBus;

            namespace MyApp;
            public sealed record Command(string Name);

            [GenerateControlBus(typeof(Command), FactoryName = "Build", BusName = "ops")]
            public static partial class OpsBus
            {
                [ControlBusCommand("pause", "pause-handler", 20)]
                private static ControlBusResult<Command> Pause(Message<Command> message, MessageContext context)
                    => ControlBusResult<Command>.Success();

                [ControlBusCommand("resume", "resume-handler", 10)]
                private static ControlBusResult<Command> Resume(Message<Command> message, MessageContext context)
                    => ControlBusResult<Command>.Success();
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesControlBusFactory));
        var gen = new ControlBusGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("OpsBus.ControlBus.g.cs", generated.HintName);
        ScenarioExpect.Contains("ControlBus<global::MyApp.Command>", text);
        ScenarioExpect.True(text.IndexOf("resume", StringComparison.Ordinal) < text.IndexOf("pause", StringComparison.Ordinal));
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("ReportsControlBusDiagnostics")]
    [Theory]
    [InlineData("public static class OpsBus;", "PKCTL001")]
    [InlineData("public static partial class OpsBus;", "PKCTL002")]
    public void ReportsControlBusDiagnostics(string declaration, string expected)
    {
        var source = $$"""
            using PatternKit.Generators.Messaging;
            namespace MyApp;
            public sealed record Command(string Name);
            [GenerateControlBus(typeof(Command))]
            {{declaration}}
            """;

        var comp = CreateCompilation(source, nameof(ReportsControlBusDiagnostics) + expected);
        var gen = new ControlBusGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    [Scenario("ReportsInvalidControlBusHandler")]
    [Fact]
    public void ReportsInvalidControlBusHandler()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record Command(string Name);
            [GenerateControlBus(typeof(Command))]
            public static partial class OpsBus
            {
                [ControlBusCommand("pause", "pause")]
                private static string Pause(Message<Command> message, MessageContext context) => "ok";
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsInvalidControlBusHandler));
        var gen = new ControlBusGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKCTL003", diagnostic.Id);
    }

    [Scenario("ReportsDuplicateControlBusCommand")]
    [Fact]
    public void ReportsDuplicateControlBusCommand()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            using PatternKit.Messaging.ControlBus;
            namespace MyApp;
            public sealed record Command(string Name);
            [GenerateControlBus(typeof(Command))]
            public static partial class OpsBus
            {
                [ControlBusCommand("pause", "one", 10)]
                private static ControlBusResult<Command> One(Message<Command> message, MessageContext context) => ControlBusResult<Command>.Success();
                [ControlBusCommand("pause", "two", 20)]
                private static ControlBusResult<Command> Two(Message<Command> message, MessageContext context) => ControlBusResult<Command>.Success();
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsDuplicateControlBusCommand));
        var gen = new ControlBusGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKCTL004", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.ControlBus.ControlBus<>).Assembly.Location));
}
