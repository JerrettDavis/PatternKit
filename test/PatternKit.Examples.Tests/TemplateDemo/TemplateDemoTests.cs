using PatternKit.Examples.TemplateDemo;
using TinyBDD;

namespace PatternKit.Examples.Tests.TemplateDemo;

public sealed class TemplateDemoTests
{
    [Scenario("DataProcessor Execute Counts Words")]
    [Fact]
    public void DataProcessor_Execute_Counts_Words()
    {
        var processor = new DataProcessor();

        var result = processor.Execute("The quick brown fox");

        ScenarioExpect.Equal(4, result);
    }

    [Scenario("DataProcessor Execute Handles Multiple Spaces")]
    [Fact]
    public void DataProcessor_Execute_Handles_Multiple_Spaces()
    {
        var processor = new DataProcessor();

        var result = processor.Execute("One   two    three");

        ScenarioExpect.Equal(3, result);
    }

    [Scenario("DataProcessor Execute Empty String")]
    [Fact]
    public void DataProcessor_Execute_Empty_String()
    {
        var processor = new DataProcessor();

        var result = processor.Execute("");

        ScenarioExpect.Equal(0, result);
    }

    [Scenario("TemplateMethodDemo Run Executes Without Errors")]
    [Fact]
    public void TemplateMethodDemo_Run_Executes_Without_Errors()
    {
        TemplateMethodDemo.Run();
    }
}

public sealed class TemplateFluentDemoTests
{
    [Scenario("TemplateFluentDemo Run Executes Without Errors")]
    [Fact]
    public void TemplateFluentDemo_Run_Executes_Without_Errors()
    {
        TemplateFluentDemo.Run();
    }
}

public sealed class AsyncDataPipelineTests
{
    [Scenario("AsyncDataPipeline ExecuteAsync Processes Request")]
    [Fact]
    public async Task AsyncDataPipeline_ExecuteAsync_Processes_Request()
    {
        var pipeline = new AsyncDataPipeline();

        var result = await pipeline.ExecuteAsync(42);

        ScenarioExpect.Equal("PAYLOAD:42", result);
    }

    [Scenario("AsyncDataPipeline ExecuteAsync Cancellation")]
    [Fact]
    public async Task AsyncDataPipeline_ExecuteAsync_Cancellation()
    {
        var pipeline = new AsyncDataPipeline();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await ScenarioExpect.ThrowsAnyAsync<OperationCanceledException>(
            () => pipeline.ExecuteAsync(1, cts.Token));
    }
}

public sealed class TemplateAsyncFluentDemoTests
{
    [Scenario("TemplateAsyncFluentDemo RunAsync Executes Without Errors")]
    [Fact]
    public async Task TemplateAsyncFluentDemo_RunAsync_Executes_Without_Errors()
    {
        await TemplateAsyncFluentDemo.RunAsync();
    }
}
