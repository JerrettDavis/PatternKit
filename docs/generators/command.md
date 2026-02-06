# Command Pattern Generator

The Command Pattern Generator automatically creates dispatch methods for command types. It eliminates boilerplate for command execution while supporting both synchronous and asynchronous handlers, static and instance dispatch, and command host grouping.

## Overview

The generator produces:

- **Execute method** for synchronous command dispatch
- **ExecuteAsync method** for asynchronous dispatch with ValueTask and CancellationToken support
- **Command host dispatch** for grouping related commands in a static class
- **Zero runtime overhead** through source generation

## Quick Start

### 1. Define Your Command

Mark your command type with `[Command]` and annotate the handler:

```csharp
using PatternKit.Generators.Command;

[Command]
public partial class SendEmailCommand
{
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";

    [CommandHandler]
    public void Handle(SendEmailCommand cmd)
    {
        // Send the email
        EmailService.Send(cmd.To, cmd.Subject, cmd.Body);
    }
}
```

### 2. Build Your Project

The generator runs during compilation and produces an `Execute` method:

```csharp
var cmd = new SendEmailCommand
{
    To = "user@example.com",
    Subject = "Hello",
    Body = "World"
};
SendEmailCommand.Execute(cmd);
```

### 3. Generated Code

```csharp
partial class SendEmailCommand
{
    public static void Execute(SendEmailCommand command)
    {
        command.Handle(command);
    }
}
```

## Attributes

### `[Command]`

Applied to a partial class or struct. Generates Execute/ExecuteAsync static methods.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CommandTypeName` | `string?` | type name | Custom name for generated extensions |
| `GenerateAsync` | `bool` | inferred | Whether to generate an async method |
| `ForceAsync` | `bool` | `false` | Force async generation even for sync handlers |
| `GenerateUndo` | `bool` | `false` | Whether to generate an Undo method |

### `[CommandHandler]`

Applied to the method that implements the command's execution logic.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CommandType` | `Type?` | containing type | Explicit command type association |

### `[CommandHost]`

Applied to a static partial class to group multiple related commands.

### `[CommandCase]`

Applied to methods within a `[CommandHost]` class. Each case gets a generated dispatch method.

## Diagnostics

| ID | Severity | Description |
|----|----------|-------------|
| PKCMD001 | Error | Type marked with `[Command]` must be partial |
| PKCMD002 | Error | No method marked with `[CommandHandler]` found |
| PKCMD003 | Error | Multiple methods marked with `[CommandHandler]` found |
| PKCMD004 | Error | Handler method has an invalid signature |
| PKCMD005 | Warning | Async handler but `GenerateAsync` is not enabled |
| PKCMD006 | Error | `[CommandHost]` type must be static and partial |
| PKCMD007 | Error | `[CommandCase]` method has an invalid signature |

## Examples

### Static Handler

```csharp
[Command]
public partial class DeleteCommand
{
    public int Id { get; set; }

    [CommandHandler]
    public static void Handle(DeleteCommand cmd)
    {
        Database.Delete(cmd.Id);
    }
}

// Generated: DeleteCommand.Execute(cmd) calls Handle(cmd)
```

### Async Handler

```csharp
[Command]
public partial class SaveCommand
{
    public string Data { get; set; } = "";

    [CommandHandler]
    public ValueTask HandleAsync(SaveCommand cmd, CancellationToken ct)
    {
        return Database.SaveAsync(cmd.Data, ct);
    }
}

// Generated: await SaveCommand.ExecuteAsync(cmd, ct)
```

### Command Host

```csharp
[CommandHost]
public static partial class OrderCommands
{
    [CommandCase]
    public static void Create(string orderId, decimal amount) { ... }

    [CommandCase]
    public static void Cancel(string orderId) { ... }
}

// Generated:
// OrderCommands.ExecuteCreate(orderId, amount)
// OrderCommands.ExecuteCancel(orderId)
```

## Best Practices

- Keep commands as **data containers** with the handler as the only behavior method
- Use `[CommandHost]` to group related commands that share a domain context
- Prefer static handlers when the command does not need instance state
- Use `ForceAsync = true` when you need a uniform async interface across sync/async commands
- Command types work well as structs for zero-allocation dispatch of small commands

## See Also

- [Command Generator Demo](../examples/command-generator-demo.md)
- [Command Pattern Documentation](../patterns/behavioral/command/command.md)
