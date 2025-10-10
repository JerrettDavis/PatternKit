using PatternKit.Behavioral.Observer;
using System.Collections;

namespace PatternKit.Examples.ObserverDemo;

/// <summary>Lightweight property change hub (INotifyPropertyChanged-like) built on Observer&lt;string&gt;.</summary>
public sealed class PropertyChangedHub
{
    private readonly Observer<string> _obs = Observer<string>.Create().Build();
    public IDisposable Subscribe(Action<string> onProperty) => _obs.Subscribe((in p) => onProperty(p));
    public void Raise(string propertyName) => _obs.Publish(in propertyName);
}

/// <summary>A tiny observable variable that publishes change notifications when its value changes.</summary>
public sealed class ObservableVar<T>(T initial = default!)
{
    private readonly Observer<(T Old, T New)> _obs = Observer<(T Old, T New)>.Create().Build();
    private T _value = initial;

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

    public IDisposable Subscribe(Action<T, T> onChange)
        => _obs.Subscribe((in e) => onChange(e.Old, e.New));
}

/// <summary>Observable list wrapper that publishes simple add/remove events.</summary>
public sealed class ObservableList<T> : IEnumerable<T>
{
    private readonly List<T> _items = [];
    private readonly Observer<(string Action, T Item)> _obs = Observer<(string Action, T Item)>.Create().Build();

    public int Count => _items.Count;
    public IReadOnlyList<T> Snapshot() => _items.ToArray();

    public void Add(T item)
    {
        _items.Add(item);
        var ev = (Action: "add", Item: item);
        _obs.Publish(in ev);
    }

    public bool Remove(T item)
    {
        var ok = _items.Remove(item);
        if (!ok)
            return ok;
        var ev = (Action: "remove", Item: item);
        _obs.Publish(in ev);
        return ok;
    }

    public IDisposable Subscribe(Action<string, T> onChange)
        => _obs.Subscribe((in e) => onChange(e.Action, e.Item));

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

