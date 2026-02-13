using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

public class StateMachineGeneratorTests
{
    [Fact]
    public void BasicStateMachine_Class_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum OrderState { Draft, Submitted, Paid, Shipped, Cancelled }
            public enum OrderTrigger { Submit, Pay, Ship, Cancel }

            [StateMachine(typeof(OrderState), typeof(OrderTrigger))]
            public partial class OrderFlow
            {
                [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
                private void OnSubmit() { }

                [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
                private void OnPay() { }

                [StateTransition(From = OrderState.Paid, Trigger = OrderTrigger.Ship, To = OrderState.Shipped)]
                private void OnShip() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(BasicStateMachine_Class_GeneratesCorrectly));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Confirm we generated the expected file
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("OrderFlow.StateMachine.g.cs", names);

        // Verify the generated source contains expected members
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("public global::PatternKit.Examples.OrderState State { get; private set; }", generatedSource);
        Assert.Contains("public bool CanFire(global::PatternKit.Examples.OrderTrigger trigger)", generatedSource);
        Assert.Contains("public void Fire(global::PatternKit.Examples.OrderTrigger trigger)", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void BasicStateMachine_Struct_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum LightState { Off, On }
            public enum LightTrigger { Toggle }

            [StateMachine(typeof(LightState), typeof(LightTrigger))]
            public partial struct LightSwitch
            {
                [StateTransition(From = LightState.Off, Trigger = LightTrigger.Toggle, To = LightState.On)]
                private void TurnOn() { }

                [StateTransition(From = LightState.On, Trigger = LightTrigger.Toggle, To = LightState.Off)]
                private void TurnOff() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(BasicStateMachine_Struct_GeneratesCorrectly));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify struct keyword is used
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("partial struct LightSwitch", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void BasicStateMachine_RecordClass_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum DoorState { Closed, Open }
            public enum DoorTrigger { OpenDoor, CloseDoor }

            [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
            public partial record class Door
            {
                [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.OpenDoor, To = DoorState.Open)]
                private void OnOpen() { }

                [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.CloseDoor, To = DoorState.Closed)]
                private void OnClose() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(BasicStateMachine_RecordClass_GeneratesCorrectly));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify record class keyword is used
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("partial record class Door", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void BasicStateMachine_RecordStruct_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum WindowState { Closed, Open }
            public enum WindowTrigger { OpenWindow, CloseWindow }

            [StateMachine(typeof(WindowState), typeof(WindowTrigger))]
            public partial record struct Window
            {
                [StateTransition(From = WindowState.Closed, Trigger = WindowTrigger.OpenWindow, To = WindowState.Open)]
                private void OnOpen() { }

                [StateTransition(From = WindowState.Open, Trigger = WindowTrigger.CloseWindow, To = WindowState.Closed)]
                private void OnClose() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(BasicStateMachine_RecordStruct_GeneratesCorrectly));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify record struct keyword is used
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("partial record struct Window", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void AsyncStateMachine_WithValueTask_GeneratesCorrectly()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum OrderState { Draft, Submitted, Paid }
            public enum OrderTrigger { Submit, Pay }

            [StateMachine(typeof(OrderState), typeof(OrderTrigger))]
            public partial class OrderFlow
            {
                [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
                private async ValueTask OnSubmitAsync(CancellationToken ct)
                {
                    await Task.Delay(10, ct);
                }

                [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
                private async ValueTask OnPayAsync(CancellationToken ct)
                {
                    await Task.Delay(10, ct);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(AsyncStateMachine_WithValueTask_GeneratesCorrectly));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify async methods are generated
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("public async global::System.Threading.Tasks.ValueTask FireAsync(global::PatternKit.Examples.OrderTrigger trigger, global::System.Threading.CancellationToken cancellationToken = default)", generatedSource);
        Assert.Contains("await OnSubmitAsync(cancellationToken)", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void StateMachineWithGuards_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum OrderState { Draft, Submitted, Paid }
            public enum OrderTrigger { Submit, Pay }

            [StateMachine(typeof(OrderState), typeof(OrderTrigger))]
            public partial class OrderFlow
            {
                [StateGuard(From = OrderState.Draft, Trigger = OrderTrigger.Submit)]
                private bool CanSubmit() => true;

                [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
                private void OnSubmit() { }

                [StateGuard(From = OrderState.Submitted, Trigger = OrderTrigger.Pay)]
                private bool CanPay() => true;

                [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
                private void OnPay() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(StateMachineWithGuards_GeneratesCorrectly));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify guards are called
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("CanSubmit()", generatedSource);
        Assert.Contains("CanPay()", generatedSource);
        Assert.Contains("if (!CanSubmit())", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void StateMachineWithAsyncGuards_GeneratesCorrectly()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum OrderState { Draft, Submitted, Paid }
            public enum OrderTrigger { Submit, Pay }

            [StateMachine(typeof(OrderState), typeof(OrderTrigger))]
            public partial class OrderFlow
            {
                [StateGuard(From = OrderState.Draft, Trigger = OrderTrigger.Submit)]
                private async ValueTask<bool> CanSubmitAsync(CancellationToken ct)
                {
                    await Task.Delay(10, ct);
                    return true;
                }

                [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
                private void OnSubmit() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(StateMachineWithAsyncGuards_GeneratesCorrectly));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify async guards are called
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("await CanSubmitAsync(cancellationToken)", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void StateMachineWithEntryHooks_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum OrderState { Draft, Submitted, Paid }
            public enum OrderTrigger { Submit, Pay }

            [StateMachine(typeof(OrderState), typeof(OrderTrigger))]
            public partial class OrderFlow
            {
                [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
                private void OnSubmit() { }

                [StateEntry(OrderState.Submitted)]
                private void OnEnterSubmitted() { }

                [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
                private void OnPay() { }

                [StateEntry(OrderState.Paid)]
                private void OnEnterPaid() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(StateMachineWithEntryHooks_GeneratesCorrectly));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify entry hooks are called after state update
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("OnEnterSubmitted()", generatedSource);
        Assert.Contains("OnEnterPaid()", generatedSource);
        
        // Verify State is updated before entry hooks
        var submitIndex = generatedSource.IndexOf("State = global::PatternKit.Examples.OrderState.Submitted");
        var entrySubmittedIndex = generatedSource.IndexOf("OnEnterSubmitted()");
        Assert.True(submitIndex < entrySubmittedIndex, "State should be updated before entry hook is called");

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void StateMachineWithExitHooks_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum OrderState { Draft, Submitted, Paid }
            public enum OrderTrigger { Submit, Pay }

            [StateMachine(typeof(OrderState), typeof(OrderTrigger))]
            public partial class OrderFlow
            {
                [StateExit(OrderState.Draft)]
                private void OnExitDraft() { }

                [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
                private void OnSubmit() { }

                [StateExit(OrderState.Submitted)]
                private void OnExitSubmitted() { }

                [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
                private void OnPay() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(StateMachineWithExitHooks_GeneratesCorrectly));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify exit hooks are called
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("OnExitDraft()", generatedSource);
        Assert.Contains("OnExitSubmitted()", generatedSource);

        // Verify exit hooks are called before transition action
        var exitIndex = generatedSource.IndexOf("OnExitDraft()");
        var transitionIndex = generatedSource.IndexOf("OnSubmit()");
        Assert.True(exitIndex < transitionIndex, "Exit hook should be called before transition action");

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void StateMachineWithAsyncEntryExitHooks_GeneratesCorrectly()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum OrderState { Draft, Submitted, Paid }
            public enum OrderTrigger { Submit, Pay }

            [StateMachine(typeof(OrderState), typeof(OrderTrigger))]
            public partial class OrderFlow
            {
                [StateExit(OrderState.Draft)]
                private async ValueTask OnExitDraftAsync(CancellationToken ct)
                {
                    await Task.Delay(10, ct);
                }

                [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
                private void OnSubmit() { }

                [StateEntry(OrderState.Submitted)]
                private async ValueTask OnEnterSubmittedAsync(CancellationToken ct)
                {
                    await Task.Delay(10, ct);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(StateMachineWithAsyncEntryExitHooks_GeneratesCorrectly));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify async entry/exit hooks are awaited
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("await OnExitDraftAsync(cancellationToken)", generatedSource);
        Assert.Contains("await OnEnterSubmittedAsync(cancellationToken)", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void StateMachineWithInvalidTriggerPolicy_Ignore_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum State { A, B }
            public enum Trigger { T1 }

            [StateMachine(typeof(State), typeof(Trigger), InvalidTrigger = StateMachineInvalidTriggerPolicy.Ignore)]
            public partial class Machine
            {
                [StateTransition(From = State.A, Trigger = Trigger.T1, To = State.B)]
                private void OnTransition() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(StateMachineWithInvalidTriggerPolicy_Ignore_GeneratesCorrectly));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify no exception is thrown for invalid triggers
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.DoesNotContain("throw new global::System.InvalidOperationException", generatedSource);
        Assert.Contains("return;", generatedSource); // Should just return instead

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void StateMachineWithGuardFailurePolicy_Ignore_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum State { A, B }
            public enum Trigger { T1 }

            [StateMachine(typeof(State), typeof(Trigger), GuardFailure = StateMachineGuardFailurePolicy.Ignore)]
            public partial class Machine
            {
                [StateGuard(From = State.A, Trigger = Trigger.T1)]
                private bool CanTransition() => false;

                [StateTransition(From = State.A, Trigger = Trigger.T1, To = State.B)]
                private void OnTransition() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(StateMachineWithGuardFailurePolicy_Ignore_GeneratesCorrectly));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify no exception is thrown for guard failures
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        var guardFailureIndex = generatedSource.IndexOf("if (!CanTransition())");
        var throwIndex = generatedSource.IndexOf("throw new global::System.InvalidOperationException($\"Guard failed", guardFailureIndex);
        Assert.True(throwIndex == -1, "Should not throw exception on guard failure with Ignore policy");

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void NonPartialType_ReportsDiagnostic()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum State { A, B }
            public enum Trigger { T1 }

            [StateMachine(typeof(State), typeof(Trigger))]
            public class Machine
            {
                [StateTransition(From = State.A, Trigger = Trigger.T1, To = State.B)]
                private void OnTransition() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(NonPartialType_ReportsDiagnostic));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have diagnostic PKST001
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST001");
    }

    [Fact]
    public void NonEnumStateType_ReportsDiagnostic()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public class State { }
            public enum Trigger { T1 }

            [StateMachine(typeof(State), typeof(Trigger))]
            public partial class Machine
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(NonEnumStateType_ReportsDiagnostic));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have diagnostic PKST002
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST002");
    }

    [Fact]
    public void NonEnumTriggerType_ReportsDiagnostic()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum State { A, B }
            public class Trigger { }

            [StateMachine(typeof(State), typeof(Trigger))]
            public partial class Machine
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(NonEnumTriggerType_ReportsDiagnostic));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have diagnostic PKST003
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST003");
    }

    [Fact]
    public void DuplicateTransition_ReportsDiagnostic()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum State { A, B }
            public enum Trigger { T1 }

            [StateMachine(typeof(State), typeof(Trigger))]
            public partial class Machine
            {
                [StateTransition(From = State.A, Trigger = Trigger.T1, To = State.B)]
                private void OnTransition1() { }

                [StateTransition(From = State.A, Trigger = Trigger.T1, To = State.B)]
                private void OnTransition2() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(DuplicateTransition_ReportsDiagnostic));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have diagnostic PKST004
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST004");
    }

    [Fact]
    public void InvalidTransitionSignature_ReportsDiagnostic()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum State { A, B }
            public enum Trigger { T1 }

            [StateMachine(typeof(State), typeof(Trigger))]
            public partial class Machine
            {
                [StateTransition(From = State.A, Trigger = Trigger.T1, To = State.B)]
                private int OnTransition() => 42; // Invalid return type
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(InvalidTransitionSignature_ReportsDiagnostic));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have diagnostic PKST005
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST005");
    }

    [Fact]
    public void InvalidGuardSignature_ReportsDiagnostic()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum State { A, B }
            public enum Trigger { T1 }

            [StateMachine(typeof(State), typeof(Trigger))]
            public partial class Machine
            {
                [StateGuard(From = State.A, Trigger = Trigger.T1)]
                private void CanTransition() { } // Invalid return type (should be bool)

                [StateTransition(From = State.A, Trigger = Trigger.T1, To = State.B)]
                private void OnTransition() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(InvalidGuardSignature_ReportsDiagnostic));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have diagnostic PKST006
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST006");
    }

    [Fact]
    public void InvalidEntryHookSignature_ReportsDiagnostic()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum State { A, B }
            public enum Trigger { T1 }

            [StateMachine(typeof(State), typeof(Trigger))]
            public partial class Machine
            {
                [StateTransition(From = State.A, Trigger = Trigger.T1, To = State.B)]
                private void OnTransition() { }

                [StateEntry(State.B)]
                private int OnEnterB() => 42; // Invalid return type
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(InvalidEntryHookSignature_ReportsDiagnostic));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should have diagnostic PKST007
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKST007");
    }

    [Fact]
    public void CompleteOrderFlowExample_GeneratesCorrectly()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum OrderState { Draft, Submitted, Paid, Shipped, Cancelled }
            public enum OrderTrigger { Submit, Pay, Ship, Cancel }

            [StateMachine(typeof(OrderState), typeof(OrderTrigger))]
            public partial class OrderFlow
            {
                [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
                private void OnSubmit() { }

                [StateGuard(From = OrderState.Submitted, Trigger = OrderTrigger.Pay)]
                private bool CanPay() => true;

                [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
                private async ValueTask OnPayAsync(CancellationToken ct)
                {
                    await Task.Delay(10, ct);
                }

                [StateExit(OrderState.Paid)]
                private void OnExitPaid() { }

                [StateTransition(From = OrderState.Paid, Trigger = OrderTrigger.Ship, To = OrderState.Shipped)]
                private void OnShip() { }

                [StateEntry(OrderState.Shipped)]
                private void OnEnterShipped() { }

                [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
                [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
                [StateTransition(From = OrderState.Paid, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
                private void OnCancel() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(CompleteOrderFlowExample_GeneratesCorrectly));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify all expected elements are generated
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("State { get; private set; }", generatedSource);
        Assert.Contains("CanFire", generatedSource);
        Assert.Contains("Fire(", generatedSource);
        Assert.Contains("FireAsync(", generatedSource);
        Assert.Contains("CanPay()", generatedSource);
        Assert.Contains("OnExitPaid()", generatedSource);
        Assert.Contains("OnEnterShipped()", generatedSource);
        Assert.Contains("await OnPayAsync(cancellationToken)", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void CustomMethodNames_GeneratesCorrectly()
    {
        var source = """
            using PatternKit.Generators.State;

            namespace PatternKit.Examples;

            public enum State { A, B }
            public enum Trigger { T1 }

            [StateMachine(typeof(State), typeof(Trigger), 
                FireMethodName = "Transition",
                FireAsyncMethodName = "TransitionAsync",
                CanFireMethodName = "CanTransition")]
            public partial class Machine
            {
                [StateTransition(From = State.A, Trigger = Trigger.T1, To = State.B)]
                private void OnTransition() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(CustomMethodNames_GeneratesCorrectly));
        var gen = new StateMachineGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify custom method names are used
        var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("public bool CanTransition", generatedSource);
        Assert.Contains("public void Transition", generatedSource);
        Assert.DoesNotContain("public void Fire(", generatedSource);
        Assert.DoesNotContain("public bool CanFire", generatedSource);

        // And the updated compilation actually compiles
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
