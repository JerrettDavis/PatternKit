namespace PatternKit.Creational.Builder;

/// <summary>
/// Reusable, allocation-light, fluent builder for collecting predicate/handler pairs + optional default,
/// and projecting them into a concrete product.
/// </summary>
/// <typeparam name="TPred">Delegate type of the predicate (e.g., bool Predicate(in TIn)).</typeparam>
/// <typeparam name="THandler">Delegate type of the handler (e.g., TOut Handler(in TIn) or void Action(in TIn)).</typeparam>
public sealed class BranchBuilder<TPred, THandler>
{
    private readonly List<TPred> _preds = new(8);
    private readonly List<THandler> _handlers = new(8);
    private THandler? _default;

    private BranchBuilder()
    {
    }

    public static BranchBuilder<TPred, THandler> Create() => new();

    /// <summary>Adds a predicate/handler pair.</summary>
    public BranchBuilder<TPred, THandler> Add(TPred predicate, THandler handler)
    {
        _preds.Add(predicate);
        _handlers.Add(handler);
        return this;
    }

    /// <summary>Sets (or replaces) the default handler.</summary>
    public BranchBuilder<TPred, THandler> Default(THandler handler)
    {
        _default = handler;
        return this;
    }

    /// <summary>
    /// Builds a product using the collected pairs and default. If no default was set,
    /// <paramref name="fallbackDefault"/> is supplied and flagged as not user-configured.
    /// </summary>
    /// <typeparam name="TProduct">The product type to construct.</typeparam>
    /// <param name="fallbackDefault">Default handler to use when none explicitly configured.</param>
    /// <param name="projector">
    /// (predicates, handlers, hasDefault, @default) → TProduct
    /// </param>
    public TProduct Build<TProduct>(
        THandler fallbackDefault,
        Func<TPred[], THandler[], bool, THandler, TProduct> projector)
    {
        var preds = _preds.ToArray();
        var handlers = _handlers.ToArray();
        var hasDefault = _default is not null;
        var def = _default ?? fallbackDefault;
        return projector(preds, handlers, hasDefault, def);
    }
}