using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

/// <summary>
/// Comprehensive tests for the Template Method Pattern generator.
/// </summary>
public class TemplateGeneratorTests
{
    #region Basic Template Tests

    [Fact]
    public void Generates_Basic_Template_Without_Diagnostics()
    {
        var source = """
            using PatternKit.Generators.Template;

            namespace PatternKit.Examples;

            public class ImportContext
            {
                public string Data { get; set; } = "";
            }

            [Template]
            public partial class ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) { }

                [TemplateStep(1)]
                private void Transform(ImportContext ctx) { }

                [TemplateStep(2)]
                private void Persist(ImportContext ctx) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Generates_Basic_Template_Without_Diagnostics));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // No generator diagnostics
        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        // Confirm we generated the expected file
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("ImportWorkflow.Template.g.cs", names);

        // Verify compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generated_Execute_Method_Compiles_Successfully()
    {
        var source = """
            using PatternKit.Generators.Template;
            using System.Collections.Generic;

            namespace PatternKit.Examples;

            public class ImportContext
            {
                public List<string> Log { get; } = new();
            }

            [Template]
            public partial class ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) 
                {
                    ctx.Log.Add("Validate");
                }

                [TemplateStep(1)]
                private void Transform(ImportContext ctx) 
                {
                    ctx.Log.Add("Transform");
                }

                [TemplateStep(2)]
                private void Persist(ImportContext ctx) 
                {
                    ctx.Log.Add("Persist");
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Generated_Execute_Method_Compiles_Successfully));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // No diagnostics
        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        // Get generated source
        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ImportWorkflow"))
            .SourceText.ToString();

        // Verify Execute method exists
        Assert.Contains("public void Execute(", generatedSource);
        Assert.Contains("Validate(ctx);", generatedSource);
        Assert.Contains("Transform(ctx);", generatedSource);
        Assert.Contains("Persist(ctx);", generatedSource);

        // Verify compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Hook Tests

    [Fact]
    public void Generates_Template_With_BeforeAll_Hook()
    {
        var source = """
            using PatternKit.Generators.Template;
            using System.Collections.Generic;

            namespace PatternKit.Examples;

            public class ImportContext
            {
                public List<string> Log { get; } = new();
            }

            [Template]
            public partial class ImportWorkflow
            {
                [TemplateHook(HookPoint.BeforeAll)]
                private void OnStart(ImportContext ctx) 
                {
                    ctx.Log.Add("BeforeAll");
                }

                [TemplateStep(0)]
                private void Validate(ImportContext ctx) 
                {
                    ctx.Log.Add("Validate");
                }

                [TemplateStep(1)]
                private void Transform(ImportContext ctx) 
                {
                    ctx.Log.Add("Transform");
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Generates_Template_With_BeforeAll_Hook));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify BeforeAll hook is called first
        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ImportWorkflow"))
            .SourceText.ToString();

        var onStartIndex = generatedSource.IndexOf("OnStart(ctx);");
        var validateIndex = generatedSource.IndexOf("Validate(ctx);");
        Assert.True(onStartIndex < validateIndex, "BeforeAll hook should be called before steps");
    }

    [Fact]
    public void Generates_Template_With_AfterAll_Hook()
    {
        var source = """
            using PatternKit.Generators.Template;
            using System.Collections.Generic;

            namespace PatternKit.Examples;

            public class ImportContext
            {
                public List<string> Log { get; } = new();
            }

            [Template]
            public partial class ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) 
                {
                    ctx.Log.Add("Validate");
                }

                [TemplateStep(1)]
                private void Transform(ImportContext ctx) 
                {
                    ctx.Log.Add("Transform");
                }

                [TemplateHook(HookPoint.AfterAll)]
                private void OnComplete(ImportContext ctx) 
                {
                    ctx.Log.Add("AfterAll");
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Generates_Template_With_AfterAll_Hook));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify AfterAll hook is called last
        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ImportWorkflow"))
            .SourceText.ToString();

        var transformIndex = generatedSource.LastIndexOf("Transform(ctx);");
        var onCompleteIndex = generatedSource.IndexOf("OnComplete(ctx);");
        Assert.True(transformIndex < onCompleteIndex, "AfterAll hook should be called after steps");
    }

    [Fact]
    public void Generates_Template_With_OnError_Hook()
    {
        var source = """
            using PatternKit.Generators.Template;
            using System;
            using System.Collections.Generic;

            namespace PatternKit.Examples;

            public class ImportContext
            {
                public List<string> Log { get; } = new();
            }

            [Template]
            public partial class ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) 
                {
                    ctx.Log.Add("Validate");
                }

                [TemplateStep(1)]
                private void Transform(ImportContext ctx) 
                {
                    ctx.Log.Add("Transform");
                    throw new InvalidOperationException("Test error");
                }

                [TemplateHook(HookPoint.OnError)]
                private void OnError(ImportContext ctx, Exception ex) 
                {
                    ctx.Log.Add($"OnError:{ex.Message}");
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Generates_Template_With_OnError_Hook));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify OnError hook is in try-catch block
        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ImportWorkflow"))
            .SourceText.ToString();

        Assert.Contains("try", generatedSource);
        Assert.Contains("catch (System.Exception ex)", generatedSource);
        Assert.Contains("OnError(ctx, ex);", generatedSource);
        Assert.Contains("throw;", generatedSource); // Rethrow policy
    }

    #endregion

    #region Async Tests

    [Fact]
    public void Generates_Async_Template_With_ValueTask()
    {
        var source = """
            using PatternKit.Generators.Template;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            namespace PatternKit.Examples;

            public class ImportContext
            {
                public List<string> Log { get; } = new();
            }

            [Template]
            public partial class ImportWorkflow
            {
                [TemplateStep(0)]
                private ValueTask ValidateAsync(ImportContext ctx, CancellationToken ct) 
                {
                    ctx.Log.Add("Validate");
                    return ValueTask.CompletedTask;
                }

                [TemplateStep(1)]
                private void Transform(ImportContext ctx) 
                {
                    ctx.Log.Add("Transform");
                }

                [TemplateStep(2)]
                private ValueTask PersistAsync(ImportContext ctx, CancellationToken ct) 
                {
                    ctx.Log.Add("Persist");
                    return ValueTask.CompletedTask;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Generates_Async_Template_With_ValueTask));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify ExecuteAsync method is generated
        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ImportWorkflow"))
            .SourceText.ToString();

        Assert.Contains("public async System.Threading.Tasks.ValueTask ExecuteAsync(", generatedSource);
        Assert.Contains("await ValidateAsync(ctx, ct).ConfigureAwait(false);", generatedSource);
        Assert.Contains("Transform(ctx);", generatedSource);
        Assert.Contains("await PersistAsync(ctx, ct).ConfigureAwait(false);", generatedSource);
    }

    [Fact]
    public void Generates_Async_Template_With_ForceAsync()
    {
        var source = """
            using PatternKit.Generators.Template;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            namespace PatternKit.Examples;

            public class ImportContext
            {
                public List<string> Log { get; } = new();
            }

            [Template(ForceAsync = true)]
            public partial class ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) 
                {
                    ctx.Log.Add("Validate");
                }

                [TemplateStep(1)]
                private void Transform(ImportContext ctx) 
                {
                    ctx.Log.Add("Transform");
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Generates_Async_Template_With_ForceAsync));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify ExecuteAsync method is generated even for sync steps
        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ImportWorkflow"))
            .SourceText.ToString();

        Assert.Contains("public async System.Threading.Tasks.ValueTask ExecuteAsync(", generatedSource);
        Assert.Contains("Validate(ctx);", generatedSource);
        Assert.Contains("Transform(ctx);", generatedSource);
    }

    #endregion

    #region Type Target Tests

    [Fact]
    public void Generates_Template_For_Struct()
    {
        var source = """
            using PatternKit.Generators.Template;

            namespace PatternKit.Examples;

            public class ImportContext
            {
                public string Data { get; set; } = "";
            }

            [Template]
            public partial struct ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) { }

                [TemplateStep(1)]
                private void Transform(ImportContext ctx) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Generates_Template_For_Struct));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Template_For_Record_Class()
    {
        var source = """
            using PatternKit.Generators.Template;

            namespace PatternKit.Examples;

            public class ImportContext
            {
                public string Data { get; set; } = "";
            }

            [Template]
            public partial record class ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) { }

                [TemplateStep(1)]
                private void Transform(ImportContext ctx) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Generates_Template_For_Record_Class));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Template_For_Record_Struct()
    {
        var source = """
            using PatternKit.Generators.Template;

            namespace PatternKit.Examples;

            public class ImportContext
            {
                public string Data { get; set; } = "";
            }

            [Template]
            public partial record struct ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) { }

                [TemplateStep(1)]
                private void Transform(ImportContext ctx) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Generates_Template_For_Record_Struct));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Diagnostic Tests

    [Fact]
    public void Reports_Error_When_Type_Not_Partial()
    {
        var source = """
            using PatternKit.Generators.Template;

            namespace PatternKit.Examples;

            public class ImportContext { }

            [Template]
            public class ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Reports_Error_When_Type_Not_Partial));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKTMP001");
    }

    [Fact]
    public void Reports_Error_When_No_Steps()
    {
        var source = """
            using PatternKit.Generators.Template;

            namespace PatternKit.Examples;

            public class ImportContext { }

            [Template]
            public partial class ImportWorkflow
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Reports_Error_When_No_Steps));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKTMP002");
    }

    [Fact]
    public void Reports_Error_When_Duplicate_Step_Order()
    {
        var source = """
            using PatternKit.Generators.Template;

            namespace PatternKit.Examples;

            public class ImportContext { }

            [Template]
            public partial class ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) { }

                [TemplateStep(0)]
                private void Transform(ImportContext ctx) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Reports_Error_When_Duplicate_Step_Order));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKTMP003");
    }

    [Fact]
    public void Reports_Error_When_Invalid_Step_Signature()
    {
        var source = """
            using PatternKit.Generators.Template;

            namespace PatternKit.Examples;

            public class ImportContext { }

            [Template]
            public partial class ImportWorkflow
            {
                [TemplateStep(0)]
                private int Validate(ImportContext ctx) { return 0; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Reports_Error_When_Invalid_Step_Signature));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKTMP004");
    }

    [Fact]
    public void Reports_Warning_When_Async_Step_Missing_CancellationToken()
    {
        var source = """
            using PatternKit.Generators.Template;
            using System.Threading.Tasks;

            namespace PatternKit.Examples;

            public class ImportContext { }

            [Template]
            public partial class ImportWorkflow
            {
                [TemplateStep(0)]
                private ValueTask ValidateAsync(ImportContext ctx) 
                { 
                    return ValueTask.CompletedTask; 
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Reports_Warning_When_Async_Step_Missing_CancellationToken));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKTMP007");
    }

    [Fact]
    public void Reports_Error_When_Invalid_Hook_Signature_No_Context()
    {
        var source = """
            using PatternKit.Generators.Template;

            namespace PatternKit.Examples;

            public class ImportContext { }

            [Template]
            public partial class ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) { }

                [TemplateHook(HookPoint.BeforeAll)]
                private void OnStart() { }  // Missing context parameter
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Reports_Error_When_Invalid_Hook_Signature_No_Context));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKTMP005");
    }

    [Fact]
    public void Reports_Error_When_OnError_Hook_Missing_Exception_Parameter()
    {
        var source = """
            using PatternKit.Generators.Template;

            namespace PatternKit.Examples;

            public class ImportContext { }

            [Template]
            public partial class ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) { }

                [TemplateHook(HookPoint.OnError)]
                private void OnError(ImportContext ctx) { }  // Missing Exception parameter
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Reports_Error_When_OnError_Hook_Missing_Exception_Parameter));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKTMP005");
    }

    [Fact]
    public void Reports_Error_When_Hook_Returns_Invalid_Type()
    {
        var source = """
            using PatternKit.Generators.Template;

            namespace PatternKit.Examples;

            public class ImportContext { }

            [Template]
            public partial class ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) { }

                [TemplateHook(HookPoint.BeforeAll)]
                private int OnStart(ImportContext ctx) { return 0; }  // Invalid return type
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Reports_Error_When_Hook_Returns_Invalid_Type));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKTMP005");
    }

    [Fact]
    public void Reports_Error_When_HandleAndContinue_With_NonOptional_Steps()
    {
        var source = """
            using PatternKit.Generators.Template;

            namespace PatternKit.Examples;

            public class ImportContext { }

            [Template(ErrorPolicy = TemplateErrorPolicy.HandleAndContinue)]
            public partial class ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) { }  // Non-optional step

                [TemplateStep(1)]
                private void Transform(ImportContext ctx) { }  // Non-optional step
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Reports_Error_When_HandleAndContinue_With_NonOptional_Steps));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKTMP008");
    }

    [Fact]
    public void Allows_HandleAndContinue_With_All_Optional_Steps()
    {
        var source = """
            using PatternKit.Generators.Template;

            namespace PatternKit.Examples;

            public class ImportContext { }

            [Template(ErrorPolicy = TemplateErrorPolicy.HandleAndContinue)]
            public partial class ImportWorkflow
            {
                [TemplateStep(0, Optional = true)]
                private void Validate(ImportContext ctx) { }

                [TemplateStep(1, Optional = true)]
                private void Transform(ImportContext ctx) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Allows_HandleAndContinue_With_All_Optional_Steps));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        // Should not have PKTMP008 diagnostic
        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.DoesNotContain(diagnostics, d => d.Id == "PKTMP008");

        // Should compile successfully
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Custom Method Names

    [Fact]
    public void Generates_Template_With_Custom_Method_Names()
    {
        var source = """
            using PatternKit.Generators.Template;
            using System.Collections.Generic;

            namespace PatternKit.Examples;

            public class ImportContext
            {
                public List<string> Log { get; } = new();
            }

            [Template(ExecuteMethodName = "Process", ExecuteAsyncMethodName = "ProcessAsync")]
            public partial class ImportWorkflow
            {
                [TemplateStep(0)]
                private void Validate(ImportContext ctx) 
                {
                    ctx.Log.Add("Validate");
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Generates_Template_With_Custom_Method_Names));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify custom method name is used
        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ImportWorkflow"))
            .SourceText.ToString();

        Assert.Contains("public void Process(", generatedSource);
        Assert.DoesNotContain("public void Execute(", generatedSource);
    }

    #endregion
}
