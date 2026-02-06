using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

public class StateGeneratorTests
{
    [Fact]
    public void Generates_StateMachine_Without_Diagnostics()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed, Locked }
            public enum DoorTrigger { Close, Open, Lock, Unlock }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
            public partial class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose() { }

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { }

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Lock, To = DoorState.Locked)]
                private void OnLock() { }

                [StateTransition(From = DoorState.Locked, Trigger = DoorTrigger.Unlock, To = DoorState.Closed)]
                private void OnUnlock() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_StateMachine_Without_Diagnostics));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("Door.StateMachine.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generated_Fire_And_CanFire_Compile()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed, Locked }
            public enum DoorTrigger { Close, Open, Lock, Unlock }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
            public partial class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose() { }

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { }

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Lock, To = DoorState.Locked)]
                private void OnLock() { }

                [StateTransition(From = DoorState.Locked, Trigger = DoorTrigger.Unlock, To = DoorState.Closed)]
                private void OnUnlock() { }
            }

            public static class TestRunner
            {
                public static void Run()
                {
                    var door = new Door();
                    bool canClose = door.CanFire(DoorTrigger.Close);
                    door.Fire(DoorTrigger.Close);
                    door.Fire(DoorTrigger.Lock);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generated_Fire_And_CanFire_Compile));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Door"))
            .SourceText.ToString();

        Assert.Contains("public void Fire(", generatedSource);
        Assert.Contains("public bool CanFire(", generatedSource);
        Assert.Contains("State", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Entry_And_Exit_Hooks()
    {
        var source = """
            using PatternKit.Generators.State;
            using System.Collections.Generic;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
            public partial class Door
            {
                public List<string> Log { get; } = new();

                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose() { Log.Add("Closing"); }

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { Log.Add("Opening"); }

                [StateEntry(DoorState.Closed)]
                private void OnEntryClosed() { Log.Add("Entered Closed"); }

                [StateExit(DoorState.Open)]
                private void OnExitOpen() { Log.Add("Exiting Open"); }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Entry_And_Exit_Hooks));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Door"))
            .SourceText.ToString();

        // Verify the Open->Close transition section has correct ordering:
        // ExitHook -> TransitionAction -> StateUpdate -> EntryHook
        // Find the OnExitOpen call and verify ordering relative to that anchor
        var exitIdx = generatedSource.IndexOf("OnExitOpen();");
        Assert.True(exitIdx >= 0, "Exit hook should be present");

        // From the exit hook, the next lines should be: OnClose(), State = ..., OnEntryClosed()
        var afterExit = generatedSource.Substring(exitIdx);
        var transIdx = afterExit.IndexOf("OnClose();");
        var stateIdx = afterExit.IndexOf("State = ");
        var entryIdx = afterExit.IndexOf("OnEntryClosed();");

        Assert.True(transIdx >= 0, "Transition action should be present after exit hook");
        Assert.True(stateIdx >= 0, "State update should be present after exit hook");
        Assert.True(entryIdx >= 0, "Entry hook should be present after exit hook");
        Assert.True(transIdx < stateIdx, "Transition action should be called before state update");
        Assert.True(stateIdx < entryIdx, "State update should happen before entry hook");

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Guard_Check()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
            public partial class Door
            {
                public bool AllowClose { get; set; } = true;

                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose() { }

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { }

                [StateGuard(From = DoorState.Open, Trigger = DoorTrigger.Close)]
                private bool CanClose() => AllowClose;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Guard_Check));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Door"))
            .SourceText.ToString();

        Assert.Contains("CanClose()", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Async_FireAsync()
    {
        var source = """
            using PatternKit.Generators.State;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger), ForceAsync = true)]
            public partial class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private ValueTask OnCloseAsync(CancellationToken ct) => ValueTask.CompletedTask;

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Async_FireAsync));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Door"))
            .SourceText.ToString();

        Assert.Contains("FireAsync(", generatedSource);
        Assert.Contains("await OnCloseAsync(ct).ConfigureAwait(false)", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Reports_Error_When_Type_Not_Partial()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
            public class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Type_Not_Partial));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST001");
    }

    [Fact]
    public void Reports_Error_When_State_Not_Enum()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public class NotAnEnum { }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(NotAnEnum), typeof(DoorTrigger))]
            public partial class Door { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_State_Not_Enum));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST002");
    }

    [Fact]
    public void Reports_Error_When_Trigger_Not_Enum()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public class NotAnEnum { }

            [StateMachine(typeof(DoorState), typeof(NotAnEnum))]
            public partial class Door { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Trigger_Not_Enum));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST003");
    }

    [Fact]
    public void Reports_Error_When_Duplicate_Transition()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
            public partial class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose1() { }

                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose2() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Duplicate_Transition));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST004");
    }

    [Fact]
    public void Generates_With_ReturnFalse_InvalidTrigger_Policy()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger), InvalidTrigger = StateMachineInvalidTriggerPolicy.ReturnFalse)]
            public partial class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose() { }

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_With_ReturnFalse_InvalidTrigger_Policy));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Door"))
            .SourceText.ToString();

        Assert.Contains("public bool Fire(", generatedSource);
        Assert.Contains("return false;", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Reports_Error_When_Transition_Returns_Invalid_Type()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
            public partial class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private int OnClose() => 42;

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Transition_Returns_Invalid_Type));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST005");
    }

    [Fact]
    public void Reports_Error_When_Transition_Has_Too_Many_Parameters()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
            public partial class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose(string extra, int another) { }

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Transition_Has_Too_Many_Parameters));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST005");
    }

    [Fact]
    public void Reports_Error_When_Guard_Returns_Void()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
            public partial class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose() { }

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { }

                [StateGuard(From = DoorState.Open, Trigger = DoorTrigger.Close)]
                private void CanClose() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Guard_Returns_Void));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST006");
    }

    [Fact]
    public void Reports_Error_When_Guard_Accepts_Parameters()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
            public partial class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose() { }

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { }

                [StateGuard(From = DoorState.Open, Trigger = DoorTrigger.Close)]
                private bool CanClose(string reason) => true;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Guard_Accepts_Parameters));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST006");
    }

    [Fact]
    public void Reports_Error_When_Entry_Hook_Returns_Invalid_Type()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
            public partial class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose() { }

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { }

                [StateEntry(DoorState.Closed)]
                private int OnEntryClosed() => 42;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Entry_Hook_Returns_Invalid_Type));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST007");
    }

    [Fact]
    public void Reports_Error_When_Exit_Hook_Has_Too_Many_Parameters()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
            public partial class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose() { }

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { }

                [StateExit(DoorState.Open)]
                private void OnExitOpen(string extra, int another) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Exit_Hook_Has_Too_Many_Parameters));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST007");
    }

    [Fact]
    public void Async_Auto_Detected_From_ValueTask_Transition()
    {
        var source = """
            using PatternKit.Generators.State;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
            public partial class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private ValueTask OnCloseAsync(CancellationToken ct) => ValueTask.CompletedTask;

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Async_Auto_Detected_From_ValueTask_Transition));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out var updated);

        // The generator auto-detects async methods and generates FireAsync
        var generatedSources = run.Results.SelectMany(r => r.GeneratedSources).ToArray();
        Assert.NotEmpty(generatedSources);

        var generatedSource = generatedSources
            .First(gs => gs.HintName.Contains("Door"))
            .SourceText.ToString();

        Assert.Contains("FireAsync(", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_With_Ignore_InvalidTrigger_Policy()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger), InvalidTrigger = StateMachineInvalidTriggerPolicy.Ignore)]
            public partial class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose() { }

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_With_Ignore_InvalidTrigger_Policy));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Door"))
            .SourceText.ToString();

        // Ignore policy: Fire returns void and does not throw
        Assert.Contains("public void Fire(", generatedSource);
        Assert.DoesNotContain("InvalidOperationException", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_With_Custom_Method_Names()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace TestApp;

            public enum DoorState { Open, Closed }
            public enum DoorTrigger { Close, Open }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger),
                FireMethodName = "Transition",
                CanFireMethodName = "CanTransition")]
            public partial class Door
            {
                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
                private void OnClose() { }

                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
                private void OnOpen() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_With_Custom_Method_Names));
        _ = RoslynTestHelpers.Run(comp, new StateGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Door"))
            .SourceText.ToString();

        Assert.Contains("public void Transition(", generatedSource);
        Assert.Contains("public bool CanTransition(", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
