using PatternKit.Generators.Command;

namespace PatternKit.Examples.Generators.Command;

/// <summary>
/// A command that creates a new task in the task tracker.
/// The [Command] attribute generates a static Execute method.
/// </summary>
[Command]
public partial class CreateTaskCommand
{
    /// <summary>The title of the task to create.</summary>
    public string Title { get; set; } = "";

    /// <summary>The assignee for the task.</summary>
    public string Assignee { get; set; } = "";

    /// <summary>The priority level (1=highest).</summary>
    public int Priority { get; set; }

    /// <summary>
    /// Handles the create task command.
    /// </summary>
    [CommandHandler]
    public static void Handle(CreateTaskCommand cmd)
    {
        CommandGeneratorDemo.ExecutionLog.Add($"Created task '{cmd.Title}' assigned to {cmd.Assignee} (priority={cmd.Priority})");
    }
}

/// <summary>
/// A command that completes a task by title.
/// Uses a static handler for a different dispatch style.
/// </summary>
[Command]
public partial class CompleteTaskCommand
{
    /// <summary>The title of the task to complete.</summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Static handler for completing a task.
    /// </summary>
    [CommandHandler]
    public static void Handle(CompleteTaskCommand cmd)
    {
        CommandGeneratorDemo.ExecutionLog.Add($"Completed task '{cmd.Title}'");
    }
}

/// <summary>
/// A command host that groups related bulk operations.
/// The [CommandHost] attribute generates Execute methods for each [CommandCase].
/// </summary>
[CommandHost]
public static partial class BulkTaskCommands
{
    /// <summary>Archives all completed tasks.</summary>
    [CommandCase]
    public static void ArchiveCompleted(List<string> completedTasks)
    {
        foreach (var task in completedTasks)
        {
            CommandGeneratorDemo.ExecutionLog.Add($"Archived task '{task}'");
        }
    }

    /// <summary>Reassigns all tasks from one person to another.</summary>
    [CommandCase]
    public static void ReassignAll(string from, string to)
    {
        CommandGeneratorDemo.ExecutionLog.Add($"Reassigned all tasks from {from} to {to}");
    }
}

/// <summary>
/// Demonstrates the Command pattern source generator with a task management scenario.
/// Shows how the generator creates Execute/ExecuteAsync static methods that dispatch
/// to annotated handler methods.
/// </summary>
public static class CommandGeneratorDemo
{
    /// <summary>
    /// Shared log for tracking command execution order.
    /// </summary>
    public static List<string> ExecutionLog { get; } = new();

    /// <summary>
    /// Runs a demonstration of the command pattern generator.
    /// </summary>
    public static List<string> Run()
    {
        ExecutionLog.Clear();
        var log = new List<string>();

        // Create some tasks using the generated Execute method
        var createCmd1 = new CreateTaskCommand
        {
            Title = "Implement login",
            Assignee = "Alice",
            Priority = 1
        };
        CreateTaskCommand.Execute(createCmd1);

        var createCmd2 = new CreateTaskCommand
        {
            Title = "Write tests",
            Assignee = "Bob",
            Priority = 2
        };
        CreateTaskCommand.Execute(createCmd2);

        // Complete a task
        var completeCmd = new CompleteTaskCommand { Title = "Implement login" };
        CompleteTaskCommand.Execute(completeCmd);

        // Bulk operations via command host
        BulkTaskCommands.ExecuteArchiveCompleted(new List<string> { "Implement login" });
        BulkTaskCommands.ExecuteReassignAll("Bob", "Charlie");

        // Copy execution log to output
        log.AddRange(ExecutionLog);
        log.Add($"Total commands executed: {ExecutionLog.Count}");

        return log;
    }
}
