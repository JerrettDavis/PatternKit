using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Transformation;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates the Claim Check pattern with large order documents.
/// </summary>
public static class LargeDocumentClaimCheckExample
{
    public static LargeDocumentClaimCheckSummary RunFluent()
    {
        var claimCheck = LargeDocumentClaimCheckPolicies.CreateFluentClaimCheck(new InMemoryClaimCheckStore<LargeOrderDocument>());
        var original = CreateDocumentMessage("doc-100");
        var claim = claimCheck.Store(original);
        var restored = claimCheck.Restore(claim);

        return LargeDocumentClaimCheckSummary.From("fluent", claim, restored);
    }

    public static LargeDocumentClaimCheckSummary RunGenerated()
    {
        var claimCheck = GeneratedLargeDocumentClaimCheck.Create();
        var original = CreateDocumentMessage("doc-100");
        var claim = claimCheck.Store(original);
        var restored = claimCheck.Restore(claim);

        return LargeDocumentClaimCheckSummary.From("source-generated", claim, restored);
    }

    public static Message<LargeOrderDocument> CreateDocumentMessage(string documentId)
        => Message<LargeOrderDocument>
            .Create(new LargeOrderDocument(documentId, "tenant-a", """{"lines":[{"sku":"SKU-1","qty":10}]}"""))
            .WithMessageId($"doc:{documentId}")
            .WithCorrelationId(documentId)
            .WithHeader("tenant-id", "tenant-a");
}

public static class LargeDocumentClaimCheckPolicies
{
    public static ClaimCheck<LargeOrderDocument> CreateFluentClaimCheck(IClaimCheckStore<LargeOrderDocument> store)
        => ClaimCheck<LargeOrderDocument>
            .Create("large-document-claim-check")
            .InStore("document-archive")
            .UseStore(store)
            .UseClaimIds(static (message, _) => $"order-doc:{message.Headers.MessageId}")
            .Build();
}

public sealed class LargeDocumentWorkflow(ClaimCheck<LargeOrderDocument> claimCheck)
{
    public LargeDocumentClaimCheckSummary Process(Message<LargeOrderDocument> document)
    {
        var claim = claimCheck.Store(document);
        var restored = claimCheck.Restore(claim);
        return LargeDocumentClaimCheckSummary.From("di", claim, restored);
    }
}

public static class LargeDocumentClaimCheckServiceCollectionExtensions
{
    public static IServiceCollection AddLargeDocumentClaimCheckExample(this IServiceCollection services)
    {
        services.AddSingleton<IClaimCheckStore<LargeOrderDocument>, InMemoryClaimCheckStore<LargeOrderDocument>>();
        services.AddSingleton(sp => LargeDocumentClaimCheckPolicies.CreateFluentClaimCheck(sp.GetRequiredService<IClaimCheckStore<LargeOrderDocument>>()));
        services.AddSingleton<LargeDocumentWorkflow>();
        services.AddSingleton(new LargeDocumentClaimCheckExampleRunner(
            LargeDocumentClaimCheckExample.RunFluent,
            LargeDocumentClaimCheckExample.RunGenerated));
        return services;
    }
}

public sealed record LargeDocumentClaimCheckExampleRunner(
    Func<LargeDocumentClaimCheckSummary> RunFluent,
    Func<LargeDocumentClaimCheckSummary> RunGenerated);

[GenerateClaimCheck(typeof(LargeOrderDocument), ClaimCheckName = "large-document-claim-check", StoreName = "document-archive", ClaimIdPrefix = "order-doc")]
public static partial class GeneratedLargeDocumentClaimCheck
{
    [ClaimCheckStoreFactory]
    private static IClaimCheckStore<LargeOrderDocument> CreateStore() => new InMemoryClaimCheckStore<LargeOrderDocument>();
}

public sealed record LargeOrderDocument(string DocumentId, string TenantId, string Json);

public sealed record LargeDocumentClaimCheckSummary(
    string Path,
    string ClaimId,
    string StoreName,
    bool Restored,
    string? DocumentId,
    string? TenantId,
    string? CorrelationId)
{
    public static LargeDocumentClaimCheckSummary From(
        string path,
        Message<ClaimCheckReference> claim,
        ClaimCheckRestoreResult<LargeOrderDocument> restored)
        => new(
            path,
            claim.Payload.ClaimId,
            claim.Payload.StoreName,
            restored.Restored,
            restored.Message?.Payload.DocumentId,
            restored.Message?.Payload.TenantId,
            restored.Message?.Headers.CorrelationId);
}
