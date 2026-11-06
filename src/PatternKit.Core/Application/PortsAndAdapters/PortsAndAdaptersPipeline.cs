namespace PatternKit.Application.PortsAndAdapters;

/// <summary>Primary port exposed by a Ports and Adapters boundary.</summary>
public interface IPortsAndAdaptersPipeline<TInbound, TCommand, TResult, TOutbound>
{
    string Name { get; }

    ValueTask<TOutbound> ExecuteAsync(TInbound inbound, CancellationToken cancellationToken = default);
}

/// <summary>Fluent Ports and Adapters pipeline that isolates delivery DTOs from application use cases.</summary>
public sealed class PortsAndAdaptersPipeline<TInbound, TCommand, TResult, TOutbound> : IPortsAndAdaptersPipeline<TInbound, TCommand, TResult, TOutbound>
{
    private readonly Func<TInbound, TCommand> _inboundAdapter;
    private readonly Func<TCommand, CancellationToken, ValueTask<TResult>> _applicationPort;
    private readonly Func<TResult, TOutbound> _outboundAdapter;

    private PortsAndAdaptersPipeline(
        string name,
        Func<TInbound, TCommand> inboundAdapter,
        Func<TCommand, CancellationToken, ValueTask<TResult>> applicationPort,
        Func<TResult, TOutbound> outboundAdapter)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Ports and Adapters pipeline name is required.", nameof(name))
            : name;
        _inboundAdapter = inboundAdapter ?? throw new ArgumentNullException(nameof(inboundAdapter));
        _applicationPort = applicationPort ?? throw new ArgumentNullException(nameof(applicationPort));
        _outboundAdapter = outboundAdapter ?? throw new ArgumentNullException(nameof(outboundAdapter));
    }

    public string Name { get; }

    public static Builder Create(string name = "ports-and-adapters") => new(name);

    public async ValueTask<TOutbound> ExecuteAsync(TInbound inbound, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (inbound is null)
            throw new ArgumentNullException(nameof(inbound));

        var command = _inboundAdapter(inbound);
        var result = await _applicationPort(command, cancellationToken).ConfigureAwait(false);
        return _outboundAdapter(result);
    }

    public sealed class Builder
    {
        private readonly string _name;
        private Func<TInbound, TCommand>? _inboundAdapter;
        private Func<TCommand, CancellationToken, ValueTask<TResult>>? _applicationPort;
        private Func<TResult, TOutbound>? _outboundAdapter;

        internal Builder(string name) => _name = name;

        public Builder AdaptInboundWith(Func<TInbound, TCommand> adapter)
        {
            _inboundAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            return this;
        }

        public Builder HandleWith(Func<TCommand, CancellationToken, ValueTask<TResult>> applicationPort)
        {
            _applicationPort = applicationPort ?? throw new ArgumentNullException(nameof(applicationPort));
            return this;
        }

        public Builder AdaptOutboundWith(Func<TResult, TOutbound> adapter)
        {
            _outboundAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            return this;
        }

        public PortsAndAdaptersPipeline<TInbound, TCommand, TResult, TOutbound> Build()
        {
            if (_inboundAdapter is null)
                throw new InvalidOperationException("Ports and Adapters pipeline requires an inbound adapter.");
            if (_applicationPort is null)
                throw new InvalidOperationException("Ports and Adapters pipeline requires an application port.");
            if (_outboundAdapter is null)
                throw new InvalidOperationException("Ports and Adapters pipeline requires an outbound adapter.");

            return new(_name, _inboundAdapter, _applicationPort, _outboundAdapter);
        }
    }
}
