using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

public class ComposerGeneratorTests
{
    [Fact]
    public void BasicSyncPipeline_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private Response Auth(in Request req, System.Func<Request, Response> next)
                {
                    if (req.Path == "/forbidden")
                        return new Response(403);
                    return next(req);
                }

                [ComposeStep(1)]
                private Response Logging(in Request req, System.Func<Request, Response> next)
                {
                    System.Console.WriteLine($"Request: {req.Path}");
                    return next(req);
                }

                [ComposeTerminal]
                private Response Terminal(in Request req) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(BasicSyncPipeline_GeneratesCorrectly));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Confirm we generated the expected file
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("RequestPipeline.Composer.g.cs", names);

        // Verify the generated source contains Invoke method
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("public global::PatternKit.Examples.Response Invoke(in global::PatternKit.Examples.Request input)", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void AsyncPipeline_WithValueTask_GeneratesCorrectly()
    {
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial class AsyncRequestPipeline
            {
                [ComposeStep(0)]
                private async ValueTask<Response> AuthAsync(Request req, Func<Request, ValueTask<Response>> next, CancellationToken ct)
                {
                    await Task.Delay(10, ct);
                    if (req.Path == "/forbidden")
                        return new Response(403);
                    return await next(req);
                }

                [ComposeTerminal]
                private ValueTask<Response> TerminalAsync(Request req, CancellationToken ct) => 
                    new ValueTask<Response>(new Response(200));
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(AsyncPipeline_WithValueTask_GeneratesCorrectly));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Confirm we generated the expected file
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("AsyncRequestPipeline.Composer.g.cs", names);

        // Verify the generated source contains InvokeAsync method
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("InvokeAsync", generatedSource);
        Assert.Contains("ValueTask", generatedSource);
        Assert.Contains("CancellationToken", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void NotPartial_ProducesDiagnostic()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public class RequestPipeline  // Missing 'partial'
            {
                [ComposeStep(0)]
                private Response Step(in Request req, System.Func<Request, Response> next) => next(req);

                [ComposeTerminal]
                private Response Terminal(in Request req) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(NotPartial_ProducesDiagnostic));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKCOM001 diagnostic
        var diagnostics = result.Results[0].Diagnostics;
        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "PKCOM001");
    }

    [Fact]
    public void NoSteps_ProducesDiagnostic()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial class RequestPipeline
            {
                [ComposeTerminal]
                private Response Terminal(in Request req) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(NoSteps_ProducesDiagnostic));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKCOM002 diagnostic
        var diagnostics = result.Results[0].Diagnostics;
        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "PKCOM002");
    }

    [Fact]
    public void NoTerminal_ProducesDiagnostic()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private Response Step(in Request req, System.Func<Request, Response> next) => next(req);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(NoTerminal_ProducesDiagnostic));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKCOM004 diagnostic
        var diagnostics = result.Results[0].Diagnostics;
        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "PKCOM004");
    }

    [Fact]
    public void MultipleTerminals_ProducesDiagnostic()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private Response Step(in Request req, System.Func<Request, Response> next) => next(req);

                [ComposeTerminal]
                private Response Terminal1(in Request req) => new(200);

                [ComposeTerminal]
                private Response Terminal2(in Request req) => new(404);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(MultipleTerminals_ProducesDiagnostic));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKCOM005 diagnostic
        var diagnostics = result.Results[0].Diagnostics;
        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "PKCOM005");
    }

    [Fact]
    public void DuplicateOrder_ProducesDiagnostic()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private Response Step1(in Request req, System.Func<Request, Response> next) => next(req);

                [ComposeStep(0)]
                private Response Step2(in Request req, System.Func<Request, Response> next) => next(req);

                [ComposeTerminal]
                private Response Terminal(in Request req) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(DuplicateOrder_ProducesDiagnostic));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKCOM003 diagnostic
        var diagnostics = result.Results[0].Diagnostics;
        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "PKCOM003");
    }

    [Fact]
    public void StructType_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial struct RequestPipeline
            {
                [ComposeStep(0)]
                private Response Step(in Request req, System.Func<Request, Response> next) => next(req);

                [ComposeTerminal]
                private Response Terminal(in Request req) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(StructType_GeneratesCorrectly));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Confirm we generated the expected file
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("RequestPipeline.Composer.g.cs", names);

        // Verify the generated source contains 'partial struct'
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("partial struct RequestPipeline", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void RecordClass_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial record class RequestPipeline
            {
                [ComposeStep(0)]
                private Response Step(in Request req, System.Func<Request, Response> next) => next(req);

                [ComposeTerminal]
                private Response Terminal(in Request req) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(RecordClass_GeneratesCorrectly));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Confirm we generated the expected file
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("RequestPipeline.Composer.g.cs", names);

        // Verify the generated source contains 'partial record class'
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("partial record class RequestPipeline", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void RecordStruct_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial record struct RequestPipeline
            {
                [ComposeStep(0)]
                private Response Step(in Request req, System.Func<Request, Response> next) => next(req);

                [ComposeTerminal]
                private Response Terminal(in Request req) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(RecordStruct_GeneratesCorrectly));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Confirm we generated the expected file
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("RequestPipeline.Composer.g.cs", names);

        // Verify the generated source contains 'partial record struct'
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("partial record struct RequestPipeline", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void OrderingOuterFirst_WrapsCorrectly()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer(WrapOrder = ComposerWrapOrder.OuterFirst)]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private Response First(in Request req, System.Func<Request, Response> next)
                {
                    System.Console.WriteLine("First - Before");
                    var result = next(req);
                    System.Console.WriteLine("First - After");
                    return result;
                }

                [ComposeStep(1)]
                private Response Second(in Request req, System.Func<Request, Response> next)
                {
                    System.Console.WriteLine("Second - Before");
                    var result = next(req);
                    System.Console.WriteLine("Second - After");
                    return result;
                }

                [ComposeTerminal]
                private Response Terminal(in Request req)
                {
                    System.Console.WriteLine("Terminal");
                    return new(200);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(OrderingOuterFirst_WrapsCorrectly));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify the generated source
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();

        // The pipeline should be built from terminal and wrapped by steps
        Assert.Contains("pipeline", generatedSource);
        Assert.Contains("First", generatedSource);
        Assert.Contains("Second", generatedSource);
        Assert.Contains("Terminal", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void OrderingInnerFirst_WrapsCorrectly()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer(WrapOrder = ComposerWrapOrder.InnerFirst)]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private Response First(in Request req, System.Func<Request, Response> next)
                {
                    System.Console.WriteLine("First - Before");
                    var result = next(req);
                    System.Console.WriteLine("First - After");
                    return result;
                }

                [ComposeStep(1)]
                private Response Second(in Request req, System.Func<Request, Response> next)
                {
                    System.Console.WriteLine("Second - Before");
                    var result = next(req);
                    System.Console.WriteLine("Second - After");
                    return result;
                }

                [ComposeTerminal]
                private Response Terminal(in Request req)
                {
                    System.Console.WriteLine("Terminal");
                    return new(200);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(OrderingInnerFirst_WrapsCorrectly));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify the generated source
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();

        // The pipeline should be built from terminal and wrapped by steps
        Assert.Contains("pipeline", generatedSource);
        Assert.Contains("First", generatedSource);
        Assert.Contains("Second", generatedSource);
        Assert.Contains("Terminal", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void ComposeIgnoreAttribute_SkipsMethod()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private Response Step1(in Request req, System.Func<Request, Response> next) => next(req);

                [ComposeStep(1)]
                [ComposeIgnore]
                private Response Step2(in Request req, System.Func<Request, Response> next) => next(req);

                [ComposeTerminal]
                private Response Terminal(in Request req) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ComposeIgnoreAttribute_SkipsMethod));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify the generated source
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();

        // Should only have pipeline using Step1, not Step2
        Assert.Contains("Step1", generatedSource);
        Assert.DoesNotContain("Step2", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void CustomInvokeMethodName_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer(InvokeMethodName = "Execute")]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private Response Step(in Request req, System.Func<Request, Response> next) => next(req);

                [ComposeTerminal]
                private Response Terminal(in Request req) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(CustomInvokeMethodName_GeneratesCorrectly));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify the generated source contains custom method name
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("public global::PatternKit.Examples.Response Execute(in global::PatternKit.Examples.Request input)", generatedSource);
        Assert.DoesNotContain("public global::PatternKit.Examples.Response Invoke(in global::PatternKit.Examples.Request input)", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void SyncPipelineWithForceAsync_GeneratesBoth()
    {
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer(ForceAsync = true)]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private Response SyncStep(in Request req, System.Func<Request, Response> next)
                {
                    System.Console.WriteLine("Sync step");
                    return next(req);
                }

                [ComposeTerminal]
                private Response Terminal(in Request req) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(SyncPipelineWithForceAsync_GeneratesBoth));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify the generated source contains both Invoke and InvokeAsync
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("public global::PatternKit.Examples.Response Invoke(in global::PatternKit.Examples.Request input)", generatedSource);
        Assert.Contains("InvokeAsync", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void AsyncStruct_GeneratesCorrectly()
    {
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial struct AsyncRequestPipeline
            {
                [ComposeStep(0)]
                private async ValueTask<Response> AuthAsync(Request req, Func<Request, ValueTask<Response>> next, CancellationToken ct)
                {
                    await Task.Delay(10, ct);
                    if (req.Path == "/forbidden")
                        return new Response(403);
                    return await next(req);
                }

                [ComposeTerminal]
                private ValueTask<Response> TerminalAsync(Request req, CancellationToken ct) => 
                    new ValueTask<Response>(new Response(200));
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(AsyncStruct_GeneratesCorrectly));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify the generated source contains InvokeAsync method
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("InvokeAsync", generatedSource);
        Assert.Contains("ValueTask", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void InvalidStepSignature_ProducesDiagnostic()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private Response Step(in Request req, int invalidNext)  // Invalid signature - not a Func
                {
                    return new Response(200);
                }

                [ComposeTerminal]
                private Response Terminal(in Request req) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(InvalidStepSignature_ProducesDiagnostic));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKCOM006 diagnostic
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToList();
        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "PKCOM006");
    }

    [Fact]
    public void InvalidTerminalSignature_ProducesDiagnostic()
    {
        var source = """
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private Response Step(in Request req, System.Func<Request, Response> next) => next(req);

                [ComposeTerminal]
                private Response Terminal()  // Invalid signature - no input parameter
                {
                    return new Response(200);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(InvalidTerminalSignature_ProducesDiagnostic));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // Should have PKCOM007 diagnostic
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToList();
        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "PKCOM007");
    }

    [Fact]
    public void TrulyMixedSyncAndAsync_GeneratesAsyncOnly()
    {
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private Response SyncStep(in Request req, System.Func<Request, Response> next)
                {
                    System.Console.WriteLine("Sync step");
                    return next(req);
                }

                [ComposeStep(1)]
                private async ValueTask<Response> AsyncStep(Request req, Func<Request, ValueTask<Response>> next, CancellationToken ct)
                {
                    await Task.Delay(10, ct);
                    return await next(req);
                }

                [ComposeTerminal]
                private Response Terminal(in Request req) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(TrulyMixedSyncAndAsync_GeneratesAsyncOnly));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify the generated source contains InvokeAsync (mixed scenario only generates async)
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("InvokeAsync", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
