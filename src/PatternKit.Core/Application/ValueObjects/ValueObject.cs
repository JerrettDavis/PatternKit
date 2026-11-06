namespace PatternKit.Application.ValueObjects;

/// <summary>
/// Base class for immutable domain value objects that compare by their component values.
/// </summary>
/// <typeparam name="TSelf">The concrete value object type.</typeparam>
public abstract class ValueObject<TSelf> : IEquatable<TSelf>
    where TSelf : ValueObject<TSelf>
{
    public bool Equals(TSelf? other)
    {
        if (ReferenceEquals(null, other))
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj)
        => obj is TSelf other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var component in GetEqualityComponents())
                hash = (hash * 31) + (component?.GetHashCode() ?? 0);

            return hash;
        }
    }

    public static bool operator ==(ValueObject<TSelf>? left, ValueObject<TSelf>? right)
        => left?.Equals(right as TSelf) ?? right is null;

    public static bool operator !=(ValueObject<TSelf>? left, ValueObject<TSelf>? right)
        => !(left == right);

    protected abstract IEnumerable<object?> GetEqualityComponents();
}

/// <summary>
/// Fluent factory and validator for creating value objects through named domain rules.
/// </summary>
public sealed class ValueObjectFactory<TValue>
{
    private readonly Func<TValue> _create;
    private readonly List<ValueObjectRule<TValue>> _rules = [];

    private ValueObjectFactory(Func<TValue> create)
    {
        _create = create ?? throw new ArgumentNullException(nameof(create));
    }

    public static ValueObjectFactory<TValue> Create(Func<TValue> create)
        => new(create);

    public ValueObjectFactory<TValue> Ensure(string name, Func<TValue, bool> predicate, string message)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Rule name is required.", nameof(name));
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Rule message is required.", nameof(message));

        _rules.Add(new ValueObjectRule<TValue>(name, predicate, message));
        return this;
    }

    public ValueObjectResult<TValue> Build()
    {
        var value = _create();
        var failures = _rules
            .Where(rule => !rule.Predicate(value))
            .Select(static rule => new ValueObjectValidationFailure(rule.Name, rule.Message))
            .ToArray();

        return failures.Length == 0
            ? ValueObjectResult<TValue>.Success(value)
            : ValueObjectResult<TValue>.Failure(value, failures);
    }
}

public sealed class ValueObjectRule<TValue>
{
    public ValueObjectRule(string name, Func<TValue, bool> predicate, string message)
    {
        Name = name;
        Predicate = predicate;
        Message = message;
    }

    public string Name { get; }

    public Func<TValue, bool> Predicate { get; }

    public string Message { get; }
}

public sealed class ValueObjectValidationFailure
{
    public ValueObjectValidationFailure(string rule, string message)
    {
        Rule = rule;
        Message = message;
    }

    public string Rule { get; }

    public string Message { get; }
}

public sealed class ValueObjectResult<TValue>
{
    public ValueObjectResult(TValue value, IReadOnlyList<ValueObjectValidationFailure> failures)
    {
        Value = value;
        Failures = failures;
    }

    public TValue Value { get; }

    public IReadOnlyList<ValueObjectValidationFailure> Failures { get; }

    public bool IsValid => Failures.Count == 0;

    public static ValueObjectResult<TValue> Success(TValue value) => new(value, Array.Empty<ValueObjectValidationFailure>());

    public static ValueObjectResult<TValue> Failure(TValue value, IReadOnlyList<ValueObjectValidationFailure> failures)
        => new(value, failures);
}
