namespace PatternKit.Cloud.ExternalConfigurationStore;

/// <summary>
/// Centralized external configuration store with typed async loading, validation, and optional caching.
/// </summary>
public sealed class ExternalConfigurationStore<TSettings>
{
    /// <summary>Loads the current configuration snapshot from an external source.</summary>
    public delegate ValueTask<ExternalConfigurationSnapshot<TSettings>> ConfigurationLoader(CancellationToken cancellationToken);

    /// <summary>Validates a loaded settings object.</summary>
    public delegate bool ConfigurationValidator(TSettings settings);

    private readonly string _name;
    private readonly ConfigurationLoader _loader;
    private readonly ValidationRule[] _validators;
    private readonly TimeSpan _cacheDuration;
    private readonly object _gate = new();
    private ExternalConfigurationSnapshot<TSettings>? _cached;

    private ExternalConfigurationStore(
        string name,
        ConfigurationLoader loader,
        ValidationRule[] validators,
        TimeSpan cacheDuration)
        => (_name, _loader, _validators, _cacheDuration) = (name, loader, validators, cacheDuration);

    /// <summary>Gets validated configuration, reusing the cached snapshot while it remains fresh.</summary>
    public async ValueTask<ExternalConfigurationResult<TSettings>> GetAsync(CancellationToken cancellationToken = default)
    {
        var cached = GetFreshCachedSnapshot();
        if (cached is not null)
            return Validate(cached);

        var loaded = await _loader(cancellationToken).ConfigureAwait(false);
        var result = Validate(loaded);
        if (result.Succeeded)
        {
            lock (_gate)
                _cached = loaded;
        }

        return result;
    }

    /// <summary>Creates a new external configuration store builder.</summary>
    public static Builder Create(string name = "external-configuration-store") => new(name);

    private ExternalConfigurationSnapshot<TSettings>? GetFreshCachedSnapshot()
    {
        if (_cacheDuration <= TimeSpan.Zero)
            return null;

        lock (_gate)
        {
            if (_cached is null)
                return null;

            return DateTimeOffset.UtcNow - _cached.LoadedAtUtc <= _cacheDuration ? _cached : null;
        }
    }

    private ExternalConfigurationResult<TSettings> Validate(ExternalConfigurationSnapshot<TSettings> snapshot)
    {
        foreach (var validator in _validators)
        {
            if (!validator.Predicate(snapshot.Settings))
                return ExternalConfigurationResult<TSettings>.Rejected(_name, snapshot, validator.RejectionReason);
        }

        return ExternalConfigurationResult<TSettings>.Accepted(_name, snapshot);
    }

    /// <summary>Fluent builder for <see cref="ExternalConfigurationStore{TSettings}"/>.</summary>
    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<ValidationRule> _validators = new(4);
        private ConfigurationLoader? _loader;
        private TimeSpan _cacheDuration = TimeSpan.Zero;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("External configuration store name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        /// <summary>Registers the async external configuration loader.</summary>
        public Builder LoadFrom(ConfigurationLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            return this;
        }

        /// <summary>Adds a validation rule for loaded settings.</summary>
        public Builder ValidateWith(string rejectionReason, ConfigurationValidator validator)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
                throw new ArgumentException("Validation rejection reason cannot be null, empty, or whitespace.", nameof(rejectionReason));
            if (validator is null)
                throw new ArgumentNullException(nameof(validator));

            _validators.Add(new ValidationRule(rejectionReason, validator));
            return this;
        }

        /// <summary>Configures how long successful snapshots should be cached.</summary>
        public Builder CacheFor(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(duration), "Cache duration cannot be negative.");

            _cacheDuration = duration;
            return this;
        }

        /// <summary>Builds an immutable external configuration store.</summary>
        public ExternalConfigurationStore<TSettings> Build()
        {
            if (_loader is null)
                throw new InvalidOperationException("External configuration store requires a loader.");

            return new ExternalConfigurationStore<TSettings>(_name, _loader, _validators.ToArray(), _cacheDuration);
        }
    }

    private sealed class ValidationRule
    {
        public ValidationRule(string rejectionReason, ConfigurationValidator predicate)
            => (RejectionReason, Predicate) = (rejectionReason, predicate);

        public string RejectionReason { get; }

        public ConfigurationValidator Predicate { get; }
    }
}

/// <summary>Loaded configuration snapshot with source version metadata.</summary>
public sealed class ExternalConfigurationSnapshot<TSettings>
{
    public ExternalConfigurationSnapshot(TSettings settings, string version, DateTimeOffset loadedAtUtc)
    {
        if (settings is null)
            throw new ArgumentNullException(nameof(settings));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Configuration version cannot be null, empty, or whitespace.", nameof(version));

        Settings = settings;
        Version = version;
        LoadedAtUtc = loadedAtUtc;
    }

    /// <summary>The loaded typed settings.</summary>
    public TSettings Settings { get; }

    /// <summary>External store version, revision, or etag.</summary>
    public string Version { get; }

    /// <summary>UTC timestamp when the snapshot was loaded.</summary>
    public DateTimeOffset LoadedAtUtc { get; }
}

/// <summary>Validated configuration result returned by <see cref="ExternalConfigurationStore{TSettings}"/>.</summary>
public sealed class ExternalConfigurationResult<TSettings>
{
    private ExternalConfigurationResult(
        string storeName,
        ExternalConfigurationSnapshot<TSettings> snapshot,
        bool succeeded,
        string? rejectionReason)
        => (StoreName, Snapshot, Succeeded, RejectionReason) = (storeName, snapshot, succeeded, rejectionReason);

    /// <summary>The store name.</summary>
    public string StoreName { get; }

    /// <summary>The loaded snapshot.</summary>
    public ExternalConfigurationSnapshot<TSettings> Snapshot { get; }

    /// <summary>True when all validation rules accepted the snapshot.</summary>
    public bool Succeeded { get; }

    /// <summary>Validation failure reason when rejected.</summary>
    public string? RejectionReason { get; }

    /// <summary>Creates an accepted result.</summary>
    public static ExternalConfigurationResult<TSettings> Accepted(string storeName, ExternalConfigurationSnapshot<TSettings> snapshot)
        => new(storeName, snapshot, true, null);

    /// <summary>Creates a rejected result.</summary>
    public static ExternalConfigurationResult<TSettings> Rejected(string storeName, ExternalConfigurationSnapshot<TSettings> snapshot, string rejectionReason)
        => new(storeName, snapshot, false, rejectionReason);
}
