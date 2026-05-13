# Command Generator

The Command generator emits a small executor adapter for a partial command type and an explicitly annotated handler.

## Usage

```csharp
using PatternKit.Generators.Command;

[Command]
public readonly partial record struct RenameUser(Guid UserId, string NewName);

public sealed class UserService
{
    [CommandHandler]
    public void Handle(in RenameUser command) { }
}
```

The generated `RenameUserCommand.Execute(handler, in command)` method calls the annotated handler. `ValueTask` handlers generate an `ExecuteAsync` entrypoint.

## Diagnostics

- `PKCMD001`: command type must be partial.
- `PKCMD002`: no handler found.
- `PKCMD003`: multiple handlers found.
- `PKCMD004`: handler signature invalid.
