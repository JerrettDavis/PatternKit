using PatternKit.Generators;

namespace PatternKit.Examples.Generators.Memento;

/// <summary>
/// Demonstrates the Memento pattern with a game state scenario.
/// Shows how to use generated memento for save/load and undo/redo in games.
/// </summary>
public static class GameStateDemo
{
    /// <summary>
    /// Game state tracking player position, inventory, and game progress.
    /// Uses a mutable class with generated memento support.
    /// </summary>
    [Memento(GenerateCaretaker = true, Capacity = 50)]
    public partial class GameState
    {
        public int PlayerX { get; set; }
        public int PlayerY { get; set; }
        public int Health { get; set; }
        public int Score { get; set; }
        public int Level { get; set; }

        // This field is excluded from snapshots (e.g., temporary state)
        [MementoIgnore]
        public bool IsPaused { get; set; }

        public GameState()
        {
            Health = 100;
            Score = 0;
            Level = 1;
        }

        /// <summary>
        /// Moves the player to a new position.
        /// </summary>
        public void MovePlayer(int deltaX, int deltaY)
        {
            PlayerX += deltaX;
            PlayerY += deltaY;
        }

        /// <summary>
        /// Player takes damage.
        /// </summary>
        public void TakeDamage(int amount)
        {
            Health = Math.Max(0, Health - amount);
        }

        /// <summary>
        /// Player heals.
        /// </summary>
        public void Heal(int amount)
        {
            Health = Math.Min(100, Health + amount);
        }

        /// <summary>
        /// Player collects points.
        /// </summary>
        public void AddScore(int points)
        {
            Score += points;
        }

        /// <summary>
        /// Advance to next level.
        /// </summary>
        public void AdvanceLevel()
        {
            Level++;
            Health = 100; // Full heal on level up
        }

        public override string ToString() =>
            $"Level {Level}: Health={Health}, Score={Score}, Pos=({PlayerX},{PlayerY})";
    }

    /// <summary>
    /// Game session manager using the generated caretaker.
    /// </summary>
    public sealed class GameSession
    {
        private readonly GameState _state;
        private readonly GameStateHistory _history;

        public GameSession()
        {
            _state = new GameState();
            _history = new GameStateHistory(_state);
        }

        public GameState State => _state;
        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;

        /// <summary>
        /// Performs an action and saves it in history (checkpoint).
        /// </summary>
        public void PerformAction(Action<GameState> action, string description)
        {
            action(_state);
            SaveCheckpoint(description);
        }

        /// <summary>
        /// Creates a checkpoint (snapshot) of the current game state.
        /// </summary>
        public void SaveCheckpoint(string? tag = null)
        {
            _history.Capture(_state);
        }

        /// <summary>
        /// Undo to previous checkpoint.
        /// </summary>
        public bool UndoToCheckpoint()
        {
            if (!_history.Undo())
                return false;

            // Use the generated memento to restore the previous state
            var currentState = _history.Current;
            var memento = GameStateMemento.Capture(in currentState);
            memento.Restore(_state);
            
            return true;
        }

        /// <summary>
        /// Redo to next checkpoint.
        /// </summary>
        public bool RedoToCheckpoint()
        {
            if (!_history.Redo())
                return false;

            // Use the generated memento to restore the next state
            var currentState = _history.Current;
            var memento = GameStateMemento.Capture(in currentState);
            memento.Restore(_state);
            
            return true;
        }
    }

    /// <summary>
    /// Runs a demonstration of the game state with checkpoints and undo/redo.
    /// </summary>
    public static List<string> Run()
    {
        var log = new List<string>();
        var session = new GameSession();

        void LogState(string action)
        {
            log.Add($"{action}: {session.State}");
        }

        // Initial checkpoint
        session.SaveCheckpoint("Game Start");
        LogState("Game Start");

        // Player moves and collects points
        session.PerformAction(s =>
        {
            s.MovePlayer(5, 3);
            s.AddScore(100);
        }, "Move and collect coins");
        LogState("Move (5,3) +100 points");

        // Player takes damage
        session.PerformAction(s =>
        {
            s.TakeDamage(30);
        }, "Hit by enemy");
        LogState("Take 30 damage");

        // Player heals
        session.PerformAction(s =>
        {
            s.Heal(20);
            s.AddScore(50);
        }, "Collect health pack");
        LogState("Heal 20 +50 points");

        // Advance to level 2
        session.PerformAction(s =>
        {
            s.AdvanceLevel();
            s.MovePlayer(-5, -3); // Reset position
        }, "Level complete");
        LogState("Advance to Level 2");

        // Oops, made a mistake - undo
        if (session.UndoToCheckpoint())
        {
            LogState("Undo (back before level 2)");
        }

        // Undo again
        if (session.UndoToCheckpoint())
        {
            LogState("Undo (back before heal)");
        }

        // Redo
        if (session.RedoToCheckpoint())
        {
            LogState("Redo (restore heal)");
        }

        // New action (clears redo history)
        session.PerformAction(s =>
        {
            s.MovePlayer(10, 0);
            s.AddScore(200);
        }, "Different path taken");
        LogState("New action (redo history cleared)");

        // Try redo (should fail)
        if (!session.RedoToCheckpoint())
        {
            log.Add("Redo failed (as expected - took different path)");
        }

        log.Add($"Final: {session.State}");
        log.Add($"CanUndo: {session.CanUndo}, CanRedo: {session.CanRedo}");

        return log;
    }

    /// <summary>
    /// Demonstrates manual save/load functionality using mementos.
    /// </summary>
    public static List<string> RunSaveLoad()
    {
        var log = new List<string>();

        var game = new GameState();
        game.MovePlayer(10, 20);
        game.AddScore(500);
        game.AdvanceLevel();
        log.Add($"Game state: {game}");

        // Save game (capture memento)
        var saveFile = GameStateMemento.Capture(in game);
        log.Add($"Game saved (memento version {saveFile.MementoVersion})");

        // Continue playing...
        game.TakeDamage(50);
        game.MovePlayer(5, 5);
        log.Add($"After playing more: {game}");

        // Load game (restore from memento)
        saveFile.Restore(game); // In-place restore for mutable class
        log.Add($"Game loaded: {game}");

        return log;
    }
}
