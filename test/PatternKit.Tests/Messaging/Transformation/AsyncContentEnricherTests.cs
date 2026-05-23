using PatternKit.Messaging;
using PatternKit.Messaging.Transformation;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Transformation;

public sealed class AsyncContentEnricherTests
{
    [Scenario("EnrichAsync AppliesAllStepsInOrder")]
    [Fact]
    public async Task EnrichAsync_AppliesAllStepsInOrder()
    {
        var enricher = AsyncContentEnricher<Customer>.Create()
            .Enrich("add-email", async (c, _, _) => { await Task.CompletedTask; return c with { Email = "user@example.com" }; })
            .Enrich("add-tier", async (c, _, _) => { await Task.CompletedTask; return c with { Tier = "Gold" }; })
            .Build();

        var message = Message<Customer>.Create(new Customer("Alice", null, null));
        var result = await enricher.EnrichAsync(message);

        ScenarioExpect.Equal("user@example.com", result.Message.Payload.Email);
        ScenarioExpect.Equal("Gold", result.Message.Payload.Tier);
        ScenarioExpect.Equal(2, result.StepResults.Count);
        ScenarioExpect.True(result.StepResults.All(r => r.Applied));
    }

    [Scenario("EnrichAsync PreservesHeadersUnchanged")]
    [Fact]
    public async Task EnrichAsync_PreservesHeadersUnchanged()
    {
        var enricher = AsyncContentEnricher<Customer>.Create()
            .Enrich("add-email", async (c, _, _) => { await Task.CompletedTask; return c with { Email = "x@x.com" }; })
            .Build();

        var message = new Message<Customer>(new Customer("Alice", null, null), MessageHeaders.Empty.WithCorrelationId("corr-99"));
        var result = await enricher.EnrichAsync(message);

        ScenarioExpect.Equal("corr-99", result.Message.Headers.CorrelationId);
    }

    [Scenario("EnrichAsync ThrowPolicy PropagatesException")]
    [Fact]
    public async Task EnrichAsync_ThrowPolicy_PropagatesException()
    {
        var enricher = AsyncContentEnricher<Customer>.Create()
            .Enrich("failing", async (c, _, _) => { await Task.CompletedTask; throw new InvalidOperationException("fetch error"); },
                EnrichmentErrorPolicy.Throw)
            .Build();

        await ScenarioExpect.ThrowsAsync<InvalidOperationException>(
            () => enricher.EnrichAsync(Message<Customer>.Create(new Customer("Alice", null, null))).AsTask());
    }

    [Scenario("EnrichAsync SkipPolicy ContinuesPipelineOnFailure")]
    [Fact]
    public async Task EnrichAsync_SkipPolicy_ContinuesPipelineOnFailure()
    {
        var enricher = AsyncContentEnricher<Customer>.Create()
            .Enrich("failing", async (c, _, _) => { await Task.CompletedTask; throw new InvalidOperationException(); },
                EnrichmentErrorPolicy.Skip)
            .Enrich("succeeding", async (c, _, _) => { await Task.CompletedTask; return c with { Tier = "Bronze" }; })
            .Build();

        var result = await enricher.EnrichAsync(Message<Customer>.Create(new Customer("Alice", null, null)));

        ScenarioExpect.Equal("Bronze", result.Message.Payload.Tier);
        ScenarioExpect.True(result.StepResults[0].Skipped);
        ScenarioExpect.True(result.StepResults[1].Applied);
    }

    [Scenario("EnrichAsync UseDefaultPolicy AppliesDefaultOnFailure")]
    [Fact]
    public async Task EnrichAsync_UseDefaultPolicy_AppliesDefaultOnFailure()
    {
        var enricher = AsyncContentEnricher<Customer>.Create()
            .Enrich("failing",
                async (c, _, _) => { await Task.CompletedTask; throw new InvalidOperationException(); },
                EnrichmentErrorPolicy.UseDefault,
                c => c with { Email = "noreply@default.com" })
            .Build();

        var result = await enricher.EnrichAsync(Message<Customer>.Create(new Customer("Alice", null, null)));

        ScenarioExpect.Equal("noreply@default.com", result.Message.Payload.Email);
        ScenarioExpect.True(result.StepResults[0].Skipped);
    }

    [Scenario("EnrichAsync StepAuditTrailCapturesException")]
    [Fact]
    public async Task EnrichAsync_StepAuditTrailCapturesException()
    {
        var enricher = AsyncContentEnricher<Customer>.Create()
            .WithDefaultPolicy(EnrichmentErrorPolicy.Skip)
            .Enrich("failing", async (c, _, _) => { await Task.CompletedTask; throw new InvalidOperationException("fetch error"); })
            .Build();

        var result = await enricher.EnrichAsync(Message<Customer>.Create(new Customer("Alice", null, null)));

        var failedStep = result.StepResults[0];
        ScenarioExpect.NotNull(failedStep.Exception);
        ScenarioExpect.Equal("InvalidOperationException", failedStep.Exception!.GetType().Name);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => AsyncContentEnricher<Customer>.Create(""));
        ScenarioExpect.Throws<ArgumentException>(() =>
            AsyncContentEnricher<Customer>.Create().Enrich("", async (c, _, _) => { await Task.CompletedTask; return c; }));
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            AsyncContentEnricher<Customer>.Create().Enrich("step", null!));
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncContentEnricher<Customer>.Create().Build());
    }

    [Scenario("EnrichAsync RejectsNullMessage")]
    [Fact]
    public async Task EnrichAsync_RejectsNullMessage()
    {
        var enricher = AsyncContentEnricher<Customer>.Create()
            .Enrich("step", async (c, _, _) => { await Task.CompletedTask; return c; })
            .Build();

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(() => enricher.EnrichAsync(null!).AsTask());
    }

    private sealed record Customer(string Name, string? Email, string? Tier);
}
