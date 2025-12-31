# Memento Pattern Real-World Examples

Production-ready examples demonstrating the Memento pattern in real-world scenarios.

---

## Example 1: Rich Text Editor with Undo Stack

### The Problem

A rich text editor needs comprehensive undo/redo with intelligent grouping (typing characters should be grouped, not individual keystrokes).

### The Solution

Use Memento with duplicate suppression and debounced saves.

### The Code

```csharp
public sealed class EditorState
{
    public string Content { get; set; } = "";
    public int SelectionStart { get; set; }
    public int SelectionLength { get; set; }
    public Dictionary<string, object> Formatting { get; set; } = new();
}

public class RichTextEditor
{
    private readonly Memento<EditorState> _history;
    private EditorState _state = new();
    private DateTime _lastSave = DateTime.MinValue;
    private const int DebounceMs = 500;

    public RichTextEditor()
    {
        _history = Memento<EditorState>.Create()
            .CloneWith((in EditorState s) => new EditorState
            {
                Content = s.Content,
                SelectionStart = s.SelectionStart,
                SelectionLength = s.SelectionLength,
                Formatting = new Dictionary<string, object>(s.Formatting)
            })
            .Capacity(500)
            .Build();

        _history.Save(in _state, tag: "new-document");
    }

    public void TypeCharacter(char c)
    {
        var pos = _state.SelectionStart;
        _state.Content = _state.Content.Insert(pos, c.ToString());
        _state.SelectionStart = pos + 1;

        // Debounce typing - don't save every character
        if ((DateTime.UtcNow - _lastSave).TotalMilliseconds > DebounceMs)
        {
            _history.Save(in _state);
            _lastSave = DateTime.UtcNow;
        }
    }

    public void ApplyFormatting(string key, object value)
    {
        // Always save before formatting changes
        FlushTyping();
        _state.Formatting[key] = value;
        _history.Save(in _state, tag: $"format:{key}");
    }

    public void Delete()
    {
        FlushTyping();
        if (_state.SelectionLength > 0)
        {
            _state.Content = _state.Content.Remove(
                _state.SelectionStart, _state.SelectionLength);
            _state.SelectionLength = 0;
            _history.Save(in _state, tag: "delete-selection");
        }
    }

    private void FlushTyping()
    {
        if ((DateTime.UtcNow - _lastSave).TotalMilliseconds < DebounceMs * 2)
        {
            _history.Save(in _state);
            _lastSave = DateTime.MinValue;
        }
    }

    public bool Undo()
    {
        FlushTyping();
        return _history.Undo(ref _state);
    }

    public bool Redo() => _history.Redo(ref _state);

    public IReadOnlyList<string> GetUndoHistory() =>
        _history.History
            .Where(s => s.Version <= _history.CurrentVersion)
            .Select(s => s.Tag ?? $"Edit at {s.TimestampUtc:HH:mm:ss}")
            .Reverse()
            .ToList();
}
```

### Why This Pattern

- **Debounced saves**: Typing groups into logical edits
- **Tagged checkpoints**: Formatting changes clearly labeled
- **Capacity limit**: Memory bounded for long sessions

---

## Example 2: Configuration Rollback System

### The Problem

A system configuration manager needs to track all changes with the ability to rollback to any previous state, with named snapshots for releases.

### The Solution

Use Memento with tagged versions for releases and automatic saves for changes.

### The Code

```csharp
public class SystemConfig
{
    public Dictionary<string, string> Settings { get; set; } = new();
    public List<string> EnabledFeatures { get; set; } = new();
    public Dictionary<string, int> ResourceLimits { get; set; } = new();
}

public class ConfigurationManager
{
    private readonly Memento<SystemConfig> _history;
    private SystemConfig _config;

    public ConfigurationManager(SystemConfig initial)
    {
        _config = initial;
        _history = Memento<SystemConfig>.Create()
            .CloneWith((in SystemConfig c) => new SystemConfig
            {
                Settings = new Dictionary<string, string>(c.Settings),
                EnabledFeatures = new List<string>(c.EnabledFeatures),
                ResourceLimits = new Dictionary<string, int>(c.ResourceLimits)
            })
            .Capacity(100)
            .Build();

        _history.Save(in _config, tag: "initial");
    }

    public void SetSetting(string key, string value)
    {
        _config.Settings[key] = value;
        _history.Save(in _config, tag: $"set:{key}={value}");
    }

    public void EnableFeature(string feature)
    {
        if (!_config.EnabledFeatures.Contains(feature))
        {
            _config.EnabledFeatures.Add(feature);
            _history.Save(in _config, tag: $"enable:{feature}");
        }
    }

    public void DisableFeature(string feature)
    {
        if (_config.EnabledFeatures.Remove(feature))
        {
            _history.Save(in _config, tag: $"disable:{feature}");
        }
    }

    public void CreateRelease(string version)
    {
        _history.Save(in _config, tag: $"release:{version}");
    }

    public bool RollbackToRelease(string version)
    {
        var release = _history.History
            .LastOrDefault(s => s.Tag == $"release:{version}");

        if (release.Version > 0)
        {
            _history.Restore(release.Version, ref _config);
            return true;
        }
        return false;
    }

    public IReadOnlyList<(string Version, DateTime Timestamp)> GetReleases() =>
        _history.History
            .Where(s => s.Tag?.StartsWith("release:") == true)
            .Select(s => (s.Tag!.Replace("release:", ""), s.TimestampUtc))
            .ToList();

    public IReadOnlyList<(int Version, string Description, DateTime Timestamp)> GetChangeLog() =>
        _history.History
            .Select(s => (s.Version, s.Tag ?? "change", s.TimestampUtc))
            .ToList();
}

// Usage
var configManager = new ConfigurationManager(LoadCurrentConfig());

configManager.SetSetting("max_connections", "100");
configManager.EnableFeature("new-dashboard");
configManager.CreateRelease("v2.1.0");

configManager.SetSetting("max_connections", "200");
configManager.EnableFeature("experimental-api");

// Rollback if issues
configManager.RollbackToRelease("v2.1.0");
```

### Why This Pattern

- **Named releases**: Easy rollback to known-good states
- **Full audit trail**: Every change recorded with description
- **Selective restore**: Jump to any version

---

## Example 3: Drawing Application with Layer History

### The Problem

A drawing application needs per-layer undo/redo with the ability to save and restore entire canvas states.

### The Solution

Use separate Memento instances per layer plus a master canvas memento.

### The Code

```csharp
public class Layer
{
    public string Name { get; set; } = "";
    public List<Shape> Shapes { get; set; } = new();
    public bool Visible { get; set; } = true;
    public float Opacity { get; set; } = 1.0f;
}

public class Canvas
{
    public List<Layer> Layers { get; set; } = new();
    public int ActiveLayerIndex { get; set; }
    public string BackgroundColor { get; set; } = "#FFFFFF";
}

public class DrawingApplication
{
    private readonly Memento<Canvas> _canvasHistory;
    private readonly Dictionary<int, Memento<Layer>> _layerHistories = new();
    private Canvas _canvas = new();

    public DrawingApplication()
    {
        _canvasHistory = Memento<Canvas>.Create()
            .CloneWith((in Canvas c) => new Canvas
            {
                Layers = c.Layers.Select(CloneLayer).ToList(),
                ActiveLayerIndex = c.ActiveLayerIndex,
                BackgroundColor = c.BackgroundColor
            })
            .Capacity(50)
            .Build();

        AddLayer("Background");
    }

    public void AddLayer(string name)
    {
        var layer = new Layer { Name = name };
        _canvas.Layers.Add(layer);

        var layerHistory = Memento<Layer>.Create()
            .CloneWith((in Layer l) => CloneLayer(l))
            .Capacity(100)
            .Build();

        _layerHistories[_canvas.Layers.Count - 1] = layerHistory;
        layerHistory.Save(in layer, tag: "created");

        SaveCanvasState("add-layer");
    }

    public void DrawShape(Shape shape)
    {
        var layer = _canvas.Layers[_canvas.ActiveLayerIndex];
        layer.Shapes.Add(shape);

        var history = _layerHistories[_canvas.ActiveLayerIndex];
        history.Save(in layer, tag: $"draw:{shape.GetType().Name}");
    }

    public bool UndoLayer()
    {
        var history = _layerHistories[_canvas.ActiveLayerIndex];
        var layer = _canvas.Layers[_canvas.ActiveLayerIndex];
        return history.Undo(ref layer);
    }

    public bool RedoLayer()
    {
        var history = _layerHistories[_canvas.ActiveLayerIndex];
        var layer = _canvas.Layers[_canvas.ActiveLayerIndex];
        return history.Redo(ref layer);
    }

    public void SaveProject(string name)
    {
        _canvasHistory.Save(in _canvas, tag: $"project:{name}");
    }

    public bool LoadProject(string name)
    {
        var project = _canvasHistory.History
            .LastOrDefault(s => s.Tag == $"project:{name}");

        if (project.Version > 0)
        {
            _canvasHistory.Restore(project.Version, ref _canvas);
            RebuildLayerHistories();
            return true;
        }
        return false;
    }

    private void SaveCanvasState(string tag)
    {
        _canvasHistory.Save(in _canvas, tag: tag);
    }

    private void RebuildLayerHistories()
    {
        _layerHistories.Clear();
        for (int i = 0; i < _canvas.Layers.Count; i++)
        {
            var history = Memento<Layer>.Create()
                .CloneWith((in Layer l) => CloneLayer(l))
                .Capacity(100)
                .Build();

            var layer = _canvas.Layers[i];
            history.Save(in layer, tag: "restored");
            _layerHistories[i] = history;
        }
    }

    private static Layer CloneLayer(Layer l) => new()
    {
        Name = l.Name,
        Shapes = l.Shapes.Select(s => s.Clone()).ToList(),
        Visible = l.Visible,
        Opacity = l.Opacity
    };
}
```

### Why This Pattern

- **Hierarchical history**: Per-layer undo separate from canvas undo
- **Project saves**: Named checkpoints for entire document
- **Memory efficient**: Each layer has bounded history

---

## Example 4: Database Migration Rollback

### The Problem

A database migration system needs to track schema changes with the ability to rollback to any previous migration.

### The Solution

Use Memento to track migration state with tagged versions for each migration.

### The Code

```csharp
public class SchemaState
{
    public List<string> AppliedMigrations { get; set; } = new();
    public Dictionary<string, TableDefinition> Tables { get; set; } = new();
    public int SchemaVersion { get; set; }
}

public class MigrationManager
{
    private readonly Memento<SchemaState> _history;
    private SchemaState _schema = new();
    private readonly IDatabase _database;

    public MigrationManager(IDatabase database)
    {
        _database = database;
        _history = Memento<SchemaState>.Create()
            .CloneWith((in SchemaState s) => new SchemaState
            {
                AppliedMigrations = new List<string>(s.AppliedMigrations),
                Tables = s.Tables.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Clone()),
                SchemaVersion = s.SchemaVersion
            })
            .Build();

        _history.Save(in _schema, tag: "initial");
    }

    public async Task<bool> ApplyMigrationAsync(Migration migration, CancellationToken ct)
    {
        if (_schema.AppliedMigrations.Contains(migration.Id))
            return false;

        // Save before migration
        _history.Save(in _schema, tag: $"before:{migration.Id}");

        try
        {
            // Apply migration
            await migration.UpAsync(_database, ct);

            // Update state
            migration.UpdateSchema(_schema);
            _schema.AppliedMigrations.Add(migration.Id);
            _schema.SchemaVersion++;

            // Save after migration
            _history.Save(in _schema, tag: $"after:{migration.Id}");

            return true;
        }
        catch (Exception ex)
        {
            // Rollback on failure
            _history.Undo(ref _schema);
            throw new MigrationFailedException(migration.Id, ex);
        }
    }

    public async Task RollbackToMigrationAsync(string migrationId, CancellationToken ct)
    {
        var target = _history.History
            .LastOrDefault(s => s.Tag == $"after:{migrationId}");

        if (target.Version == 0)
            throw new MigrationNotFoundException(migrationId);

        // Get migrations to rollback
        var currentIndex = _schema.AppliedMigrations.IndexOf(migrationId);
        var migrationsToRollback = _schema.AppliedMigrations
            .Skip(currentIndex + 1)
            .Reverse()
            .ToList();

        // Execute rollbacks
        foreach (var migId in migrationsToRollback)
        {
            var migration = GetMigration(migId);
            await migration.DownAsync(_database, ct);
        }

        // Restore state
        _history.Restore(target.Version, ref _schema);
    }

    public IReadOnlyList<(string Id, DateTime AppliedAt)> GetMigrationHistory() =>
        _history.History
            .Where(s => s.Tag?.StartsWith("after:") == true)
            .Select(s => (s.Tag!.Replace("after:", ""), s.TimestampUtc))
            .ToList();
}

// Usage
var migrationManager = new MigrationManager(database);

await migrationManager.ApplyMigrationAsync(new AddUsersTableMigration(), ct);
await migrationManager.ApplyMigrationAsync(new AddOrdersTableMigration(), ct);
await migrationManager.ApplyMigrationAsync(new AddIndexesMigration(), ct);

// Problem discovered! Rollback to after users table
await migrationManager.RollbackToMigrationAsync("add-users-table", ct);
```

### Why This Pattern

- **Safe migrations**: State saved before each change
- **Selective rollback**: Return to any migration point
- **Audit trail**: Complete migration history with timestamps

---

## Key Takeaways

1. **Clone functions are critical**: Ensure deep copies for reference types
2. **Use tags strategically**: Name important checkpoints for easy navigation
3. **Set capacity limits**: Prevent unbounded memory growth
4. **Debounce for UX**: Group rapid changes into logical units
5. **Hierarchical history**: Separate concerns with multiple memento instances

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
