namespace PatternKit.Cloud.CacheStampedeProtection;

/// <summary>
/// Coordinates keyed cache misses so only one loader per key runs at a time while followers await the shared result.
/// </summary>
public sealed class CacheStampedeProtectionPolicy<TResult>
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Lazy<Task<CacheStampedeProtectionResult<TResult>>>> _inFlight = new(StringComparer.Ordinal);

    private CacheStampedeProtectionPolicy(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Cache stampede protection policy name is required.", nameof(name));

        Name = name;
    }

    public string Name { get; }

    public int InFlightCount
    {
        get
        {
            lock (_gate)
                return _inFlight.Count;
        }
    }

    public static Builder Create(string name = "cache-stampede-protection") => new(name);

    public ValueTask<CacheStampedeProtectionResult<TResult>> GetOrLoadAsync(
        string key,
        Func<CancellationToken, ValueTask<TResult>> loader,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        if (loader is null)
            throw new ArgumentNullException(nameof(loader));

        cancellationToken.ThrowIfCancellationRequested();

        Lazy<Task<CacheStampedeProtectionResult<TResult>>> lazy;
        var shared = false;
        lock (_gate)
        {
            if (!_inFlight.TryGetValue(key, out lazy!))
            {
                lazy = new Lazy<Task<CacheStampedeProtectionResult<TResult>>>(
                    () => LoadAndReleaseAsync(key, loader),
                    LazyThreadSafetyMode.ExecutionAndPublication);
                _inFlight.Add(key, lazy);
            }
            else
            {
                shared = true;
            }
        }

        return AwaitResultAsync(lazy.Value, shared, cancellationToken);
    }

    private async Task<CacheStampedeProtectionResult<TResult>> LoadAndReleaseAsync(
        string key,
        Func<CancellationToken, ValueTask<TResult>> loader)
    {
        try
        {
            var value = await loader(CancellationToken.None).ConfigureAwait(false);
            return new CacheStampedeProtectionResult<TResult>(key, value, sharedFlight: false);
        }
        finally
        {
            lock (_gate)
                _inFlight.Remove(key);
        }
    }

    private static async ValueTask<CacheStampedeProtectionResult<TResult>> AwaitResultAsync(
        Task<CacheStampedeProtectionResult<TResult>> task,
        bool shared,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.CanBeCanceled)
            await WhenCompletedOrCanceledAsync(task, cancellationToken).ConfigureAwait(false);

        var result = await task.ConfigureAwait(false);
        return shared ? result.AsSharedFlight() : result;
    }

    private static Task WhenCompletedOrCanceledAsync(Task task, CancellationToken cancellationToken)
    {
        if (task.IsCompleted)
            return task;

        var cancellation = new TaskCompletionSource<bool>();
        var registration = cancellationToken.Register(static state =>
        {
            var source = (TaskCompletionSource<bool>)state!;
            source.TrySetCanceled();
        }, cancellation);

        return CompleteWhenAnyAsync(task, cancellation.Task, registration);
    }

    private static async Task CompleteWhenAnyAsync(Task task, Task cancellation, CancellationTokenRegistration registration)
    {
        try
        {
            var completed = await Task.WhenAny(task, cancellation).ConfigureAwait(false);
            await completed.ConfigureAwait(false);
        }
        finally
        {
            registration.Dispose();
        }
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache stampede protection key is required.", nameof(key));
    }

    public sealed class Builder
    {
        private readonly string _name;

        internal Builder(string name)
            => _name = name;

        public CacheStampedeProtectionPolicy<TResult> Build()
            => new(_name);
    }
}

public sealed class CacheStampedeProtectionResult<TResult>
{
    public CacheStampedeProtectionResult(string key, TResult value, bool sharedFlight)
    {
        Key = key;
        Value = value;
        SharedFlight = sharedFlight;
    }

    public string Key { get; }

    public TResult Value { get; }

    public bool SharedFlight { get; }

    internal CacheStampedeProtectionResult<TResult> AsSharedFlight()
        => SharedFlight ? this : new(Key, Value, sharedFlight: true);
}
