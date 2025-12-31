using System.Collections.Concurrent;

namespace PatternKit.Creational.AbstractFactory;

/// <summary>
/// Abstract Factory pattern implementation for creating families of related objects.
/// Provides a fluent API for defining product families and selecting them at runtime.
/// </summary>
/// <remarks>
/// <para>
/// The Abstract Factory pattern differs from the Factory Method pattern:
/// <list type="bullet">
/// <item><b>Factory Method:</b> Creates single products by key</item>
/// <item><b>Abstract Factory:</b> Creates families of related products that work together</item>
/// </list>
/// </para>
/// <para>
/// <b>Example use cases:</b>
/// <list type="bullet">
/// <item>UI theme factories (light/dark themes with matching buttons, textboxes, menus)</item>
/// <item>Database provider factories (SQL Server, PostgreSQL with matching connections, commands)</item>
/// <item>Document format factories (PDF, Word, Excel with matching readers, writers)</item>
/// </list>
/// </para>
/// <para>
/// <b>Thread-safety:</b> Factories built via <see cref="Builder.Build"/> are immutable and thread-safe.
/// Product families are also thread-safe. The builder is not thread-safe.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of key used to identify product families.</typeparam>
public sealed class AbstractFactory<TKey> where TKey : notnull
{
    private readonly IReadOnlyDictionary<TKey, ProductFamily> _families;
    private readonly ProductFamily? _default;

    private AbstractFactory(IReadOnlyDictionary<TKey, ProductFamily> families, ProductFamily? @default)
    {
        _families = families;
        _default = @default;
    }

    /// <summary>
    /// Gets a product family by key.
    /// </summary>
    /// <param name="key">The family key.</param>
    /// <returns>The product family for creating related products.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the family key is not registered and no default exists.</exception>
    public ProductFamily GetFamily(TKey key)
    {
        if (_families.TryGetValue(key, out var family))
            return family;

        if (_default is not null)
            return _default;

        throw new KeyNotFoundException($"No product family registered for key '{key}'");
    }

    /// <summary>
    /// Tries to get a product family by key.
    /// </summary>
    /// <param name="key">The family key.</param>
    /// <param name="family">The product family if found.</param>
    /// <returns><see langword="true"/> if the family was found or a default exists; otherwise <see langword="false"/>.</returns>
    public bool TryGetFamily(TKey key, out ProductFamily family)
    {
        if (_families.TryGetValue(key, out family!))
            return true;

        if (_default is not null)
        {
            family = _default;
            return true;
        }

        family = default!;
        return false;
    }

    /// <summary>
    /// Checks if a family is registered for the specified key.
    /// </summary>
    /// <param name="key">The family key.</param>
    /// <returns><see langword="true"/> if the family exists; otherwise <see langword="false"/>.</returns>
    public bool HasFamily(TKey key) => _families.ContainsKey(key);

    /// <summary>
    /// Gets all registered family keys.
    /// </summary>
    public IEnumerable<TKey> FamilyKeys => _families.Keys;

    /// <summary>Creates a new fluent builder for an abstract factory.</summary>
    public static Builder Create() => new();

    /// <summary>Creates a new fluent builder with a custom key comparer.</summary>
    public static Builder Create(IEqualityComparer<TKey> comparer) => new(comparer);

    /// <summary>
    /// Represents a family of related products that can be created together.
    /// </summary>
    public sealed class ProductFamily
    {
        private readonly ConcurrentDictionary<Type, Delegate> _creators;

        internal ProductFamily(ConcurrentDictionary<Type, Delegate> creators)
        {
            _creators = creators;
        }

        /// <summary>
        /// Creates a product of the specified type.
        /// </summary>
        /// <typeparam name="TProduct">The product type to create.</typeparam>
        /// <returns>A new instance of the product.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no creator is registered for the product type.</exception>
        public TProduct Create<TProduct>()
        {
            if (_creators.TryGetValue(typeof(TProduct), out var creator))
                return ((Func<TProduct>)creator)();

            throw new InvalidOperationException($"No creator registered for product type '{typeof(TProduct).Name}'");
        }

        /// <summary>
        /// Tries to create a product of the specified type.
        /// </summary>
        /// <typeparam name="TProduct">The product type to create.</typeparam>
        /// <param name="product">The created product if successful.</param>
        /// <returns><see langword="true"/> if the product was created; otherwise <see langword="false"/>.</returns>
        public bool TryCreate<TProduct>(out TProduct product)
        {
            if (_creators.TryGetValue(typeof(TProduct), out var creator))
            {
                product = ((Func<TProduct>)creator)();
                return true;
            }

            product = default!;
            return false;
        }

        /// <summary>
        /// Checks if a creator is registered for the specified product type.
        /// </summary>
        /// <typeparam name="TProduct">The product type to check.</typeparam>
        /// <returns><see langword="true"/> if a creator exists; otherwise <see langword="false"/>.</returns>
        public bool CanCreate<TProduct>() => _creators.ContainsKey(typeof(TProduct));
    }

    /// <summary>Fluent builder for configuring product families.</summary>
    public sealed class Builder
    {
        private readonly Dictionary<TKey, ConcurrentDictionary<Type, Delegate>> _families;
        private readonly IEqualityComparer<TKey>? _comparer;
        private ConcurrentDictionary<Type, Delegate>? _defaultFamily;
        private TKey? _currentFamily;

        internal Builder(IEqualityComparer<TKey>? comparer = null)
        {
            _comparer = comparer;
            _families = comparer is not null
                ? new Dictionary<TKey, ConcurrentDictionary<Type, Delegate>>(comparer)
                : new Dictionary<TKey, ConcurrentDictionary<Type, Delegate>>();
        }

        /// <summary>
        /// Starts or switches to a product family definition.
        /// Subsequent <see cref="Product{TProduct}(Func{TProduct})"/> calls will register to this family.
        /// </summary>
        /// <param name="key">The family key.</param>
        /// <returns>This builder for method chaining.</returns>
        public Builder Family(TKey key)
        {
            _currentFamily = key;
            if (!_families.ContainsKey(key))
                _families[key] = new ConcurrentDictionary<Type, Delegate>();
            return this;
        }

        /// <summary>
        /// Registers a product creator for the current family.
        /// </summary>
        /// <typeparam name="TProduct">The product type.</typeparam>
        /// <param name="creator">The factory function to create the product.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no family has been selected with <see cref="Family(TKey)"/>.</exception>
        public Builder Product<TProduct>(Func<TProduct> creator)
        {
            if (_currentFamily is null)
                throw new InvalidOperationException("Call Family() before registering products");

            _families[_currentFamily][typeof(TProduct)] = creator;
            return this;
        }

        /// <summary>
        /// Starts or switches to the default family definition.
        /// The default family is used when a requested family key is not found.
        /// </summary>
        /// <returns>This builder for method chaining.</returns>
        public Builder DefaultFamily()
        {
            _currentFamily = default;
            _defaultFamily ??= new ConcurrentDictionary<Type, Delegate>();
            return this;
        }

        /// <summary>
        /// Registers a product creator for the default family.
        /// </summary>
        public Builder DefaultProduct<TProduct>(Func<TProduct> creator)
        {
            _defaultFamily ??= new ConcurrentDictionary<Type, Delegate>();
            _defaultFamily[typeof(TProduct)] = creator;
            return this;
        }

        /// <summary>
        /// Builds the immutable abstract factory.
        /// </summary>
        /// <returns>A new abstract factory instance.</returns>
        public AbstractFactory<TKey> Build()
        {
            var families = _families.ToDictionary(
                kvp => kvp.Key,
                kvp => new ProductFamily(kvp.Value),
                _comparer);

            var defaultFamily = _defaultFamily is not null
                ? new ProductFamily(_defaultFamily)
                : null;

            return new AbstractFactory<TKey>(families, defaultFamily);
        }
    }
}
