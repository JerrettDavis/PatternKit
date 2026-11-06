namespace PatternKit.Application.AntiCorruption;

/// <summary>
/// Outcome returned by an anti-corruption layer translation.
/// </summary>
public sealed class AntiCorruptionResult<TDomain>
{
    private AntiCorruptionResult(string sourceSystem, TDomain? value, bool accepted, string? rejectionReason)
    {
        SourceSystem = sourceSystem;
        Value = value;
        Accepted = accepted;
        RejectionReason = rejectionReason;
    }

    public string SourceSystem { get; }
    public TDomain? Value { get; }
    public bool Accepted { get; }
    public bool Rejected => !Accepted;
    public string? RejectionReason { get; }

    public static AntiCorruptionResult<TDomain> Success(string sourceSystem, TDomain value)
        => new(sourceSystem, value, true, null);

    public static AntiCorruptionResult<TDomain> Rejection(string sourceSystem, string reason)
        => new(sourceSystem, default, false, reason);
}

/// <summary>
/// Translates external models into a protected domain model while rejecting invalid external or domain shapes.
/// </summary>
public sealed class AntiCorruptionLayer<TExternal, TDomain>
{
    public delegate TDomain Translator(TExternal external);
    public delegate bool Validator<in T>(T value);

    private readonly Translator _translator;
    private readonly IReadOnlyList<ValidationRule<TExternal>> _externalRules;
    private readonly IReadOnlyList<ValidationRule<TDomain>> _domainRules;

    private AntiCorruptionLayer(
        string name,
        string sourceSystem,
        Translator translator,
        IReadOnlyList<ValidationRule<TExternal>> externalRules,
        IReadOnlyList<ValidationRule<TDomain>> domainRules)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Anti-corruption layer name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(sourceSystem))
            throw new ArgumentException("Source system is required.", nameof(sourceSystem));

        Name = name;
        SourceSystem = sourceSystem;
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _externalRules = externalRules ?? throw new ArgumentNullException(nameof(externalRules));
        _domainRules = domainRules ?? throw new ArgumentNullException(nameof(domainRules));
    }

    public string Name { get; }
    public string SourceSystem { get; }

    public static Builder Create(string name = "anti-corruption-layer") => new(name);

    public AntiCorruptionResult<TDomain> Translate(TExternal external)
    {
        if (external is null)
            throw new ArgumentNullException(nameof(external));

        foreach (var rule in _externalRules)
        {
            if (!rule.Predicate(external))
                return AntiCorruptionResult<TDomain>.Rejection(SourceSystem, rule.RejectionReason);
        }

        var domain = _translator(external);
        if (domain is null)
            return AntiCorruptionResult<TDomain>.Rejection(SourceSystem, "Translator returned a null domain value.");

        foreach (var rule in _domainRules)
        {
            if (!rule.Predicate(domain))
                return AntiCorruptionResult<TDomain>.Rejection(SourceSystem, rule.RejectionReason);
        }

        return AntiCorruptionResult<TDomain>.Success(SourceSystem, domain);
    }

    public async ValueTask<AntiCorruptionResult<TDomain>> TranslateAsync(
        TExternal external,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await new ValueTask<AntiCorruptionResult<TDomain>>(Translate(external)).ConfigureAwait(false);
    }

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<ValidationRule<TExternal>> _externalRules = [];
        private readonly List<ValidationRule<TDomain>> _domainRules = [];
        private string _sourceSystem = "external";
        private Translator? _translator;

        internal Builder(string name) => _name = name;

        public Builder FromSource(string sourceSystem)
        {
            _sourceSystem = sourceSystem;
            return this;
        }

        public Builder TranslateWith(Translator translator)
        {
            _translator = translator ?? throw new ArgumentNullException(nameof(translator));
            return this;
        }

        public Builder RejectExternalWhen(Validator<TExternal> predicate, string reason)
            => AddExternalRule(value => !predicate(value), reason);

        public Builder RequireExternal(Validator<TExternal> predicate, string reason)
            => AddExternalRule(predicate, reason);

        public Builder RejectDomainWhen(Validator<TDomain> predicate, string reason)
            => AddDomainRule(value => !predicate(value), reason);

        public Builder RequireDomain(Validator<TDomain> predicate, string reason)
            => AddDomainRule(predicate, reason);

        public AntiCorruptionLayer<TExternal, TDomain> Build()
        {
            if (_translator is null)
                throw new InvalidOperationException("Anti-corruption layer translator is required.");

            return new(_name, _sourceSystem, _translator, _externalRules.ToArray(), _domainRules.ToArray());
        }

        private Builder AddExternalRule(Validator<TExternal> predicate, string reason)
        {
            _externalRules.Add(new(predicate ?? throw new ArgumentNullException(nameof(predicate)), RequireReason(reason)));
            return this;
        }

        private Builder AddDomainRule(Validator<TDomain> predicate, string reason)
        {
            _domainRules.Add(new(predicate ?? throw new ArgumentNullException(nameof(predicate)), RequireReason(reason)));
            return this;
        }

        private static string RequireReason(string reason)
            => string.IsNullOrWhiteSpace(reason)
                ? throw new ArgumentException("Validation rejection reason is required.", nameof(reason))
                : reason;
    }

    private sealed class ValidationRule<T>
    {
        public ValidationRule(Validator<T> predicate, string rejectionReason)
        {
            Predicate = predicate;
            RejectionReason = rejectionReason;
        }

        public Validator<T> Predicate { get; }
        public string RejectionReason { get; }
    }
}
