using PatternKit.Examples.TemplateDemo;

namespace PatternKit.Examples.Tests.TemplateDemo;

public sealed class TemplateDemoTests
{
    [Fact]
    public void DataProcessor_Execute_Counts_Words()
    {
        var processor = new DataProcessor();

        var result = processor.Execute("The quick brown fox");

        Assert.Equal(4, result);
    }

    [Fact]
    public void DataProcessor_Execute_Handles_Multiple_Spaces()
    {
        var processor = new DataProcessor();

        var result = processor.Execute("One   two    three");

        Assert.Equal(3, result);
    }

    [Fact]
    public void DataProcessor_Execute_Empty_String()
    {
        var processor = new DataProcessor();

        var result = processor.Execute("");

        Assert.Equal(0, result);
    }

    [Fact]
    public void TemplateMethodDemo_Run_Executes_Without_Errors()
    {
        TemplateMethodDemo.Run();
    }
}

public sealed class TemplateFluentDemoTests
{
    [Fact]
    public void TemplateFluentDemo_Run_Executes_Without_Errors()
    {
        TemplateFluentDemo.Run();
    }
}

public sealed class AsyncDataPipelineTests
{
    [Fact]
    public async Task AsyncDataPipeline_ExecuteAsync_Processes_Request()
    {
        var pipeline = new AsyncDataPipeline();

        var result = await pipeline.ExecuteAsync(42);

        Assert.Equal("PAYLOAD:42", result);
    }

    [Fact]
    public async Task AsyncDataPipeline_ExecuteAsync_Cancellation()
    {
        var pipeline = new AsyncDataPipeline();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pipeline.ExecuteAsync(1, cts.Token));
    }
}

public sealed class TemplateAsyncFluentDemoTests
{
    [Fact]
    public async Task TemplateAsyncFluentDemo_RunAsync_Executes_Without_Errors()
    {
        await TemplateAsyncFluentDemo.RunAsync();
    }
}
