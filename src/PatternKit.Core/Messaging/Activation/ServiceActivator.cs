namespace PatternKit.Messaging.Activation;

/// <summary>Activates an application service operation from a typed message.</summary>
public sealed class ServiceActivator<TRequest, TResponse>
{
    public delegate Message<TResponse> ServiceHandler(Message<TRequest> request, MessageContext context);

    private readonly ServiceHandler _handler;

    private ServiceActivator(string name, ServiceHandler handler)
        => (Name, _handler) = (name, handler);

    public string Name { get; }

    public ServiceActivatorResult<TRequest, TResponse> Activate(Message<TRequest> request, MessageContext? context = null)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var effectiveContext = context ?? MessageContext.From(request);
        var response = _handler(request, effectiveContext);
        if (response is null)
            throw new InvalidOperationException("Service activator handler returned null.");

        return new(Name, request, response);
    }

    public static Builder Create(string name = "service-activator") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private ServiceHandler? _handler;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Service activator name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        public Builder Handle(ServiceHandler handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        public ServiceActivator<TRequest, TResponse> Build()
        {
            if (_handler is null)
                throw new InvalidOperationException("Service activator requires a handler.");

            return new(_name, _handler);
        }
    }
}

public sealed class ServiceActivatorResult<TRequest, TResponse>
{
    public ServiceActivatorResult(
        string activatorName,
        Message<TRequest> request,
        Message<TResponse> response)
        => (ActivatorName, Request, Response) = (activatorName, request, response);

    public string ActivatorName { get; }

    public Message<TRequest> Request { get; }

    public Message<TResponse> Response { get; }

    public bool Completed => Response is not null;
}
