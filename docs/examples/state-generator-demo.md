# State Generator Demo

This demo shows how to use the State Machine source generator to build a turnstile with declarative state transitions, entry/exit hooks, and compile-time validation.

## Goal

Create a turnstile that transitions between Locked and Unlocked states based on coin insertion and push events, with entry/exit hooks for logging.

## Key Idea

The `[StateMachine(typeof(TurnstileState), typeof(TurnstileTrigger))]` attribute generates Fire, CanFire, and a State property from declarative `[StateTransition]` attributes. Guards and hooks are wired in automatically.

## Code

```csharp
public enum TurnstileState { Locked, Unlocked }
public enum TurnstileTrigger { InsertCoin, Push }

[StateMachine(typeof(TurnstileState), typeof(TurnstileTrigger))]
public partial class Turnstile
{
    [StateTransition(From = TurnstileState.Locked, Trigger = TurnstileTrigger.InsertCoin, To = TurnstileState.Unlocked)]
    private void OnCoinInserted() { Console.WriteLine("Coin accepted"); }

    [StateTransition(From = TurnstileState.Unlocked, Trigger = TurnstileTrigger.Push, To = TurnstileState.Locked)]
    private void OnPushed() { Console.WriteLine("Person passed through"); }

    [StateEntry(TurnstileState.Unlocked)]
    private void OnEntryUnlocked() { Console.WriteLine("Turnstile unlocked"); }

    [StateExit(TurnstileState.Locked)]
    private void OnExitLocked() { Console.WriteLine("Leaving locked state"); }
}
```

Usage:

```csharp
var turnstile = new Turnstile();
// State = Locked (first enum value)

turnstile.CanFire(TurnstileTrigger.Push);        // false
turnstile.CanFire(TurnstileTrigger.InsertCoin);   // true

turnstile.Fire(TurnstileTrigger.InsertCoin);
// Output: Leaving locked state -> Coin accepted -> State = Unlocked -> Turnstile unlocked

turnstile.Fire(TurnstileTrigger.Push);
// Output: Person passed through -> State = Locked
```

## Mental Model

Think of the generated state machine as a board game:
- **States** = squares on the board
- **Triggers** = dice rolls that move your piece
- **Transitions** = the rules for which square you land on
- **Guards** = "you can only move if..." conditions
- **Entry/Exit hooks** = "when you land on/leave this square, do..."
- **Execution order** = exit current square -> perform the move -> land on new square -> entry effects

## Test References

- `StateGeneratorDemoTests.TurnstileTransitions` - Verifies state transitions work
- `StateGeneratorDemoTests.CanFireChecks` - Verifies CanFire returns correct values
- `StateGeneratorDemoTests.EntryExitHooksFire` - Verifies hooks are invoked
- `StateGeneratorDemoTests.DemoRunsSuccessfully` - Smoke test for the demo
