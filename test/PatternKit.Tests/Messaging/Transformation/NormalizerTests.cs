using PatternKit.Messaging.Transformation;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Transformation;

public sealed class NormalizerTests
{
    [Scenario("NormalizeAsync FirstMatchWins")]
    [Fact]
    public async Task NormalizeAsync_FirstMatchWins()
    {
        var normalizer = Normalizer<string, Order>.Create()
            .When(s => s.StartsWith("JSON:"), "json").Normalize(async (s, _) => { await Task.CompletedTask; return new Order(s[5..], "json"); })
            .When(s => s.StartsWith("XML:"), "xml").Normalize(async (s, _) => { await Task.CompletedTask; return new Order(s[4..], "xml"); })
            .Build();

        var result = await normalizer.NormalizeAsync("JSON:order-1");

        ScenarioExpect.True(result.Normalized);
        ScenarioExpect.Equal("order-1", result.Canonical!.Id);
        ScenarioExpect.Equal("json", result.Canonical.Format);
        ScenarioExpect.Equal("json", result.HandlerName);
    }

    [Scenario("NormalizeAsync SecondHandlerMatchesWhenFirstDoesNot")]
    [Fact]
    public async Task NormalizeAsync_SecondHandlerMatchesWhenFirstDoesNot()
    {
        var normalizer = Normalizer<string, Order>.Create()
            .When(s => s.StartsWith("JSON:")).Normalize(async (s, _) => { await Task.CompletedTask; return new Order(s, "json"); })
            .When(s => s.StartsWith("XML:")).Normalize(async (s, _) => { await Task.CompletedTask; return new Order(s[4..], "xml"); })
            .Build();

        var result = await normalizer.NormalizeAsync("XML:order-2");

        ScenarioExpect.True(result.Normalized);
        ScenarioExpect.Equal("xml", result.Canonical!.Format);
    }

    [Scenario("NormalizeAsync DefaultHandlerUsedWhenNoMatchFound")]
    [Fact]
    public async Task NormalizeAsync_DefaultHandlerUsedWhenNoMatchFound()
    {
        var normalizer = Normalizer<string, Order>.Create()
            .When(s => s.StartsWith("JSON:")).Normalize(async (s, _) => { await Task.CompletedTask; return new Order(s, "json"); })
            .Default(async (s, _) => { await Task.CompletedTask; return new Order(s, "unknown"); })
            .Build();

        var result = await normalizer.NormalizeAsync("CSV:data");

        ScenarioExpect.True(result.Normalized);
        ScenarioExpect.Equal("unknown", result.Canonical!.Format);
        ScenarioExpect.Equal("default", result.HandlerName);
    }

    [Scenario("NormalizeAsync NoMatchAndNoDefault ReturnsMiss")]
    [Fact]
    public async Task NormalizeAsync_NoMatchAndNoDefault_ReturnsMiss()
    {
        var normalizer = Normalizer<string, Order>.Create()
            .When(s => s.StartsWith("JSON:")).Normalize(async (s, _) => { await Task.CompletedTask; return new Order(s, "json"); })
            .Build();

        var result = await normalizer.NormalizeAsync("CSV:data");

        ScenarioExpect.False(result.Normalized);
        ScenarioExpect.NotNull(result.MissReason);
    }

    [Scenario("Builder RequiresAtLeastOneHandlerOrDefault")]
    [Fact]
    public void Builder_RequiresAtLeastOneHandlerOrDefault()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            Normalizer<string, Order>.Create().Build());
    }

    [Scenario("Builder RejectsNullPredicate")]
    [Fact]
    public void Builder_RejectsNullPredicate()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            Normalizer<string, Order>.Create().When(null!));
    }

    [Scenario("NormalizeAsync RespectsCancellation")]
    [Fact]
    public async Task NormalizeAsync_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var normalizer = Normalizer<string, Order>.Create()
            .When(s => true).Normalize(async (s, ct) => { await Task.Delay(100, ct); return new Order(s, "x"); })
            .Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() => normalizer.NormalizeAsync("data", cts.Token).AsTask());
    }

    [Scenario("NormalizeAsync ContentPredicateReceivesActualValue")]
    [Fact]
    public async Task NormalizeAsync_ContentPredicateReceivesActualValue()
    {
        string? seenRaw = null;
        var normalizer = Normalizer<string, Order>.Create()
            .When(s => { seenRaw = s; return true; }).Normalize(async (s, _) => { await Task.CompletedTask; return new Order(s, "x"); })
            .Build();

        await normalizer.NormalizeAsync("my-raw-value");

        ScenarioExpect.Equal("my-raw-value", seenRaw);
    }

    private sealed record Order(string Id, string Format);
}
