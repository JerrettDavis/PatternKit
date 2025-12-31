# Command Pattern Real-World Examples

Production-ready examples demonstrating the Command pattern in real-world scenarios.

---

## Example 1: Document Editor with Undo/Redo

### The Problem

A text editor needs to support undo/redo for all editing operations including insert, delete, format, and composite operations like "find and replace".

### The Solution

Use Command to encapsulate each editing operation with its inverse, maintaining undo/redo stacks.

### The Code

```csharp
public class Document
{
    public StringBuilder Content { get; } = new();
    public int CursorPosition { get; set; }
}

public class EditorContext
{
    public Document Document { get; init; }
    public string? SavedText { get; set; }
    public int? SavedPosition { get; set; }
}

// Command factory for common operations
public static class EditorCommands
{
    public static Command<EditorContext> InsertText(string text)
    {
        return Command<EditorContext>.Create()
            .Do(ctx =>
            {
                ctx.SavedPosition = ctx.Document.CursorPosition;
                ctx.Document.Content.Insert(ctx.Document.CursorPosition, text);
                ctx.Document.CursorPosition += text.Length;
            })
            .Undo(ctx =>
            {
                ctx.Document.Content.Remove(ctx.SavedPosition!.Value, text.Length);
                ctx.Document.CursorPosition = ctx.SavedPosition.Value;
            })
            .Build();
    }

    public static Command<EditorContext> DeleteSelection(int start, int length)
    {
        return Command<EditorContext>.Create()
            .Do(ctx =>
            {
                ctx.SavedText = ctx.Document.Content.ToString(start, length);
                ctx.SavedPosition = start;
                ctx.Document.Content.Remove(start, length);
                ctx.Document.CursorPosition = start;
            })
            .Undo(ctx =>
            {
                ctx.Document.Content.Insert(ctx.SavedPosition!.Value, ctx.SavedText!);
                ctx.Document.CursorPosition = ctx.SavedPosition.Value + ctx.SavedText.Length;
            })
            .Build();
    }

    public static Command<EditorContext> ReplaceAll(string find, string replace)
    {
        var replacements = new List<(int pos, string old)>();

        return Command<EditorContext>.Create()
            .Do(ctx =>
            {
                replacements.Clear();
                var content = ctx.Document.Content.ToString();
                var index = 0;

                while ((index = content.IndexOf(find, index, StringComparison.Ordinal)) >= 0)
                {
                    replacements.Add((index, find));
                    ctx.Document.Content.Remove(index, find.Length);
                    ctx.Document.Content.Insert(index, replace);
                    content = ctx.Document.Content.ToString();
                    index += replace.Length;
                }
            })
            .Undo(ctx =>
            {
                // Reverse order to maintain positions
                for (int i = replacements.Count - 1; i >= 0; i--)
                {
                    var (pos, old) = replacements[i];
                    ctx.Document.Content.Remove(pos, replace.Length);
                    ctx.Document.Content.Insert(pos, old);
                }
            })
            .Build();
    }
}

// Editor with undo/redo stacks
public class TextEditor
{
    private readonly Stack<Command<EditorContext>> _undoStack = new();
    private readonly Stack<Command<EditorContext>> _redoStack = new();
    private readonly EditorContext _context;

    public TextEditor()
    {
        _context = new EditorContext { Document = new Document() };
    }

    public async Task ExecuteAsync(Command<EditorContext> command)
    {
        await command.Execute(_context);

        if (command.HasUndo)
        {
            _undoStack.Push(command);
            _redoStack.Clear(); // Clear redo on new action
        }
    }

    public async Task<bool> UndoAsync()
    {
        if (!_undoStack.TryPop(out var command))
            return false;

        if (command.TryUndo(_context, out var undoTask))
        {
            await undoTask;
            _redoStack.Push(command);
            return true;
        }

        return false;
    }

    public async Task<bool> RedoAsync()
    {
        if (!_redoStack.TryPop(out var command))
            return false;

        await command.Execute(_context);
        _undoStack.Push(command);
        return true;
    }

    public string GetContent() => _context.Document.Content.ToString();
}

// Usage
var editor = new TextEditor();

await editor.ExecuteAsync(EditorCommands.InsertText("Hello "));
await editor.ExecuteAsync(EditorCommands.InsertText("World"));
Console.WriteLine(editor.GetContent()); // "Hello World"

await editor.UndoAsync();
Console.WriteLine(editor.GetContent()); // "Hello "

await editor.RedoAsync();
Console.WriteLine(editor.GetContent()); // "Hello World"
```

### Why This Pattern

- **Reversible operations**: Every edit can be undone
- **Composite undo**: ReplaceAll undoes all replacements at once
- **History tracking**: Undo/redo stacks maintain edit history
- **Encapsulated logic**: Each operation is self-contained

---

## Example 2: Deployment Pipeline

### The Problem

A deployment system needs to execute multiple steps (build, test, deploy, notify) with the ability to rollback if any step fails.

### The Solution

Use Macro commands to create a deployment pipeline with ordered execution and rollback capability.

### The Code

```csharp
public class DeploymentContext
{
    public string Version { get; init; }
    public string Environment { get; init; }
    public List<string> Log { get; } = new();
    public string? BuildArtifactPath { get; set; }
    public string? PreviousVersion { get; set; }
    public bool TestsPassed { get; set; }
}

public static class DeploymentCommands
{
    public static Command<DeploymentContext> Build() =>
        Command<DeploymentContext>.Create()
            .Do(async (in DeploymentContext ctx, CancellationToken ct) =>
            {
                ctx.Log.Add($"Building version {ctx.Version}...");
                await Task.Delay(100, ct); // Simulate build
                ctx.BuildArtifactPath = $"/artifacts/{ctx.Version}.zip";
                ctx.Log.Add($"Build complete: {ctx.BuildArtifactPath}");
            })
            .Undo(async (in DeploymentContext ctx, CancellationToken ct) =>
            {
                ctx.Log.Add($"Cleaning build artifacts...");
                if (ctx.BuildArtifactPath != null)
                {
                    // Delete artifact
                    ctx.BuildArtifactPath = null;
                }
            })
            .Build();

    public static Command<DeploymentContext> RunTests() =>
        Command<DeploymentContext>.Create()
            .Do(async (in DeploymentContext ctx, CancellationToken ct) =>
            {
                ctx.Log.Add("Running tests...");
                await Task.Delay(200, ct); // Simulate tests
                ctx.TestsPassed = true;
                ctx.Log.Add("All tests passed");
            })
            .Build(); // No undo - tests don't need rollback

    public static Command<DeploymentContext> Deploy() =>
        Command<DeploymentContext>.Create()
            .Do(async (in DeploymentContext ctx, CancellationToken ct) =>
            {
                ctx.Log.Add($"Deploying to {ctx.Environment}...");

                // Save current version for rollback
                ctx.PreviousVersion = await GetCurrentVersionAsync(ctx.Environment, ct);

                await DeployVersionAsync(ctx.Environment, ctx.Version, ct);
                ctx.Log.Add($"Deployed {ctx.Version} to {ctx.Environment}");
            })
            .Undo(async (in DeploymentContext ctx, CancellationToken ct) =>
            {
                if (ctx.PreviousVersion != null)
                {
                    ctx.Log.Add($"Rolling back to {ctx.PreviousVersion}...");
                    await DeployVersionAsync(ctx.Environment, ctx.PreviousVersion, ct);
                    ctx.Log.Add($"Rolled back to {ctx.PreviousVersion}");
                }
            })
            .Build();

    public static Command<DeploymentContext> Notify() =>
        Command<DeploymentContext>.Create()
            .Do(async (in DeploymentContext ctx, CancellationToken ct) =>
            {
                ctx.Log.Add("Sending notifications...");
                await SendSlackMessageAsync($"Deployed {ctx.Version} to {ctx.Environment}", ct);
                ctx.Log.Add("Notifications sent");
            })
            .Build(); // Notifications don't rollback

    private static Task<string> GetCurrentVersionAsync(string env, CancellationToken ct) =>
        Task.FromResult("1.0.0");

    private static Task DeployVersionAsync(string env, string version, CancellationToken ct) =>
        Task.Delay(100, ct);

    private static Task SendSlackMessageAsync(string msg, CancellationToken ct) =>
        Task.Delay(50, ct);
}

// Create and execute pipeline
public class DeploymentService
{
    public async Task<DeploymentResult> DeployAsync(
        string version,
        string environment,
        bool runTests,
        CancellationToken ct)
    {
        var context = new DeploymentContext
        {
            Version = version,
            Environment = environment
        };

        var pipeline = Command<DeploymentContext>.Macro()
            .Add(DeploymentCommands.Build())
            .AddIf(runTests, DeploymentCommands.RunTests())
            .Add(DeploymentCommands.Deploy())
            .Add(DeploymentCommands.Notify())
            .Build();

        try
        {
            await pipeline.Execute(context, ct);
            return new DeploymentResult(true, context.Log);
        }
        catch (Exception ex)
        {
            context.Log.Add($"Error: {ex.Message}");
            context.Log.Add("Initiating rollback...");

            if (pipeline.TryUndo(context, ct, out var undoTask))
            {
                try
                {
                    await undoTask;
                    context.Log.Add("Rollback complete");
                }
                catch (Exception undoEx)
                {
                    context.Log.Add($"Rollback failed: {undoEx.Message}");
                }
            }

            return new DeploymentResult(false, context.Log, ex);
        }
    }
}
```

### Why This Pattern

- **Ordered execution**: Steps run in defined sequence
- **Automatic rollback**: Undo reverses in correct order
- **Conditional steps**: Skip tests in certain environments
- **Audit trail**: Log captures all operations

---

## Example 3: Shopping Cart with Optimistic Updates

### The Problem

An e-commerce cart needs instant UI updates when items are added/removed, but must rollback if the server rejects the change.

### The Solution

Use Command to apply changes optimistically, then undo if the server operation fails.

### The Code

```csharp
public class CartContext
{
    public List<CartItem> Items { get; } = new();
    public Action OnCartChanged { get; init; }
}

public record CartItem(string ProductId, string Name, decimal Price, int Quantity);

public class ShoppingCartService
{
    private readonly ICartApi _api;
    private readonly CartContext _context;

    public ShoppingCartService(ICartApi api, Action onCartChanged)
    {
        _api = api;
        _context = new CartContext { OnCartChanged = onCartChanged };
    }

    public async Task AddItemAsync(CartItem item, CancellationToken ct)
    {
        var addCommand = Command<CartContext>.Create()
            .Do(ctx =>
            {
                var existing = ctx.Items.FirstOrDefault(i => i.ProductId == item.ProductId);
                if (existing != null)
                {
                    var index = ctx.Items.IndexOf(existing);
                    ctx.Items[index] = existing with { Quantity = existing.Quantity + item.Quantity };
                }
                else
                {
                    ctx.Items.Add(item);
                }
                ctx.OnCartChanged();
            })
            .Undo(ctx =>
            {
                var existing = ctx.Items.FirstOrDefault(i => i.ProductId == item.ProductId);
                if (existing != null)
                {
                    if (existing.Quantity > item.Quantity)
                    {
                        var index = ctx.Items.IndexOf(existing);
                        ctx.Items[index] = existing with { Quantity = existing.Quantity - item.Quantity };
                    }
                    else
                    {
                        ctx.Items.Remove(existing);
                    }
                }
                ctx.OnCartChanged();
            })
            .Build();

        // Apply optimistically
        await addCommand.Execute(_context);

        // Verify with server
        var result = await _api.AddItemAsync(item.ProductId, item.Quantity, ct);

        if (!result.Success)
        {
            // Rollback
            if (addCommand.TryUndo(_context, out var undo))
                await undo;

            throw new CartOperationException(result.Error);
        }
    }

    public async Task RemoveItemAsync(string productId, CancellationToken ct)
    {
        var item = _context.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null) return;

        var removeCommand = Command<CartContext>.Create()
            .Do(ctx =>
            {
                ctx.Items.Remove(item);
                ctx.OnCartChanged();
            })
            .Undo(ctx =>
            {
                ctx.Items.Add(item);
                ctx.OnCartChanged();
            })
            .Build();

        await removeCommand.Execute(_context);

        var result = await _api.RemoveItemAsync(productId, ct);

        if (!result.Success)
        {
            if (removeCommand.TryUndo(_context, out var undo))
                await undo;

            throw new CartOperationException(result.Error);
        }
    }
}
```

### Why This Pattern

- **Instant feedback**: UI updates immediately
- **Server validation**: Backend confirms the change
- **Graceful rollback**: Reverts on server rejection
- **Clean separation**: Command logic is independent of API

---

## Example 4: Database Migration System

### The Problem

A database migration system needs to apply schema changes in order and support rollback to previous versions.

### The Solution

Use Commands for each migration with Up (apply) and Down (rollback) operations.

### The Code

```csharp
public class MigrationContext
{
    public IDbConnection Connection { get; init; }
    public List<string> AppliedMigrations { get; } = new();
}

public interface IMigration
{
    string Version { get; }
    Command<MigrationContext> CreateCommand();
}

public class CreateUsersTableMigration : IMigration
{
    public string Version => "001";

    public Command<MigrationContext> CreateCommand() =>
        Command<MigrationContext>.Create()
            .Do(async (in MigrationContext ctx, CancellationToken ct) =>
            {
                await ctx.Connection.ExecuteAsync("""
                    CREATE TABLE Users (
                        Id INT PRIMARY KEY,
                        Email VARCHAR(255) NOT NULL,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    )
                    """);
                ctx.AppliedMigrations.Add(Version);
            })
            .Undo(async (in MigrationContext ctx, CancellationToken ct) =>
            {
                await ctx.Connection.ExecuteAsync("DROP TABLE Users");
                ctx.AppliedMigrations.Remove(Version);
            })
            .Build();
}

public class MigrationRunner
{
    private readonly List<IMigration> _migrations;
    private readonly MigrationContext _context;

    public MigrationRunner(IDbConnection connection, IEnumerable<IMigration> migrations)
    {
        _context = new MigrationContext { Connection = connection };
        _migrations = migrations.OrderBy(m => m.Version).ToList();
    }

    public async Task MigrateToAsync(string targetVersion, CancellationToken ct)
    {
        var toApply = _migrations
            .Where(m => string.CompareOrdinal(m.Version, targetVersion) <= 0)
            .Where(m => !_context.AppliedMigrations.Contains(m.Version))
            .ToList();

        var macro = Command<MigrationContext>.Macro();
        foreach (var migration in toApply)
        {
            macro.Add(migration.CreateCommand());
        }

        var pipeline = macro.Build();

        try
        {
            await pipeline.Execute(_context, ct);
        }
        catch
        {
            // Rollback on failure
            if (pipeline.TryUndo(_context, ct, out var undo))
                await undo;
            throw;
        }
    }

    public async Task RollbackAsync(int steps, CancellationToken ct)
    {
        var toRollback = _context.AppliedMigrations
            .OrderByDescending(v => v)
            .Take(steps)
            .ToList();

        foreach (var version in toRollback)
        {
            var migration = _migrations.First(m => m.Version == version);
            var cmd = migration.CreateCommand();

            if (cmd.TryUndo(_context, ct, out var undo))
                await undo;
        }
    }
}
```

### Why This Pattern

- **Ordered migrations**: Apply in version sequence
- **Full rollback**: Each migration knows how to undo itself
- **Atomic batches**: Macro ensures all-or-nothing
- **Version tracking**: Context maintains applied state

---

## Key Takeaways

1. **Encapsulate reversible actions**: Pair Do with Undo for reversible operations
2. **Use macros for pipelines**: Combine related commands into atomic units
3. **Handle failures explicitly**: Macro doesn't auto-rollback; call undo in catch
4. **Optimistic updates**: Apply immediately, rollback on server rejection
5. **Reuse command instances**: Commands are immutable and thread-safe

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
- [Command.md](./command.md) - Original Command documentation
