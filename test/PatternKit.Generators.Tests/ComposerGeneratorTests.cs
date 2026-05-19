using Microsoft.CodeAnalysis;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public class ComposerGeneratorTests
{
    [Scenario("BasicSyncPipeline GeneratesCorrectly")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Confirm we generated the expected file
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("RequestPipeline.Composer.g.cs", names);

        // Verify the generated source contains Invoke method
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        ScenarioExpect.Contains("public global::PatternKit.Examples.Response Invoke(in global::PatternKit.Examples.Request input)", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("AsyncPipeline WithValueTask GeneratesCorrectly")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Confirm we generated the expected file
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("AsyncRequestPipeline.Composer.g.cs", names);

        // Verify the generated source contains InvokeAsync method
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        ScenarioExpect.Contains("InvokeAsync", generatedSource);
        ScenarioExpect.Contains("ValueTask", generatedSource);
        ScenarioExpect.Contains("CancellationToken", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("NotPartial ProducesDiagnostic")]
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
        ScenarioExpect.NotEmpty(diagnostics);
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKCOM001");
    }

    [Scenario("NoSteps ProducesDiagnostic")]
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
        ScenarioExpect.NotEmpty(diagnostics);
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKCOM002");
    }

    [Scenario("NoTerminal ProducesDiagnostic")]
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
        ScenarioExpect.NotEmpty(diagnostics);
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKCOM004");
    }

    [Scenario("MultipleTerminals ProducesDiagnostic")]
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
        ScenarioExpect.NotEmpty(diagnostics);
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKCOM005");
    }

    [Scenario("DuplicateOrder ProducesDiagnostic")]
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
        ScenarioExpect.NotEmpty(diagnostics);
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKCOM003");
    }

    [Scenario("StructType GeneratesCorrectly")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Confirm we generated the expected file
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("RequestPipeline.Composer.g.cs", names);

        // Verify the generated source contains 'partial struct'
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        ScenarioExpect.Contains("partial struct RequestPipeline", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("RecordClass GeneratesCorrectly")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Confirm we generated the expected file
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("RequestPipeline.Composer.g.cs", names);

        // Verify the generated source contains 'partial record class'
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        ScenarioExpect.Contains("partial record class RequestPipeline", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("RecordStruct GeneratesCorrectly")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Confirm we generated the expected file
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("RequestPipeline.Composer.g.cs", names);

        // Verify the generated source contains 'partial record struct'
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        ScenarioExpect.Contains("partial record struct RequestPipeline", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("OrderingOuterFirst WrapsCorrectly")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify the generated source
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();

        // The pipeline should be built from terminal and wrapped by steps
        ScenarioExpect.Contains("pipeline", generatedSource);
        ScenarioExpect.Contains("First", generatedSource);
        ScenarioExpect.Contains("Second", generatedSource);
        ScenarioExpect.Contains("Terminal", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("OrderingInnerFirst WrapsCorrectly")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify the generated source
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();

        // The pipeline should be built from terminal and wrapped by steps
        ScenarioExpect.Contains("pipeline", generatedSource);
        ScenarioExpect.Contains("First", generatedSource);
        ScenarioExpect.Contains("Second", generatedSource);
        ScenarioExpect.Contains("Terminal", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ComposeIgnoreAttribute SkipsMethod")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify the generated source
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();

        // Should only have pipeline using Step1, not Step2
        ScenarioExpect.Contains("Step1", generatedSource);
        ScenarioExpect.DoesNotContain("Step2", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("CustomInvokeMethodName GeneratesCorrectly")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify the generated source contains custom method name
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        ScenarioExpect.Contains("public global::PatternKit.Examples.Response Execute(in global::PatternKit.Examples.Request input)", generatedSource);
        ScenarioExpect.DoesNotContain("public global::PatternKit.Examples.Response Invoke(in global::PatternKit.Examples.Request input)", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("SyncPipelineWithForceAsync GeneratesBoth")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify the generated source contains both Invoke and InvokeAsync
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        ScenarioExpect.Contains("public global::PatternKit.Examples.Response Invoke(in global::PatternKit.Examples.Request input)", generatedSource);
        ScenarioExpect.Contains("InvokeAsync", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("AsyncStruct GeneratesCorrectly")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify the generated source contains InvokeAsync method
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        ScenarioExpect.Contains("InvokeAsync", generatedSource);
        ScenarioExpect.Contains("ValueTask", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("InvalidStepSignature ProducesDiagnostic")]
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
        ScenarioExpect.NotEmpty(diagnostics);
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKCOM006");
    }

    [Scenario("InvalidTerminalSignature ProducesDiagnostic")]
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
        ScenarioExpect.NotEmpty(diagnostics);
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKCOM007");
    }

    [Scenario("TrulyMixedSyncAndAsync GeneratesAsyncOnly")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify the generated source contains InvokeAsync (mixed scenario only generates async)
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        ScenarioExpect.Contains("InvokeAsync", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("StructComposer ForceAsync CustomNames InnerFirst CoversStructAsyncPipeline")]
    [Fact]
    public void StructComposer_ForceAsync_CustomNames_InnerFirst_CoversStructAsyncPipeline()
    {
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Composer;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer(
                InvokeMethodName = "Run",
                InvokeAsyncMethodName = "RunAsync",
                ForceAsync = true,
                WrapOrder = ComposerWrapOrder.InnerFirst)]
            public partial struct RequestPipeline
            {
                [ComposeStep(2, Name = "Audit")]
                private Response Audit(in Request req, Func<Request, Response> next) => next(req);

                [ComposeStep(1)]
                private ValueTask<Response> AuthAsync(Request req, Func<Request, ValueTask<Response>> next)
                    => next(req);

                [ComposeTerminal]
                private Response Terminal(in Request req) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(StructComposer_ForceAsync_CustomNames_InnerFirst_CoversStructAsyncPipeline));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        ScenarioExpect.Contains("RunAsync", generatedSource);
        ScenarioExpect.Contains("var self = this;", generatedSource);
        ScenarioExpect.Contains("terminalFunc", generatedSource);
        ScenarioExpect.Contains("self.AuthAsync(arg, pipeline)", generatedSource);
        ScenarioExpect.Contains("self.Audit(in arg, inp => pipeline(inp).GetAwaiter().GetResult())", generatedSource);
        ScenarioExpect.DoesNotContain("public Response Run(", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("AsyncSignatureWarnings WhenCancellationTokenIsWrongType")]
    [Fact]
    public void AsyncSignatureWarnings_WhenCancellationTokenIsWrongType()
    {
        var source = """
            using System;
            using System.Threading.Tasks;
            using PatternKit.Generators.Composer;

            namespace PatternKit.Examples;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private ValueTask<Response> StepAsync(Request req, Func<Request, ValueTask<Response>> next, string notCancellationToken)
                    => next(req);

                [ComposeTerminal]
                private ValueTask<Response> TerminalAsync(Request req, string notCancellationToken)
                    => new(new Response(200));
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(AsyncSignatureWarnings_WhenCancellationTokenIsWrongType));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Equal(2, diagnostics.Count(d => d.Id == "PKCOM009"));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.False(emit.Success);
    }

    [Scenario("InvalidStep WithTooFewParameters ReportsDiagnostic")]
    [Fact]
    public void InvalidStep_WithTooFewParameters_ReportsDiagnostic()
    {
        var source = """
            using PatternKit.Generators.Composer;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private Response Step(in Request req) => new(200);

                [ComposeTerminal]
                private Response Terminal(in Request req) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(InvalidStep_WithTooFewParameters_ReportsDiagnostic));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostic = ScenarioExpect.Single(result.Results.SelectMany(r => r.Diagnostics));
        ScenarioExpect.Equal("PKCOM006", diagnostic.Id);
        ScenarioExpect.Contains("Step", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Scenario("InvalidTerminal WithTooManyParameters ReportsDiagnostic")]
    [Fact]
    public void InvalidTerminal_WithTooManyParameters_ReportsDiagnostic()
    {
        var source = """
            using PatternKit.Generators.Composer;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer]
            public partial class RequestPipeline
            {
                [ComposeStep(0)]
                private Response Step(in Request req, System.Func<Request, Response> next) => next(req);

                [ComposeTerminal]
                private Response Terminal(in Request req, int extra, string other) => new(200);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(InvalidTerminal_WithTooManyParameters_ReportsDiagnostic));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostic = ScenarioExpect.Single(result.Results.SelectMany(r => r.Diagnostics));
        ScenarioExpect.Equal("PKCOM007", diagnostic.Id);
        ScenarioExpect.Contains("Terminal", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Scenario("TaskBasedAsyncPipeline CustomAsyncName GeneratesTaskUnwrapAndNoCancellationTokenCalls")]
    [Fact]
    public void TaskBasedAsyncPipeline_CustomAsyncName_GeneratesTaskUnwrapAndNoCancellationTokenCalls()
    {
        var source = """
            using System;
            using System.Threading.Tasks;
            using PatternKit.Generators.Composer;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer(InvokeAsyncMethodName = "ExecuteAsync")]
            public partial class RequestPipeline
            {
                [ComposeStep(2, Name = "Second")]
                private Task<Response> SecondAsync(Request req, Func<Request, ValueTask<Response>> next)
                    => next(req).AsTask();

                [ComposeStep(1)]
                private Response First(in Request req, Func<Request, Response> next)
                    => next(req);

                [ComposeTerminal]
                private Task<Response> TerminalAsync(Request req)
                    => Task.FromResult(new Response(200));
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(TaskBasedAsyncPipeline_CustomAsyncName_GeneratesTaskUnwrapAndNoCancellationTokenCalls));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var generatedSource = result.Results.SelectMany(r => r.GeneratedSources).Single().SourceText.ToString();
        ScenarioExpect.Contains("ExecuteAsync", generatedSource);
        ScenarioExpect.Contains("new global::System.Threading.Tasks.ValueTask<global::Response>(TerminalAsync(arg))", generatedSource);
        ScenarioExpect.Contains("new global::System.Threading.Tasks.ValueTask<global::Response>(SecondAsync(arg, pipeline))", generatedSource);
        ScenarioExpect.Contains("First(in arg, inp => prevPipeline(inp).GetAwaiter().GetResult())", generatedSource);
        ScenarioExpect.DoesNotContain("namespace ", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("StructTaskBasedAsyncPipeline WithCancellationToken WrapsTaskCalls")]
    [Fact]
    public void StructTaskBasedAsyncPipeline_WithCancellationToken_WrapsTaskCalls()
    {
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.Composer;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Composer(InvokeAsyncMethodName = "ExecuteAsync")]
            public partial struct RequestPipeline
            {
                [ComposeStep(0)]
                private Task<Response> StepAsync(Request req, Func<Request, ValueTask<Response>> next, CancellationToken ct)
                    => next(req).AsTask();

                [ComposeTerminal]
                private Task<Response> TerminalAsync(Request req, CancellationToken ct)
                    => Task.FromResult(new Response(200));
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(StructTaskBasedAsyncPipeline_WithCancellationToken_WrapsTaskCalls));
        var gen = new ComposerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var generatedSource = result.Results.SelectMany(r => r.GeneratedSources).Single().SourceText.ToString();
        ScenarioExpect.Contains("var self = this;", generatedSource);
        ScenarioExpect.Contains("new global::System.Threading.Tasks.ValueTask<global::Response>(self.TerminalAsync(arg, cancellationToken))", generatedSource);
        ScenarioExpect.Contains("new global::System.Threading.Tasks.ValueTask<global::Response>(self.StepAsync(arg, pipeline, cancellationToken))", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
