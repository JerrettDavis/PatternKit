using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

/// <summary>
/// Tests for the Chain of Responsibility / Pipeline pattern generator.
/// </summary>
public class ChainGeneratorTests
{
    #region Responsibility Model Tests

    [Fact]
    public void Generates_Responsibility_Chain_Without_Diagnostics()
    {
        var source = """
            using PatternKit.Generators.Chain;

            namespace TestApp;

            public record struct Request(string Path, string Method);

            [Chain(Model = ChainModel.Responsibility)]
            public partial class RequestChain
            {
                [ChainHandler(0)]
                private bool TryHandleGet(in Request input, out string output)
                {
                    if (input.Method == "GET") { output = "GET handled"; return true; }
                    output = default!; return false;
                }

                [ChainHandler(1)]
                private bool TryHandlePost(in Request input, out string output)
                {
                    if (input.Method == "POST") { output = "POST handled"; return true; }
                    output = default!; return false;
                }

                [ChainDefault]
                private string DefaultHandler(in Request input)
                {
                    return "Default: " + input.Method;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Responsibility_Chain_Without_Diagnostics));
        _ = RoslynTestHelpers.Run(comp, new ChainGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("RequestChain.Chain.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Responsibility_Chain_Generates_TryHandle_And_Handle()
    {
        var source = """
            using PatternKit.Generators.Chain;

            namespace TestApp;

            [Chain]
            public partial class NumberChain
            {
                [ChainHandler(0)]
                private bool TryHandlePositive(in int input, out string output)
                {
                    if (input > 0) { output = "positive"; return true; }
                    output = default!; return false;
                }

                [ChainHandler(1)]
                private bool TryHandleNegative(in int input, out string output)
                {
                    if (input < 0) { output = "negative"; return true; }
                    output = default!; return false;
                }

                [ChainDefault]
                private string HandleZero(in int input) => "zero";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Responsibility_Chain_Generates_TryHandle_And_Handle));
        _ = RoslynTestHelpers.Run(comp, new ChainGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("NumberChain"))
            .SourceText.ToString();

        Assert.Contains("public bool TryHandle(", generatedSource);
        Assert.Contains("public string Handle(", generatedSource);
        Assert.Contains("TryHandlePositive(in input, out output)", generatedSource);
        Assert.Contains("TryHandleNegative(in input, out output)", generatedSource);
        Assert.Contains("HandleZero(in input)", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Responsibility_Without_Default_Generates_Warning()
    {
        var source = """
            using PatternKit.Generators.Chain;

            namespace TestApp;

            [Chain]
            public partial class NoDefaultChain
            {
                [ChainHandler(0)]
                private bool TryHandleOne(in int input, out string output)
                {
                    if (input == 1) { output = "one"; return true; }
                    output = default!; return false;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Responsibility_Without_Default_Generates_Warning));
        _ = RoslynTestHelpers.Run(comp, new ChainGenerator(), out var run, out var updated);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKCH007");

        // Should still generate code (warning, not error)
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("NoDefaultChain.Chain.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Pipeline Model Tests

    [Fact]
    public void Generates_Pipeline_Chain_Without_Diagnostics()
    {
        var source = """
            using System;
            using PatternKit.Generators.Chain;

            namespace TestApp;

            [Chain(Model = ChainModel.Pipeline)]
            public partial class LoggingPipeline
            {
                [ChainHandler(0)]
                private string AddTimestamp(in string input, Func<string, string> next)
                {
                    return "[" + System.DateTime.UtcNow.ToString("o") + "] " + next(input);
                }

                [ChainHandler(1)]
                private string AddPrefix(in string input, Func<string, string> next)
                {
                    return "LOG: " + next(input);
                }

                [ChainTerminal]
                private string Format(in string input) => input.ToUpperInvariant();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Pipeline_Chain_Without_Diagnostics));
        _ = RoslynTestHelpers.Run(comp, new ChainGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("LoggingPipeline.Chain.g.cs", names);

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("LoggingPipeline"))
            .SourceText.ToString();

        Assert.Contains("public string Handle(in string input)", generatedSource);
        Assert.Contains("Format(in arg)", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Diagnostic Tests

    [Fact]
    public void Reports_Error_When_Type_Not_Partial()
    {
        var source = """
            using PatternKit.Generators.Chain;

            namespace TestApp;

            [Chain]
            public class NotPartialChain
            {
                [ChainHandler(0)]
                private bool TryHandleOne(in int input, out string output)
                {
                    output = "one"; return true;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Type_Not_Partial));
        _ = RoslynTestHelpers.Run(comp, new ChainGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKCH001");
    }

    [Fact]
    public void Reports_Error_When_No_Handlers()
    {
        var source = """
            using PatternKit.Generators.Chain;

            namespace TestApp;

            [Chain]
            public partial class EmptyChain
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_No_Handlers));
        _ = RoslynTestHelpers.Run(comp, new ChainGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKCH002");
    }

    [Fact]
    public void Reports_Error_When_Duplicate_Order()
    {
        var source = """
            using PatternKit.Generators.Chain;

            namespace TestApp;

            [Chain]
            public partial class DuplicateChain
            {
                [ChainHandler(0)]
                private bool Handler1(in int input, out string output)
                { output = "a"; return true; }

                [ChainHandler(0)]
                private bool Handler2(in int input, out string output)
                { output = "b"; return true; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Duplicate_Order));
        _ = RoslynTestHelpers.Run(comp, new ChainGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKCH003");
    }

    [Fact]
    public void Reports_Error_When_Invalid_Responsibility_Signature()
    {
        var source = """
            using PatternKit.Generators.Chain;

            namespace TestApp;

            [Chain]
            public partial class BadSigChain
            {
                [ChainHandler(0)]
                private string BadHandler(in int input)
                {
                    return "bad";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Invalid_Responsibility_Signature));
        _ = RoslynTestHelpers.Run(comp, new ChainGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKCH004");
    }

    [Fact]
    public void Reports_Error_When_Pipeline_Missing_Terminal()
    {
        var source = """
            using System;
            using PatternKit.Generators.Chain;

            namespace TestApp;

            [Chain(Model = ChainModel.Pipeline)]
            public partial class NoTerminalPipeline
            {
                [ChainHandler(0)]
                private string Handler(in string input, Func<string, string> next)
                    => next(input);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Pipeline_Missing_Terminal));
        _ = RoslynTestHelpers.Run(comp, new ChainGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKCH005");
    }

    [Fact]
    public void Reports_Error_When_Pipeline_Multiple_Terminals()
    {
        var source = """
            using System;
            using PatternKit.Generators.Chain;

            namespace TestApp;

            [Chain(Model = ChainModel.Pipeline)]
            public partial class MultiTerminalPipeline
            {
                [ChainHandler(0)]
                private string Handler(in string input, Func<string, string> next) => next(input);

                [ChainTerminal]
                private string Terminal1(in string input) => input;

                [ChainTerminal]
                private string Terminal2(in string input) => input;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Pipeline_Multiple_Terminals));
        _ = RoslynTestHelpers.Run(comp, new ChainGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKCH006");
    }

    #endregion

    #region Custom Method Names

    [Fact]
    public void Generates_Chain_With_Custom_Method_Names()
    {
        var source = """
            using PatternKit.Generators.Chain;

            namespace TestApp;

            [Chain(HandleMethodName = "Process", TryHandleMethodName = "TryProcess")]
            public partial class CustomChain
            {
                [ChainHandler(0)]
                private bool TryHandleOne(in int input, out string output)
                {
                    if (input == 1) { output = "one"; return true; }
                    output = default!; return false;
                }

                [ChainDefault]
                private string FallbackHandler(in int input) => "default";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Chain_With_Custom_Method_Names));
        _ = RoslynTestHelpers.Run(comp, new ChainGenerator(), out var run, out var updated);

        // Filter out PKCH007 warning (missing default is actually present, but we still want to check generation)
        var errors = run.Results.SelectMany(r => r.Diagnostics).Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(errors);

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("CustomChain"))
            .SourceText.ToString();

        Assert.Contains("public bool TryProcess(", generatedSource);
        Assert.Contains("public string Process(", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Struct Support

    [Fact]
    public void Generates_Chain_For_Struct()
    {
        var source = """
            using PatternKit.Generators.Chain;

            namespace TestApp;

            [Chain]
            public partial struct ValueChain
            {
                [ChainHandler(0)]
                private bool TryHandlePositive(in int input, out string output)
                {
                    if (input > 0) { output = "pos"; return true; }
                    output = default!; return false;
                }

                [ChainDefault]
                private string HandleOther(in int input) => "other";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Chain_For_Struct));
        _ = RoslynTestHelpers.Run(comp, new ChainGenerator(), out var run, out var updated);

        var errors = run.Results.SelectMany(r => r.Diagnostics).Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(errors);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion
}
