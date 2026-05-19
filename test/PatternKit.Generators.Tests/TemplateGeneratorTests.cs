using Microsoft.CodeAnalysis;
using TinyBDD;

namespace PatternKit.Generators.Tests;

/// <summary>
/// Comprehensive tests for the Template Method Pattern generator.
/// </summary>
public class TemplateGeneratorTests
{
    #region Basic Template Tests

    [Scenario("Generates Basic Template Without Diagnostics")]
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
        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Confirm we generated the expected file
        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("ImportWorkflow.Template.g.cs", names);

        // Verify compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Generated Execute Method Compiles Successfully")]
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
        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Get generated source
        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ImportWorkflow"))
            .SourceText.ToString();

        // Verify Execute method exists
        ScenarioExpect.Contains("public void Execute(", generatedSource);
        ScenarioExpect.Contains("Validate(ctx);", generatedSource);
        ScenarioExpect.Contains("Transform(ctx);", generatedSource);
        ScenarioExpect.Contains("Persist(ctx);", generatedSource);

        // Verify compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Hook Tests

    [Scenario("Generates Template With BeforeAll Hook")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify BeforeAll hook is called first
        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ImportWorkflow"))
            .SourceText.ToString();

        var onStartIndex = generatedSource.IndexOf("OnStart(ctx);");
        var validateIndex = generatedSource.IndexOf("Validate(ctx);");
        ScenarioExpect.True(onStartIndex < validateIndex, "BeforeAll hook should be called before steps");
    }

    [Scenario("Generates Template With AfterAll Hook")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify AfterAll hook is called last
        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ImportWorkflow"))
            .SourceText.ToString();

        var transformIndex = generatedSource.LastIndexOf("Transform(ctx);");
        var onCompleteIndex = generatedSource.IndexOf("OnComplete(ctx);");
        ScenarioExpect.True(transformIndex < onCompleteIndex, "AfterAll hook should be called after steps");
    }

    [Scenario("Generates Template With OnError Hook")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify OnError hook is in try-catch block
        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ImportWorkflow"))
            .SourceText.ToString();

        ScenarioExpect.Contains("try", generatedSource);
        ScenarioExpect.Contains("catch (System.Exception ex)", generatedSource);
        ScenarioExpect.Contains("OnError(ctx, ex);", generatedSource);
        ScenarioExpect.Contains("throw;", generatedSource); // Rethrow policy
    }

    #endregion

    #region Async Tests

    [Scenario("Generates Async Template With ValueTask")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify ExecuteAsync method is generated
        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ImportWorkflow"))
            .SourceText.ToString();

        ScenarioExpect.Contains("public async System.Threading.Tasks.ValueTask ExecuteAsync(", generatedSource);
        ScenarioExpect.Contains("await ValidateAsync(ctx, ct).ConfigureAwait(false);", generatedSource);
        ScenarioExpect.Contains("Transform(ctx);", generatedSource);
        ScenarioExpect.Contains("await PersistAsync(ctx, ct).ConfigureAwait(false);", generatedSource);
    }

    [Scenario("Generates Async Template With ForceAsync")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify ExecuteAsync method is generated even for sync steps
        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ImportWorkflow"))
            .SourceText.ToString();

        ScenarioExpect.Contains("public async System.Threading.Tasks.ValueTask ExecuteAsync(", generatedSource);
        ScenarioExpect.Contains("Validate(ctx);", generatedSource);
        ScenarioExpect.Contains("Transform(ctx);", generatedSource);
    }

    #endregion

    #region Type Target Tests

    [Scenario("Generates Template For Struct")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Generates Template For Record Class")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Generates Template For Record Struct")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Diagnostic Tests

    [Scenario("Reports Error When Type Not Partial")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKTMP001");
    }

    [Scenario("Reports Error When No Steps")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKTMP002");
    }

    [Scenario("Reports Error When Duplicate Step Order")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKTMP003");
    }

    [Scenario("Reports Error When Invalid Step Signature")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKTMP004");
    }

    [Scenario("Reports Warning When Async Step Missing CancellationToken")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKTMP007");
    }

    [Scenario("Reports Error When Invalid Hook Signature No Context")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKTMP005");
    }

    [Scenario("Reports Error When OnError Hook Missing Exception Parameter")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKTMP005");
    }

    [Scenario("Reports Error When Hook Returns Invalid Type")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKTMP005");
    }

    [Scenario("Reports Error When HandleAndContinue With NonOptional Steps")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKTMP008");
    }

    [Scenario("Allows HandleAndContinue With All Optional Steps")]
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
        ScenarioExpect.DoesNotContain(diagnostics, d => d.Id == "PKTMP008");

        // Should compile successfully
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Custom Method Names

    [Scenario("Generates Template With Custom Method Names")]
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

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));

        // Verify custom method name is used
        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("ImportWorkflow"))
            .SourceText.ToString();

        ScenarioExpect.Contains("public void Process(", generatedSource);
        ScenarioExpect.DoesNotContain("public void Execute(", generatedSource);
    }

    [Scenario("Generates Async Template With Mixed Sync Async Hooks And Error Rethrow")]
    [Fact]
    public void Generates_Async_Template_With_Mixed_Sync_Async_Hooks_And_Error_Rethrow()
    {
        var source = """
            using PatternKit.Generators.Template;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace PatternKit.Examples;

            public sealed class ImportContext
            {
                public string Data { get; set; } = "";
            }

            [Template(ExecuteAsyncMethodName = "RunAsync", ErrorPolicy = TemplateErrorPolicy.Rethrow)]
            public partial class ImportWorkflow
            {
                [TemplateHook(HookPoint.BeforeAll)]
                private ValueTask OpenAsync(ImportContext ctx, CancellationToken ct) => ValueTask.CompletedTask;

                [TemplateHook(HookPoint.BeforeAll)]
                private void TraceOpen(ImportContext ctx) { }

                [TemplateStep(0, Name = "Validate")]
                private ValueTask ValidateAsync(ImportContext ctx, CancellationToken ct) => ValueTask.CompletedTask;

                [TemplateStep(1, Name = "Transform")]
                private void Transform(ImportContext ctx) { }

                [TemplateHook(HookPoint.AfterAll)]
                private ValueTask CloseAsync(ImportContext ctx, CancellationToken ct) => ValueTask.CompletedTask;

                [TemplateHook(HookPoint.AfterAll)]
                private void TraceClose(ImportContext ctx) { }

                [TemplateHook(HookPoint.OnError)]
                private ValueTask CaptureErrorAsync(ImportContext ctx, Exception ex, CancellationToken ct) => ValueTask.CompletedTask;

                [TemplateHook(HookPoint.OnError)]
                private void TraceError(ImportContext ctx, Exception ex) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName: nameof(Generates_Async_Template_With_Mixed_Sync_Async_Hooks_And_Error_Rethrow));

        var gen = new TemplateGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, r => ScenarioExpect.Empty(r.Diagnostics));
        var generated = run.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(gs => gs.HintName == "ImportWorkflow.Template.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("public async System.Threading.Tasks.ValueTask RunAsync", generated);
        ScenarioExpect.Contains("await OpenAsync(ctx, ct).ConfigureAwait(false);", generated);
        ScenarioExpect.Contains("TraceOpen(ctx);", generated);
        ScenarioExpect.Contains("await ValidateAsync(ctx, ct).ConfigureAwait(false);", generated);
        ScenarioExpect.Contains("Transform(ctx);", generated);
        ScenarioExpect.Contains("await CloseAsync(ctx, ct).ConfigureAwait(false);", generated);
        ScenarioExpect.Contains("TraceClose(ctx);", generated);
        ScenarioExpect.Contains("await CaptureErrorAsync(ctx, ex, ct).ConfigureAwait(false);", generated);
        ScenarioExpect.Contains("TraceError(ctx, ex);", generated);
        ScenarioExpect.Contains("throw;", generated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion
}
