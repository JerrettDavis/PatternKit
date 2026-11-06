using PatternKit.Messaging;
using PatternKit.Messaging.Transformation;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Transformation;

public sealed class ClaimCheckTests
{
    private sealed record LargeOrderDocument(string DocumentId, string Json);

    [Scenario("Claim check stores and restores payloads")]
    [Fact]
    public void ClaimCheck_Stores_And_Restores_Payloads()
    {
        var claimCheck = CreateClaimCheck();
        var message = CreateMessage("doc-100");

        var claim = claimCheck.Store(message);
        var restored = claimCheck.Restore(claim);

        ScenarioExpect.Equal("claim:msg-doc-100", claim.Payload.ClaimId);
        ScenarioExpect.Equal("documents", claim.Payload.StoreName);
        ScenarioExpect.Equal("corr-doc-100", claim.Headers.CorrelationId);
        ScenarioExpect.True(restored.Restored);
        ScenarioExpect.Equal(message.Payload, restored.Message!.Payload);
        ScenarioExpect.Equal("corr-doc-100", restored.Message.Headers.CorrelationId);
    }

    [Scenario("Claim check reports missing references")]
    [Fact]
    public void ClaimCheck_Reports_Missing_References()
    {
        var claimCheck = CreateClaimCheck();
        var missing = Message<ClaimCheckReference>.Create(new ClaimCheckReference("missing", "documents", "LargeOrderDocument", DateTimeOffset.UtcNow));

        var result = claimCheck.Restore(missing);

        ScenarioExpect.True(result.Missing);
        ScenarioExpect.Contains("missing", result.MissReason!);
    }

    [Scenario("Async claim check preserves cancellation")]
    [Fact]
    public async Task AsyncClaimCheck_Preserves_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var claimCheck = CreateClaimCheck();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() =>
            claimCheck.StoreAsync(CreateMessage("doc-100"), cancellationToken: cts.Token).AsTask());
    }

    [Scenario("Claim check rejects invalid configuration")]
    [Fact]
    public void ClaimCheck_Rejects_Invalid_Configuration()
    {
        var claimCheck = CreateClaimCheck();

        ScenarioExpect.Throws<ArgumentException>(() => ClaimCheck<LargeOrderDocument>.Create("").Build());
        ScenarioExpect.Throws<ArgumentException>(() => ClaimCheck<LargeOrderDocument>.Create().InStore("").Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => ClaimCheck<LargeOrderDocument>.Create().UseStore(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => ClaimCheck<LargeOrderDocument>.Create().UseClaimIds(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => claimCheck.Store(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => claimCheck.Restore(null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => ClaimCheck<LargeOrderDocument>.Create().UseClaimIds(static (_, _) => "").Build().Store(CreateMessage("doc-100")));
    }

    private static ClaimCheck<LargeOrderDocument> CreateClaimCheck()
        => ClaimCheck<LargeOrderDocument>
            .Create("large-documents")
            .InStore("documents")
            .UseClaimIds(static (message, _) => $"claim:{message.Headers.MessageId}")
            .Build();

    private static Message<LargeOrderDocument> CreateMessage(string documentId)
        => Message<LargeOrderDocument>
            .Create(new LargeOrderDocument(documentId, """{"lines":[1,2,3]}"""))
            .WithMessageId($"msg-{documentId}")
            .WithCorrelationId($"corr-{documentId}");
}
