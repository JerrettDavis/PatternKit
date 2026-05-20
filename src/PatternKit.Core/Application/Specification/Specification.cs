namespace PatternKit.Application.Specification;

/// <summary>
/// Represents a business rule that can decide whether a candidate satisfies a named condition.
/// </summary>
/// <typeparam name="T">Candidate type evaluated by the rule.</typeparam>
public interface ISpecification<in T>
{
    /// <summary>Evaluates the candidate against the rule.</summary>
    bool IsSatisfiedBy(T candidate);
}

/// <summary>
/// Fluent specification implementation with composition helpers for domain rules.
/// </summary>
/// <typeparam name="T">Candidate type evaluated by the rule.</typeparam>
public sealed class Specification<T> : ISpecification<T>
{
    private readonly Func<T, bool> _predicate;

    private Specification(string name, Func<T, bool> predicate)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Specification name is required.", nameof(name))
            : name;
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    /// <summary>Human-readable rule name used by registries and diagnostics.</summary>
    public string Name { get; }

    /// <summary>Creates a specification from a predicate.</summary>
    public static Specification<T> Where(string name, Func<T, bool> predicate) => new(name, predicate);

    /// <summary>Creates a specification that accepts every candidate.</summary>
    public static Specification<T> All(string name = "all") => new(name, static _ => true);

    /// <summary>Creates a specification that rejects every candidate.</summary>
    public static Specification<T> None(string name = "none") => new(name, static _ => false);

    /// <inheritdoc />
    public bool IsSatisfiedBy(T candidate) => _predicate(candidate);

    /// <summary>Returns this specification as a predicate delegate.</summary>
    public Func<T, bool> ToPredicate() => _predicate;

    /// <summary>Composes this specification with another specification using logical AND.</summary>
    public Specification<T> And(ISpecification<T> other, string? name = null)
    {
        if (other is null)
            throw new ArgumentNullException(nameof(other));

        return new Specification<T>(name ?? $"{Name}.and", candidate => IsSatisfiedBy(candidate) && other.IsSatisfiedBy(candidate));
    }

    /// <summary>Composes this specification with another specification using logical OR.</summary>
    public Specification<T> Or(ISpecification<T> other, string? name = null)
    {
        if (other is null)
            throw new ArgumentNullException(nameof(other));

        return new Specification<T>(name ?? $"{Name}.or", candidate => IsSatisfiedBy(candidate) || other.IsSatisfiedBy(candidate));
    }

    /// <summary>Negates this specification.</summary>
    public Specification<T> Not(string? name = null)
        => new(name ?? $"{Name}.not", candidate => !IsSatisfiedBy(candidate));
}

/// <summary>
/// Named collection of specifications for application services and IoC registrations.
/// </summary>
/// <typeparam name="T">Candidate type evaluated by the registered rules.</typeparam>
public sealed class SpecificationRegistry<T>
{
    private readonly IReadOnlyDictionary<string, ISpecification<T>> _specifications;

    private SpecificationRegistry(IReadOnlyDictionary<string, ISpecification<T>> specifications)
        => _specifications = specifications;

    /// <summary>Registered specification names.</summary>
    public IReadOnlyCollection<string> Names => _specifications.Keys.ToArray();

    /// <summary>Creates a fluent registry builder.</summary>
    public static Builder Create() => new();

    /// <summary>Gets a specification by name.</summary>
    public ISpecification<T> Get(string name)
    {
        if (!_specifications.TryGetValue(name, out var specification))
            throw new KeyNotFoundException($"Specification '{name}' is not registered.");

        return specification;
    }

    /// <summary>Evaluates a named specification against a candidate.</summary>
    public bool IsSatisfiedBy(string name, T candidate) => Get(name).IsSatisfiedBy(candidate);

    /// <summary>Builds a named specification registry.</summary>
    public sealed class Builder
    {
        private readonly Dictionary<string, ISpecification<T>> _specifications = new(StringComparer.Ordinal);

        /// <summary>Adds a specification to the registry.</summary>
        public Builder Add(string name, ISpecification<T> specification)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Specification name is required.", nameof(name));

            _specifications[name] = specification ?? throw new ArgumentNullException(nameof(specification));
            return this;
        }

        /// <summary>Adds a predicate-backed specification to the registry.</summary>
        public Builder Add(string name, Func<T, bool> predicate)
            => Add(name, Specification<T>.Where(name, predicate));

        /// <summary>Builds an immutable registry snapshot.</summary>
        public SpecificationRegistry<T> Build()
            => new(new Dictionary<string, ISpecification<T>>(_specifications, StringComparer.Ordinal));
    }
}
