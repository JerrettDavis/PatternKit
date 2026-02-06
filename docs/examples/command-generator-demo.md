# Command Generator Demo

Demonstrates how the Command Pattern generator creates dispatch methods for task management commands. Shows both single-command `[Command]` types and grouped `[CommandHost]` classes.

## Goal

Encapsulate task management operations as command objects with generated static `Execute` methods, eliminating boilerplate dispatch code while maintaining a clean separation of command data and handler logic.

## Key idea

Mark a partial type with `[Command]` and a method with `[CommandHandler]`. The generator produces a `static Execute(command)` method. For grouped operations, use `[CommandHost]` on a static partial class with `[CommandCase]` methods.

## Code snippet

```csharp
using PatternKit.Generators.Command;

// Single command with instance handler
[Command]
public partial class CreateTaskCommand
{
    public string Title { get; set; } = "";
    public string Assignee { get; set; } = "";

    [CommandHandler]
    public void Handle(CreateTaskCommand cmd)
    {
        TaskStore.Add(cmd.Title, cmd.Assignee);
    }
}

// Static handler variant
[Command]
public partial class CompleteTaskCommand
{
    public string Title { get; set; } = "";

    [CommandHandler]
    public static void Handle(CompleteTaskCommand cmd)
    {
        TaskStore.Complete(cmd.Title);
    }
}

// Command host for bulk operations
[CommandHost]
public static partial class BulkTaskCommands
{
    [CommandCase]
    public static void ArchiveCompleted(List<string> tasks) { ... }

    [CommandCase]
    public static void ReassignAll(string from, string to) { ... }
}

// Usage:
CreateTaskCommand.Execute(new CreateTaskCommand { Title = "Login", Assignee = "Alice" });
CompleteTaskCommand.Execute(new CompleteTaskCommand { Title = "Login" });
BulkTaskCommands.ExecuteArchiveCompleted(completedList);
BulkTaskCommands.ExecuteReassignAll("Alice", "Bob");
```

## Mental model

```
[Command] on type
    |
    +-- [CommandHandler] on method
    |       |
    |       +-- Generated: TypeName.Execute(command)
    |                        --> command.Handle(command)   [instance]
    |                        --> Handle(command)           [static]
    |
[CommandHost] on static class
    |
    +-- [CommandCase] on methods
            |
            +-- Generated: HostName.Execute{CaseName}(params)
                             --> CaseName(params)
```

## Test references

- `CommandGeneratorDemoTests.Run_Executes_All_Commands` -- validates full demo output
- `CommandGeneratorDemoTests.CreateTask_Execute_Dispatches` -- single command dispatch
- `CommandGeneratorDemoTests.CompleteTask_Execute_Dispatches` -- static handler dispatch
- `CommandGeneratorDemoTests.CommandHost_Generates_Execute_Methods` -- host dispatch

## See Also

- [Command Generator Reference](../generators/command.md)
