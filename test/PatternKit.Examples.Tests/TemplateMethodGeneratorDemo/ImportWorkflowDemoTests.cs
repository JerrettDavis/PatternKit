using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.TemplateMethodGeneratorDemo;

[Feature("Template Method Generator - Import Workflow")]
public sealed partial class ImportWorkflowDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Successful import runs all steps in order")]
    [Fact]
    public Task Successful_Import_Runs_All_Steps()
        => Given("import workflow demo", () => PatternKit.Examples.TemplateMethodGeneratorDemo.ImportWorkflowDemo.Run())
            .When("executing the workflow", log => log)
            .Then("starts with BeforeAll hook", log => log.Any(l => l.Contains("Starting import")))
            .And("loads data", log => log.Any(l => l.Contains("Loading data")))
            .And("validates data", log => log.Any(l => l.Contains("Validating data")))
            .And("transforms data", log => log.Any(l => l.Contains("Transforming data")))
            .And("persists data", log => log.Any(l => l.Contains("Persisting data")))
            .And("ends with AfterAll hook", log => log.Any(l => l.Contains("Import completed successfully")))
            .And("processes all 3 records", log => log.Any(l => l.Contains("3 records imported")))
            .AssertPassed();

    [Scenario("Import validates data and logs errors")]
    [Fact]
    public Task Invalid_Data_Triggers_OnError_Hook()
        => Given("import workflow with invalid data", 
                () => PatternKit.Examples.TemplateMethodGeneratorDemo.ImportWorkflowDemo.RunWithInvalidData())
            .When("executing the workflow", log => log)
            .Then("loads data", log => log.Any(l => l.Contains("Loading data")))
            .And("attempts validation", log => log.Any(l => l.Contains("Validating data")))
            .And("finds invalid lines", log => log.Any(l => l.Contains("invalid lines")))
            .And("invokes OnError hook", log => log.Any(l => l.Contains("ERROR:")))
            .And("logs validation failure", log => log.Any(l => l.Contains("Import failed")))
            .And("does not reach transform step", log => !log.Any(l => l.Contains("Transforming data")))
            .AssertPassed();

    [Scenario("Steps execute in deterministic order")]
    [Fact]
    public Task Steps_Execute_In_Order()
        => Given("import workflow demo", () => PatternKit.Examples.TemplateMethodGeneratorDemo.ImportWorkflowDemo.Run())
            .When("extracting step order from log", log =>
            {
                var startIdx = log.FindIndex(l => l.Contains("Starting import"));
                var loadIdx = log.FindIndex(l => l.Contains("Loading data"));
                var validateIdx = log.FindIndex(l => l.Contains("Validating data"));
                var transformIdx = log.FindIndex(l => l.Contains("Transforming data"));
                var persistIdx = log.FindIndex(l => l.Contains("Persisting data"));
                var completeIdx = log.FindIndex(l => l.Contains("Import completed"));
                
                return new[] { startIdx, loadIdx, validateIdx, transformIdx, persistIdx, completeIdx };
            })
            .Then("steps are in ascending order", indices =>
            {
                for (int i = 1; i < indices.Length; i++)
                {
                    if (indices[i] <= indices[i - 1])
                        return false;
                }
                return true;
            })
            .AssertPassed();
}
