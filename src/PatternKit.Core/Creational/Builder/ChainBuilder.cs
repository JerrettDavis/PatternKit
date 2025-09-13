namespace PatternKit.Creational.Builder;

public sealed class ChainBuilder<T>
{
    private readonly List<T> _items = new(8);

    private ChainBuilder()
    {
    }

    public static ChainBuilder<T> Create() => new();

    public ChainBuilder<T> Add(T item)
    {
        _items.Add(item);
        return this;
    }

    public ChainBuilder<T> AddIf(bool condition, T item)
    {
        if (condition) _items.Add(item);
        return this;
    }

    public TProduct Build<TProduct>(Func<T[], TProduct> projector)
        => projector(_items.ToArray());
}