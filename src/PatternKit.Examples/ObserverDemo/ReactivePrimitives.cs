using PatternKit.Behavioral.Observer;
using System.Collections;

namespace PatternKit.Examples.ObserverDemo;

/// <summary>
/// Lightweight property change hub (INotifyPropertyChanged-like) built on <see cref="Observer{TEvent}"/> of <see cref="string"/>.
/// Publishes property names to subscribers.
/// </summary>
public sealed class PropertyChangedHub
{
    private readonly Observer<string> _obs = Observer<string>.Create().Build();

    /// <summary>
    /// Subscribe to property name change notifications.
    /// </summary>
    /// <param name="onProperty">Callback that receives the property name.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    public IDisposable Subscribe(Action<string> onProperty) => _obs.Subscribe((in p) => onProperty(p));

    /// <summary>
    /// Raise a property change notification.
    /// </summary>
    /// <param name="propertyName">The property name to publish.</param>
    public void Raise(string propertyName) => _obs.Publish(in propertyName);
}

/// <summary>
/// A tiny observable variable that publishes change notifications when its value changes.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="initial">Optional initial value.</param>
public sealed class ObservableVar<T>(T initial = default!)
{
    private readonly Observer<(T Old, T New)> _obs = Observer<(T Old, T New)>.Create().Build();
    private T _value = initial;

    /// <summary>
    /// The current value. Setting this will publish a change event if the value actually changes.
    /// </summary>
    public T Value
    {
        get => _value;
        set
        {
            var old = _value;
            if (EqualityComparer<T>.Default.Equals(old, value)) return;
            _value = value;
            var ev = (Old: old, New: value);
            _obs.Publish(in ev);
        }
    }

    /// <summary>
    /// Subscribe to change notifications. The callback receives the old and new value.
    /// </summary>
    /// <param name="onChange">Callback invoked when <see cref="Value"/> changes.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    public IDisposable Subscribe(Action<T, T> onChange)
        => _obs.Subscribe((in e) => onChange(e.Old, e.New));
}

/// <summary>
/// Observable list wrapper that publishes simple add/remove events.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class ObservableList<T> : IEnumerable<T>
{
    private readonly List<T> _items = [];
    private readonly Observer<(string Action, T Item)> _obs = Observer<(string Action, T Item)>.Create().Build();

    /// <summary>The current number of items.</summary>
    public int Count => _items.Count;

    /// <summary>
    /// Return a shallow snapshot copy of the list's current items.
    /// </summary>
    public IReadOnlyList<T> Snapshot() => _items.ToArray();

    /// <summary>
    /// Add an item and publish an "add" event.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Add(T item)
    {
        _items.Add(item);
        var ev = (Action: "add", Item: item);
        _obs.Publish(in ev);
    }

    /// <summary>
    /// Remove an item and publish a "remove" event if the item existed.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns><see langword="true"/> if the item was removed; otherwise <see langword="false"/>.</returns>
    public bool Remove(T item)
    {
        var ok = _items.Remove(item);
        if (!ok)
            return ok;
        var ev = (Action: "remove", Item: item);
        _obs.Publish(in ev);
        return ok;
    }

    /// <summary>
    /// Subscribe to add/remove change notifications.
    /// </summary>
    /// <param name="onChange">Callback receiving the action ("add" or "remove") and the item.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    public IDisposable Subscribe(Action<string, T> onChange)
        => _obs.Subscribe((in e) => onChange(e.Action, e.Item));

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
