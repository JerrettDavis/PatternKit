namespace PatternKit.Messaging.ControlBus;

/// <summary>
/// Dispatches operational control messages to named command handlers.
/// </summary>
public sealed class ControlBus<TCommand>
{
    /// <summary>Selects the operational command name from a message.</summary>
    public delegate string CommandNameSelector(Message<TCommand> message, MessageContext context);

    /// <summary>Executes an operational command.</summary>
    public delegate ControlBusResult<TCommand> CommandHandler(Message<TCommand> message, MessageContext context);

    private readonly string _name;
    private readonly CommandNameSelector _selector;
    private readonly Dictionary<string, HandlerRegistration> _handlers;

    private ControlBus(string name, CommandNameSelector selector, Dictionary<string, HandlerRegistration> handlers)
        => (_name, _selector, _handlers) = (name, selector, handlers);

    /// <summary>Dispatches a control message to a registered handler.</summary>
    public ControlBusResult<TCommand> Dispatch(Message<TCommand> message, MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message);
        var commandName = _selector(message, effectiveContext);
        if (string.IsNullOrWhiteSpace(commandName))
            return ControlBusResult<TCommand>.Rejected(_name, commandName ?? string.Empty, message, "Control command name was not supplied.");

        if (!_handlers.TryGetValue(commandName, out var registration))
            return ControlBusResult<TCommand>.Rejected(_name, commandName, message, "No control bus handler is registered for the command.");

        var result = registration.Handler(message, effectiveContext);
        return result.WithDispatchMetadata(_name, commandName, registration.Name, message);
    }

    /// <summary>Creates a control bus builder.</summary>
    public static Builder Create(string name = "control-bus") => new(name);

    /// <summary>Fluent builder for <see cref="ControlBus{TCommand}"/>.</summary>
    public sealed class Builder
    {
        private readonly string _name;
        private readonly Dictionary<string, HandlerRegistration> _handlers = new(StringComparer.Ordinal);
        private CommandNameSelector _selector = static (message, _) =>
            message.Headers.GetString(ControlBusHeaders.CommandName) ?? string.Empty;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Control bus name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        /// <summary>Configures command name selection.</summary>
        public Builder SelectCommand(CommandNameSelector selector)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            return this;
        }

        /// <summary>Registers a command handler.</summary>
        public Builder Handle(string commandName, string handlerName, CommandHandler handler)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                throw new ArgumentException("Control command name cannot be null, empty, or whitespace.", nameof(commandName));
            if (string.IsNullOrWhiteSpace(handlerName))
                throw new ArgumentException("Control handler name cannot be null, empty, or whitespace.", nameof(handlerName));
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));
            if (_handlers.ContainsKey(commandName))
                throw new InvalidOperationException("Control bus command names must be unique.");

            _handlers.Add(commandName, new HandlerRegistration(handlerName, handler));
            return this;
        }

        /// <summary>Builds an immutable control bus.</summary>
        public ControlBus<TCommand> Build()
        {
            if (_handlers.Count == 0)
                throw new InvalidOperationException("Control bus must have at least one command handler.");

            return new ControlBus<TCommand>(_name, _selector, new Dictionary<string, HandlerRegistration>(_handlers, StringComparer.Ordinal));
        }
    }

    private sealed class HandlerRegistration
    {
        public HandlerRegistration(string name, CommandHandler handler)
            => (Name, Handler) = (name, handler);

        public string Name { get; }

        public CommandHandler Handler { get; }
    }
}

/// <summary>Well-known control bus headers.</summary>
public static class ControlBusHeaders
{
    /// <summary>Header that contains the operational command name.</summary>
    public const string CommandName = "control-command";
}

/// <summary>Result returned by a control bus dispatch.</summary>
public sealed class ControlBusResult<TCommand>
{
    private ControlBusResult(
        string busName,
        string commandName,
        string? handlerName,
        Message<TCommand>? message,
        bool succeeded,
        string? rejectionReason)
        => (BusName, CommandName, HandlerName, Message, Succeeded, RejectionReason) = (busName, commandName, handlerName, message, succeeded, rejectionReason);

    /// <summary>Name of the control bus.</summary>
    public string BusName { get; }

    /// <summary>Operational command name.</summary>
    public string CommandName { get; }

    /// <summary>Handler that executed the command.</summary>
    public string? HandlerName { get; }

    /// <summary>Original control message.</summary>
    public Message<TCommand>? Message { get; }

    /// <summary>True when the control command completed.</summary>
    public bool Succeeded { get; }

    /// <summary>Rejection reason when the command failed.</summary>
    public string? RejectionReason { get; }

    /// <summary>Creates a successful control result.</summary>
    public static ControlBusResult<TCommand> Success()
        => new(string.Empty, string.Empty, null, null, true, null);

    /// <summary>Creates a rejected control result.</summary>
    public static ControlBusResult<TCommand> Failure(string rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new ArgumentException("Rejection reason cannot be null, empty, or whitespace.", nameof(rejectionReason));

        return new ControlBusResult<TCommand>(string.Empty, string.Empty, null, null, false, rejectionReason);
    }

    internal static ControlBusResult<TCommand> Rejected(string busName, string commandName, Message<TCommand> message, string rejectionReason)
        => new(busName, commandName, null, message, false, rejectionReason);

    internal ControlBusResult<TCommand> WithDispatchMetadata(string busName, string commandName, string handlerName, Message<TCommand> message)
        => new(busName, commandName, handlerName, message, Succeeded, RejectionReason);
}
