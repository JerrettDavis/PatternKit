using PatternKit.Examples.Generators.Command;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Generators.Command;

[Feature("Command Generator Example")]
public sealed class CommandGeneratorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Run executes all commands and logs results")]
    [Fact]
    public Task Run_Executes_All_Commands()
        => Given("the command generator demo", () => (Func<List<string>>)CommandGeneratorDemo.Run)
            .When("running the demo", run => run())
            .Then("log contains command execution results", log => log.Count > 0)
            .And("tasks were created", log => log.Any(l => l.Contains("Created task")))
            .And("tasks were completed", log => log.Any(l => l.Contains("Completed task")))
            .And("bulk operations executed", log => log.Any(l => l.Contains("Archived task")))
            .AssertPassed();

    [Scenario("CreateTaskCommand.Execute dispatches to handler")]
    [Fact]
    public Task CreateTask_Execute_Dispatches()
        => Given("a create task command", () =>
            {
                CommandGeneratorDemo.ExecutionLog.Clear();
                return new CreateTaskCommand
                {
                    Title = "Test task",
                    Assignee = "Tester",
                    Priority = 1
                };
            })
            .When("executing the command", cmd =>
            {
                CreateTaskCommand.Execute(cmd);
                return CommandGeneratorDemo.ExecutionLog.ToList();
            })
            .Then("handler was invoked", log => log.Count == 1)
            .And("log contains task details", log => log[0].Contains("Test task") && log[0].Contains("Tester"))
            .AssertPassed();

    [Scenario("CompleteTaskCommand.Execute dispatches to static handler")]
    [Fact]
    public Task CompleteTask_Execute_Dispatches()
        => Given("a complete task command", () =>
            {
                CommandGeneratorDemo.ExecutionLog.Clear();
                return new CompleteTaskCommand { Title = "Done task" };
            })
            .When("executing the command", cmd =>
            {
                CompleteTaskCommand.Execute(cmd);
                return CommandGeneratorDemo.ExecutionLog.ToList();
            })
            .Then("handler was invoked", log => log.Count == 1)
            .And("log confirms completion", log => log[0].Contains("Completed task 'Done task'"))
            .AssertPassed();

    [Scenario("CommandHost generates Execute methods for each case")]
    [Fact]
    public Task CommandHost_Generates_Execute_Methods()
        => Given("cleared execution log", () =>
            {
                CommandGeneratorDemo.ExecutionLog.Clear();
                return true;
            })
            .When("executing bulk commands via host", _ =>
            {
                BulkTaskCommands.ExecuteReassignAll("Alice", "Bob");
                return CommandGeneratorDemo.ExecutionLog.ToList();
            })
            .Then("reassign command was executed", log => log.Any(l => l.Contains("Reassigned all tasks from Alice to Bob")))
            .AssertPassed();
}
