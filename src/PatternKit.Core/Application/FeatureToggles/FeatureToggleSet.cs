namespace PatternKit.Application.FeatureToggles;

public interface IFeatureToggleSet<TContext>
{
    string Name { get; }

    FeatureToggleDecision Evaluate(string toggleName, TContext context);

    bool IsEnabled(string toggleName, TContext context);
}

public sealed class FeatureToggleSet<TContext> : IFeatureToggleSet<TContext>
{
    private readonly Dictionary<string, FeatureToggleRule<TContext>> _rules;

    private FeatureToggleSet(string name, Dictionary<string, FeatureToggleRule<TContext>> rules)
    {
        Name = name;
        _rules = rules;
    }

    public string Name { get; }

    public static Builder Create(string name) => new(name);

    public FeatureToggleDecision Evaluate(string toggleName, TContext context)
    {
        ValidateToggleName(toggleName);

        if (!_rules.TryGetValue(toggleName, out var rule))
            return FeatureToggleDecision.Missing(toggleName, $"Feature toggle '{toggleName}' is not configured in '{Name}'.");

        return rule.Evaluate(context);
    }

    public bool IsEnabled(string toggleName, TContext context) => Evaluate(toggleName, context).Enabled;

    private static void ValidateToggleName(string toggleName)
    {
        if (string.IsNullOrWhiteSpace(toggleName))
            throw new ArgumentException("Feature toggle name is required.", nameof(toggleName));
    }

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<FeatureToggleRule<TContext>> _rules = [];
        private IEqualityComparer<string> _comparer = StringComparer.Ordinal;

        internal Builder(string name)
        {
            _name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Feature toggle set name is required.", nameof(name))
                : name;
        }

        public Builder UseComparer(IEqualityComparer<string> comparer)
        {
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            return this;
        }

        public Builder AddStatic(string toggleName, bool enabled)
        {
            _rules.Add(FeatureToggleRule<TContext>.Static(toggleName, enabled));
            return this;
        }

        public Builder AddRule(string toggleName, bool defaultEnabled, Func<TContext, bool> predicate)
        {
            _rules.Add(FeatureToggleRule<TContext>.Conditional(toggleName, defaultEnabled, predicate));
            return this;
        }

        public FeatureToggleSet<TContext> Build()
        {
            var map = new Dictionary<string, FeatureToggleRule<TContext>>(_comparer);
            foreach (var rule in _rules)
            {
                if (map.ContainsKey(rule.Name))
                    throw new InvalidOperationException($"Feature toggle '{rule.Name}' is configured more than once in '{_name}'.");

                map.Add(rule.Name, rule);
            }

            return new FeatureToggleSet<TContext>(_name, map);
        }
    }
}

public sealed class FeatureToggleRule<TContext>
{
    private readonly Func<TContext, bool>? _predicate;

    private FeatureToggleRule(string name, bool defaultEnabled, Func<TContext, bool>? predicate)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Feature toggle rule name is required.", nameof(name))
            : name;
        DefaultEnabled = defaultEnabled;
        _predicate = predicate;
    }

    public string Name { get; }

    public bool DefaultEnabled { get; }

    public static FeatureToggleRule<TContext> Static(string name, bool enabled) => new(name, enabled, null);

    public static FeatureToggleRule<TContext> Conditional(string name, bool defaultEnabled, Func<TContext, bool> predicate)
        => new(name, defaultEnabled, predicate ?? throw new ArgumentNullException(nameof(predicate)));

    public FeatureToggleDecision Evaluate(TContext context)
    {
        if (_predicate is null)
            return FeatureToggleDecision.Configured(Name, DefaultEnabled, $"Feature toggle '{Name}' is statically configured.");

        var enabled = _predicate(context) || DefaultEnabled;
        return FeatureToggleDecision.Configured(Name, enabled, $"Feature toggle '{Name}' was evaluated by a contextual rule.");
    }
}

public sealed class FeatureToggleDecision
{
    private FeatureToggleDecision(string toggleName, bool enabled, bool found, string reason)
    {
        ToggleName = string.IsNullOrWhiteSpace(toggleName)
            ? throw new ArgumentException("Feature toggle decision name is required.", nameof(toggleName))
            : toggleName;
        Enabled = enabled;
        Found = found;
        Reason = string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("Feature toggle decision reason is required.", nameof(reason))
            : reason;
    }

    public string ToggleName { get; }

    public bool Enabled { get; }

    public bool Found { get; }

    public string Reason { get; }

    public static FeatureToggleDecision Configured(string toggleName, bool enabled, string reason)
        => new(toggleName, enabled, true, reason);

    public static FeatureToggleDecision Missing(string toggleName, string reason)
        => new(toggleName, false, false, reason);
}
