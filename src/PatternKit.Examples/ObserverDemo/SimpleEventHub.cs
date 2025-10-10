namespace PatternKit.Examples.ObserverDemo;

using Behavioral.Observer;

/// <summary>
/// A tiny demo event hub built on top of <see cref="Observer{TEvent}"/> for examples and docs.
/// </summary>
/// <typeparam name="TEvent">The event payload type to broadcast.</typeparam>
public sealed class EventHub<TEvent>
{
    private readonly Observer<TEvent> _observer;

    /// <summary>
    /// Create an event hub that wraps the provided <see cref="Observer{TEvent}"/>.
    /// </summary>
    /// <param name="observer">The underlying observer to delegate to.</param>
    public EventHub(Observer<TEvent> observer) => _observer = observer;

    /// <summary>
    /// Create a default event hub using <see cref="Observer{TEvent}.Builder"/> with default error handling (aggregate).
    /// </summary>
    /// <returns>A new <see cref="EventHub{TEvent}"/>.</returns>
    public static EventHub<TEvent> CreateDefault()
        => new(Observer<TEvent>.Create().Build());

    /// <summary>
    /// Subscribe a handler to receive all events.
    /// </summary>
    /// <param name="handler">The handler to invoke.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    public IDisposable On(Observer<TEvent>.Handler handler)
        => _observer.Subscribe(handler);

    /// <summary>
    /// Subscribe a handler with an optional predicate filter.
    /// </summary>
    /// <param name="predicate">The filter to decide whether to deliver the event to the handler.</param>
    /// <param name="handler">The handler to invoke.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    public IDisposable On(Observer<TEvent>.Predicate predicate, Observer<TEvent>.Handler handler)
        => _observer.Subscribe(predicate, handler);

    /// <summary>
    /// Publish an event to all matching subscribers.
    /// </summary>
    /// <param name="evt">The event value.</param>
    public void Publish(in TEvent evt) => _observer.Publish(in evt);
}

/// <summary>Sample event used in docs and tests for the observer demo.</summary>
/// <param name="Id">The unique user identifier.</param>
/// <param name="Action">The action performed by the user.</param>
public readonly record struct UserEvent(int Id, string Action);
