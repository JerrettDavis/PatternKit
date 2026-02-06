# State Machine Generator

The State Machine Generator automatically creates state transition infrastructure from declarative attributes. It eliminates boilerplate for state management while providing compile-time validation, guard conditions, entry/exit hooks, and configurable policies for invalid triggers.

## Overview

The generator produces:

- **State property** for reading the current state
- **Fire method** for synchronous state transitions with O(1) dispatch
- **FireAsync method** for asynchronous transitions (when enabled)
- **CanFire method** to check if a trigger is valid from the current state
- **Guard conditions** that can block transitions
- **Entry/Exit hooks** invoked during state changes
- **Compile-time validation** of transition definitions

## Quick Start

### 1. Define Your State Machine

Mark your class with `[StateMachine]` and declare transitions:

```csharp
using PatternKit.Generators.State;

public enum DoorState { Open, Closed, Locked }
public enum DoorTrigger { Close, Open, Lock, Unlock }

[StateMachine(typeof(DoorState), typeof(DoorTrigger))]
public partial class Door
{
    [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
    private void OnClose() { Console.WriteLine("Door closed"); }

    [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Open, To = DoorState.Open)]
    private void OnOpen() { Console.WriteLine("Door opened"); }

    [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Lock, To = DoorState.Locked)]
    private void OnLock() { Console.WriteLine("Door locked"); }

    [StateTransition(From = DoorState.Locked, Trigger = DoorTrigger.Unlock, To = DoorState.Closed)]
    private void OnUnlock() { Console.WriteLine("Door unlocked"); }
}
```

### 2. Build Your Project

The generator creates Fire, CanFire, and State members:

```csharp
var door = new Door(); // State = DoorState.Open (first enum value)

door.Fire(DoorTrigger.Close);  // State -> Closed
door.Fire(DoorTrigger.Lock);   // State -> Locked

if (door.CanFire(DoorTrigger.Unlock))
    door.Fire(DoorTrigger.Unlock);
```

### 3. Generated Code

```csharp
partial class Door
{
    public DoorState State { get; private set; }

    public bool CanFire(DoorTrigger trigger) { /* switch-based dispatch */ }
    public void Fire(DoorTrigger trigger) { /* switch-based dispatch */ }
}
```

## Attributes

### [StateMachine]

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `stateType` (ctor) | `Type` | required | Enum type for states |
| `triggerType` (ctor) | `Type` | required | Enum type for triggers |
| `FireMethodName` | `string` | `"Fire"` | Name of the fire method |
| `FireAsyncMethodName` | `string` | `"FireAsync"` | Name of the async fire method |
| `CanFireMethodName` | `string` | `"CanFire"` | Name of the can-fire method |
| `GenerateAsync` | `bool` | `false` | Generate async FireAsync method |
| `ForceAsync` | `bool` | `false` | Force async generation |
| `InvalidTrigger` | `StateMachineInvalidTriggerPolicy` | `Throw` | Behavior for invalid triggers |
| `GuardFailure` | `StateMachineGuardFailurePolicy` | `Throw` | Behavior when guards fail |

### [StateTransition]

| Property | Type | Description |
|----------|------|-------------|
| `From` | `object` (enum) | Source state |
| `Trigger` | `object` (enum) | Trigger that causes the transition |
| `To` | `object` (enum) | Destination state |

Transition methods must return `void` or `ValueTask` and accept zero parameters or a `CancellationToken`.

### [StateGuard]

| Property | Type | Description |
|----------|------|-------------|
| `From` | `object` (enum) | State this guard applies to |
| `Trigger` | `object` (enum) | Trigger this guard applies to |

Guard methods must return `bool` and accept zero parameters.

### [StateEntry] / [StateExit]

| Parameter | Type | Description |
|-----------|------|-------------|
| `state` (ctor) | `object` (enum) | State this hook applies to |

Hook methods must return `void` or `ValueTask` and accept zero parameters or a `CancellationToken`.

## Execution Order

When a trigger is fired, the following sequence occurs:

1. **Guard check** - If a guard exists, it is evaluated. If it returns false, the transition is blocked.
2. **Exit hooks** - All exit hooks for the current state are invoked.
3. **Transition action** - The transition method is invoked.
4. **State update** - The State property is updated to the new state.
5. **Entry hooks** - All entry hooks for the new state are invoked.

## Diagnostics

| Code | Message | Resolution |
|------|---------|------------|
| PKST001 | Type must be partial | Add `partial` keyword to type declaration |
| PKST002 | State type not enum | Use an enum type for states |
| PKST003 | Trigger type not enum | Use an enum type for triggers |
| PKST004 | Duplicate transition | Each (state, trigger) pair must be unique |
| PKST005 | Invalid transition signature | Return void/ValueTask, accept 0 or CancellationToken params |
| PKST006 | Invalid guard signature | Return bool, accept 0 parameters |
| PKST007 | Invalid hook signature | Return void/ValueTask, accept 0 or CancellationToken params |
| PKST008 | Async disabled | Enable GenerateAsync or ForceAsync |

## Examples

### Guards

```csharp
[StateMachine(typeof(DoorState), typeof(DoorTrigger))]
public partial class Door
{
    public bool HasKey { get; set; }

    [StateTransition(From = DoorState.Closed, Trigger = DoorTrigger.Lock, To = DoorState.Locked)]
    private void OnLock() { }

    [StateGuard(From = DoorState.Closed, Trigger = DoorTrigger.Lock)]
    private bool CanLock() => HasKey;
}
```

### Entry/Exit Hooks

```csharp
[StateMachine(typeof(DoorState), typeof(DoorTrigger))]
public partial class Door
{
    [StateEntry(DoorState.Locked)]
    private void OnEntryLocked() => Console.WriteLine("Door is now secured");

    [StateExit(DoorState.Locked)]
    private void OnExitLocked() => Console.WriteLine("Door is no longer secured");
}
```

### ReturnFalse Policy

```csharp
[StateMachine(typeof(DoorState), typeof(DoorTrigger),
    InvalidTrigger = StateMachineInvalidTriggerPolicy.ReturnFalse)]
public partial class Door
{
    // Fire() returns bool instead of void
    // Returns false for invalid triggers instead of throwing
}

var door = new Door();
bool success = door.Fire(DoorTrigger.Lock); // false if invalid
```

### Async Transitions

```csharp
[StateMachine(typeof(OrderState), typeof(OrderTrigger), ForceAsync = true)]
public partial class OrderWorkflow
{
    [StateTransition(From = OrderState.New, Trigger = OrderTrigger.Submit, To = OrderState.Processing)]
    private async ValueTask OnSubmitAsync(CancellationToken ct)
    {
        await SaveToDatabase(ct);
    }
}

await workflow.FireAsync(OrderTrigger.Submit, cancellationToken);
```

## Best Practices

### 1. Use Enums for States and Triggers

Enums provide type safety and IntelliSense support. Keep enum values descriptive:

```csharp
public enum OrderState { New, Processing, Shipped, Delivered, Cancelled }
public enum OrderTrigger { Submit, Ship, Deliver, Cancel }
```

### 2. Keep Transition Actions Focused

Each transition method should handle only the logic specific to that transition:

```csharp
[StateTransition(From = OrderState.New, Trigger = OrderTrigger.Submit, To = OrderState.Processing)]
private void OnSubmit() { /* Only submission logic */ }
```

### 3. Use Guards for Business Rules

Guards cleanly separate "can this happen?" from "what happens?":

```csharp
[StateGuard(From = OrderState.Processing, Trigger = OrderTrigger.Ship)]
private bool CanShip() => Items.All(i => i.InStock);
```

### 4. Use Entry/Exit Hooks for Cross-Cutting Concerns

Hooks are ideal for logging, auditing, and notification:

```csharp
[StateEntry(OrderState.Shipped)]
private void OnShipped() => NotifyCustomer();
```

## See Also

- [API Reference](../api/PatternKit.Generators.State.html)
- [Examples](examples.md)
- [Troubleshooting](troubleshooting.md)
