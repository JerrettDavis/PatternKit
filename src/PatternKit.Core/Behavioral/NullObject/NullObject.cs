namespace PatternKit.Behavioral.NullObject;

/// <summary>
/// Fluent Null Object wrapper for a production fallback implementation of <typeparamref name="TContract"/>.
/// </summary>
/// <typeparam name="TContract">The service, strategy, or collaborator contract represented by the null object.</typeparam>
public sealed class NullObject<TContract>
    where TContract : class
{
    private readonly TContract _instance;

    private NullObject(TContract instance)
        => _instance = instance ?? throw new ArgumentNullException(nameof(instance));

    /// <summary>
    /// Gets the configured null object instance.
    /// </summary>
    public TContract Instance => _instance;

    /// <summary>
    /// Creates a builder from an existing null object instance.
    /// </summary>
    public static Builder Create(TContract instance) => new(instance);

    /// <summary>
    /// Creates a builder from a factory that produces the null object instance exactly once.
    /// </summary>
    public static Builder Create(Func<TContract> factory)
    {
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));

        return new Builder(factory());
    }

    /// <summary>
    /// Fluent builder for <see cref="NullObject{TContract}"/>.
    /// </summary>
    public sealed class Builder
    {
        private readonly TContract _instance;

        internal Builder(TContract instance)
            => _instance = instance ?? throw new ArgumentNullException(nameof(instance));

        /// <summary>
        /// Builds an immutable null object wrapper.
        /// </summary>
        public NullObject<TContract> Build() => new(_instance);
    }
}
