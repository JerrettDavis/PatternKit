using PatternKit.Generators.State;

namespace PatternKit.Examples.Generators.State;

/// <summary>
/// Represents the states of a turnstile.
/// </summary>
public enum TurnstileState
{
    Locked,
    Unlocked
}

/// <summary>
/// Represents the triggers that can change a turnstile's state.
/// </summary>
public enum TurnstileTrigger
{
    InsertCoin,
    Push
}

/// <summary>
/// A turnstile with generated state machine behavior.
/// The [StateMachine] attribute generates:
/// - State property
/// - Fire(trigger) for state transitions
/// - CanFire(trigger) to check if a transition is valid
/// </summary>
[StateMachine(typeof(TurnstileState), typeof(TurnstileTrigger))]
public partial class Turnstile
{
    public List<string> Log { get; } = new();
    public int CoinCount { get; private set; }

    [StateTransition(From = TurnstileState.Locked, Trigger = TurnstileTrigger.InsertCoin, To = TurnstileState.Unlocked)]
    private void OnCoinInserted()
    {
        CoinCount++;
        Log.Add("Coin accepted, turnstile unlocked");
    }

    [StateTransition(From = TurnstileState.Unlocked, Trigger = TurnstileTrigger.Push, To = TurnstileState.Locked)]
    private void OnPushed()
    {
        Log.Add("Person passed through, turnstile locked");
    }

    [StateTransition(From = TurnstileState.Unlocked, Trigger = TurnstileTrigger.InsertCoin, To = TurnstileState.Unlocked)]
    private void OnExtraCoin()
    {
        CoinCount++;
        Log.Add("Extra coin returned");
    }

    [StateEntry(TurnstileState.Unlocked)]
    private void OnEntryUnlocked()
    {
        Log.Add("[Entry] Turnstile is now unlocked");
    }

    [StateExit(TurnstileState.Locked)]
    private void OnExitLocked()
    {
        Log.Add("[Exit] Leaving locked state");
    }
}

/// <summary>
/// Demonstrates the State Machine pattern source generator with a turnstile scenario.
/// Shows transitions, guards, entry/exit hooks, and the CanFire check using generated code.
/// </summary>
public static class StateGeneratorDemo
{
    /// <summary>
    /// Runs a demonstration of the turnstile state machine.
    /// </summary>
    public static List<string> Run()
    {
        var log = new List<string>();
        var turnstile = new Turnstile();

        log.Add($"Initial state: {turnstile.State}");

        // Check available transitions from locked state
        log.Add($"Can push when locked? {turnstile.CanFire(TurnstileTrigger.Push)}");
        log.Add($"Can insert coin when locked? {turnstile.CanFire(TurnstileTrigger.InsertCoin)}");

        // Insert coin - transitions from Locked to Unlocked
        turnstile.Fire(TurnstileTrigger.InsertCoin);
        log.Add($"State after coin: {turnstile.State}");

        // Insert extra coin while unlocked
        turnstile.Fire(TurnstileTrigger.InsertCoin);
        log.Add($"Coins inserted: {turnstile.CoinCount}");

        // Push through
        turnstile.Fire(TurnstileTrigger.Push);
        log.Add($"State after push: {turnstile.State}");

        // Add turnstile's internal log
        log.Add("--- Turnstile log ---");
        log.AddRange(turnstile.Log);

        return log;
    }
}
