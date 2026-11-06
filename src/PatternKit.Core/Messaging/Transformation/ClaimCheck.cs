using System.Collections.Concurrent;

namespace PatternKit.Messaging.Transformation;

/// <summary>
/// Reference passed through a message flow after the original payload is stored externally.
/// </summary>
public sealed class ClaimCheckReference
{
    public ClaimCheckReference(string claimId, string storeName, string payloadType, DateTimeOffset checkedAt)
    {
        ClaimId = string.IsNullOrWhiteSpace(claimId) ? throw new ArgumentException("Claim id is required.", nameof(claimId)) : claimId;
        StoreName = string.IsNullOrWhiteSpace(storeName) ? throw new ArgumentException("Store name is required.", nameof(storeName)) : storeName;
        PayloadType = string.IsNullOrWhiteSpace(payloadType) ? throw new ArgumentException("Payload type is required.", nameof(payloadType)) : payloadType;
        CheckedAt = checkedAt;
    }

    public string ClaimId { get; }
    public string StoreName { get; }
    public string PayloadType { get; }
    public DateTimeOffset CheckedAt { get; }
}

/// <summary>
/// Result returned when resolving a claim check reference.
/// </summary>
public sealed class ClaimCheckRestoreResult<TPayload>
{
    private ClaimCheckRestoreResult(Message<TPayload>? message, bool restored, string? missReason)
    {
        Message = message;
        Restored = restored;
        MissReason = missReason;
    }

    public Message<TPayload>? Message { get; }
    public bool Restored { get; }
    public bool Missing => !Restored;
    public string? MissReason { get; }

    public static ClaimCheckRestoreResult<TPayload> Success(Message<TPayload> message)
        => new(message ?? throw new ArgumentNullException(nameof(message)), true, null);

    public static ClaimCheckRestoreResult<TPayload> Miss(string reason)
        => new(null, false, string.IsNullOrWhiteSpace(reason) ? "Claim check payload was not found." : reason);
}

/// <summary>
/// Payload store used by the Claim Check pattern.
/// </summary>
public interface IClaimCheckStore<TPayload>
{
    ValueTask StoreAsync(string claimId, TPayload payload, MessageHeaders headers, CancellationToken cancellationToken = default);
    ValueTask<ClaimCheckStoredPayload<TPayload>?> TryLoadAsync(string claimId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stored payload plus the headers captured at check-in time.
/// </summary>
public sealed class ClaimCheckStoredPayload<TPayload>
{
    public ClaimCheckStoredPayload(TPayload payload, MessageHeaders headers)
    {
        Payload = payload;
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
    }

    public TPayload Payload { get; }
    public MessageHeaders Headers { get; }
}

/// <summary>
/// In-memory claim-check store for tests, samples, and single-process applications.
/// </summary>
public sealed class InMemoryClaimCheckStore<TPayload> : IClaimCheckStore<TPayload>
{
    private readonly ConcurrentDictionary<string, ClaimCheckStoredPayload<TPayload>> _items = new(StringComparer.Ordinal);

    public ValueTask StoreAsync(string claimId, TPayload payload, MessageHeaders headers, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(claimId))
            throw new ArgumentException("Claim id is required.", nameof(claimId));

        _items[claimId] = new(payload, headers ?? throw new ArgumentNullException(nameof(headers)));
        return default;
    }

    public ValueTask<ClaimCheckStoredPayload<TPayload>?> TryLoadAsync(string claimId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(claimId))
            throw new ArgumentException("Claim id is required.", nameof(claimId));

        _items.TryGetValue(claimId, out var payload);
        return new(payload);
    }
}

/// <summary>
/// Stores large payloads externally, passes a claim reference through the flow, and restores the payload later.
/// </summary>
public sealed class ClaimCheck<TPayload>
{
    public delegate string ClaimIdFactory(Message<TPayload> message, MessageContext context);

    private readonly IClaimCheckStore<TPayload> _store;
    private readonly ClaimIdFactory _claimIds;

    private ClaimCheck(string name, string storeName, IClaimCheckStore<TPayload> store, ClaimIdFactory claimIds)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Claim check name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(storeName))
            throw new ArgumentException("Claim check store name is required.", nameof(storeName));

        Name = name;
        StoreName = storeName;
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _claimIds = claimIds ?? throw new ArgumentNullException(nameof(claimIds));
    }

    public string Name { get; }
    public string StoreName { get; }

    public static Builder Create(string name = "claim-check") => new(name);

    public ValueTask<Message<ClaimCheckReference>> StoreAsync(
        Message<TPayload> message,
        MessageContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message, cancellationToken);
        return StoreCoreAsync(message, effectiveContext, cancellationToken);
    }

    public Message<ClaimCheckReference> Store(Message<TPayload> message, MessageContext? context = null)
        => StoreAsync(message, context).GetAwaiter().GetResult();

    public ValueTask<ClaimCheckRestoreResult<TPayload>> RestoreAsync(
        Message<ClaimCheckReference> claim,
        CancellationToken cancellationToken = default)
    {
        if (claim is null)
            throw new ArgumentNullException(nameof(claim));

        return RestoreCoreAsync(claim, cancellationToken);
    }

    public ClaimCheckRestoreResult<TPayload> Restore(Message<ClaimCheckReference> claim)
        => RestoreAsync(claim).GetAwaiter().GetResult();

    private async ValueTask<Message<ClaimCheckReference>> StoreCoreAsync(
        Message<TPayload> message,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var claimId = _claimIds(message, context);
        if (string.IsNullOrWhiteSpace(claimId))
            throw new InvalidOperationException("Claim id factory returned an empty claim id.");

        await _store.StoreAsync(claimId, message.Payload, message.Headers, cancellationToken).ConfigureAwait(false);
        var reference = new ClaimCheckReference(claimId, StoreName, typeof(TPayload).FullName ?? typeof(TPayload).Name, DateTimeOffset.UtcNow);
        return new Message<ClaimCheckReference>(reference, message.Headers.With("claim-check-id", claimId).With("claim-check-store", StoreName));
    }

    private async ValueTask<ClaimCheckRestoreResult<TPayload>> RestoreCoreAsync(
        Message<ClaimCheckReference> claim,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stored = await _store.TryLoadAsync(claim.Payload.ClaimId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
            return ClaimCheckRestoreResult<TPayload>.Miss($"Claim '{claim.Payload.ClaimId}' was not found in store '{StoreName}'.");

        return ClaimCheckRestoreResult<TPayload>.Success(new Message<TPayload>(stored.Payload, stored.Headers));
    }

    public sealed class Builder
    {
        private readonly string _name;
        private string _storeName = "claim-store";
        private IClaimCheckStore<TPayload>? _store;
        private ClaimIdFactory _claimIds = static (message, _) => message.Headers.MessageId ?? Guid.NewGuid().ToString("N");

        internal Builder(string name) => _name = name;

        public Builder UseStore(IClaimCheckStore<TPayload> store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            return this;
        }

        public Builder InStore(string storeName)
        {
            _storeName = storeName;
            return this;
        }

        public Builder UseClaimIds(ClaimIdFactory factory)
        {
            _claimIds = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        public ClaimCheck<TPayload> Build()
            => new(_name, _storeName, _store ?? new InMemoryClaimCheckStore<TPayload>(), _claimIds);
    }
}
