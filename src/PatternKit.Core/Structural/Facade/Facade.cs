namespace PatternKit.Structural.Facade;

/// <summary>
/// Fluent, allocation-light facade that provides a simplified interface to complex subsystem operations.
/// Maps named operations to coordinated subsystem interactions. Build once, then call <see cref="Execute"/> or
/// <see cref="TryExecute"/> with an operation name.
/// </summary>
/// <typeparam name="TIn">Input type for operations.</typeparam>
/// <typeparam name="TOut">Output type produced by operations.</typeparam>
/// <remarks>
/// <para>
/// <b>Mental model</b>: A <i>facade</i> hides the complexity of multiple subsystems behind a simple,
/// unified interface. Instead of clients needing to understand and coordinate multiple subsystem calls,
/// they invoke a single named operation on the facade.
/// </para>
/// <para>
/// <b>Use cases</b>:
/// <list type="bullet">
///   <item><description>Simplify complex library or framework APIs.</description></item>
///   <item><description>Coordinate multiple service calls into single operations.</description></item>
///   <item><description>Provide a cleaner API for legacy or third-party code.</description></item>
///   <item><description>Decouple clients from subsystem implementation details.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Immutability</b>: After <see cref="Builder.Build"/>, the facade is immutable and safe for concurrent reuse.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var orderFacade = Facade&lt;OrderRequest, OrderResult&gt;.Create()
///     .Operation("process", req => {
///         var inventory = inventoryService.Reserve(req.Items);
///         var payment = paymentService.Charge(req.Payment);
///         var shipment = shippingService.Schedule(req.Address);
///         return new OrderResult(inventory, payment, shipment);
///     })
///     .Operation("cancel", req => {
///         inventoryService.Release(req.OrderId);
///         paymentService.Refund(req.OrderId);
///         return new OrderResult { Status = "Cancelled" };
///     })
///     .Default(req => new OrderResult { Status = "Unknown" })
///     .Build();
///
/// var result = orderFacade.Execute("process", request);
/// </code>
/// </example>
public sealed class Facade<TIn, TOut>
{
    /// <summary>
    /// Delegate representing a facade operation that coordinates subsystem interactions.
    /// </summary>
    /// <param name="input">The input value (readonly via <c>in</c>).</param>
    /// <returns>The result after coordinating subsystem calls.</returns>
    public delegate TOut Operation(in TIn input);

    private readonly Dictionary<string, int> _operationIndices;
    private readonly Operation[] _operations;
    private readonly Operation? _default;

    private Facade(Dictionary<string, int> operationIndices, Operation[] operations, Operation? defaultOp)
    {
        _operationIndices = operationIndices;
        _operations = operations;
        _default = defaultOp;
    }

    /// <summary>
    /// Executes the named operation with the given <paramref name="input"/>.
    /// </summary>
    /// <param name="operationName">The name of the operation to execute.</param>
    /// <param name="input">The input value (readonly via <c>in</c>).</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the operation is not found and no default is configured.</exception>
    /// <remarks>
    /// If the operation name is not found, the default operation is invoked (if configured).
    /// Otherwise, an exception is thrown.
    /// </remarks>
    public TOut Execute(string operationName, in TIn input)
    {
        if (_operationIndices.TryGetValue(operationName, out var index))
            return _operations[index](in input);

        return _default is not null 
            ? _default(in input) 
            : throw new InvalidOperationException($"Operation '{operationName}' not found and no default configured.");
    }

    /// <summary>
    /// Attempts to execute the named operation. Returns <c>false</c> if the operation is not found
    /// and no default is configured.
    /// </summary>
    /// <param name="operationName">The name of the operation to execute.</param>
    /// <param name="input">The input value (readonly via <c>in</c>).</param>
    /// <param name="output">The result of the operation, or <c>default</c> on failure.</param>
    /// <returns><c>true</c> if the operation was found or a default exists; otherwise <c>false</c>.</returns>
    public bool TryExecute(string operationName, in TIn input, out TOut output)
    {
        if (_operationIndices.TryGetValue(operationName, out var index))
        {
            output = _operations[index](in input);
            return true;
        }

        if (_default is not null)
        {
            output = _default(in input);
            return true;
        }

        output = default!;
        return false;
    }

    /// <summary>
    /// Checks whether an operation with the given name is registered.
    /// </summary>
    /// <param name="operationName">The operation name to check.</param>
    /// <returns><c>true</c> if the operation exists; otherwise <c>false</c>.</returns>
    public bool HasOperation(string operationName)
        => _operationIndices.ContainsKey(operationName);

    /// <summary>
    /// Creates a new <see cref="Builder"/> for constructing a facade.
    /// </summary>
    /// <returns>A new <see cref="Builder"/> instance.</returns>
    /// <example>
    /// <code language="csharp">
    /// var facade = Facade&lt;int, string&gt;.Create()
    ///     .Operation("double", x => (x * 2).ToString())
    ///     .Operation("square", x => (x * x).ToString())
    ///     .Build();
    ///
    /// var result = facade.Execute("double", 5); // "10"
    /// </code>
    /// </example>
    public static Builder Create() => new();

    /// <summary>
    /// Fluent builder for <see cref="Facade{TIn, TOut}"/>.
    /// </summary>
    public sealed class Builder
    {
        private readonly Dictionary<string, int> _operationIndices;
        private readonly List<Operation> _operations = new(8);
        private Operation? _default;
        private StringComparer _comparer = StringComparer.Ordinal;

        internal Builder() 
        { 
            _operationIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Registers a named operation that coordinates subsystem interactions.
        /// </summary>
        /// <param name="name">The unique name for this operation.</param>
        /// <param name="handler">The operation delegate.</param>
        /// <returns>This builder for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when an operation with the same name is already registered.</exception>
        /// <remarks>
        /// Operation names are case-sensitive. If you need case-insensitive names, use <see cref="OperationIgnoreCase"/>.
        /// </remarks>
        public Builder Operation(string name, Operation handler)
        {
            if (_operationIndices.ContainsKey(name))
                throw new ArgumentException($"Operation '{name}' is already registered.", nameof(name));

            _operationIndices[name] = _operations.Count;
            _operations.Add(handler);
            return this;
        }

        /// <summary>
        /// Registers a named operation with case-insensitive matching.
        /// </summary>
        /// <param name="name">The unique name for this operation (case-insensitive).</param>
        /// <param name="handler">The operation delegate.</param>
        /// <returns>This builder for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when an operation with the same name (ignoring case) is already registered.</exception>
        public Builder OperationIgnoreCase(string name, Operation handler)
        {
            // Switch to case-insensitive comparer if this is the first case-insensitive operation
            if (_comparer == StringComparer.Ordinal && _operations.Count == 0)
            {
                _comparer = StringComparer.OrdinalIgnoreCase;
                var newDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in _operationIndices)
                    newDict[kvp.Key] = kvp.Value;
                _operationIndices.Clear();
                foreach (var kvp in newDict)
                    _operationIndices[kvp.Key] = kvp.Value;
            }
            else if (_comparer == StringComparer.Ordinal)
            {
                throw new InvalidOperationException(
                    "Cannot mix case-sensitive and case-insensitive operations. Use OperationIgnoreCase for all operations or none.");
            }

            if (_operationIndices.ContainsKey(name))
                throw new ArgumentException($"Operation '{name}' is already registered.", nameof(name));

            _operationIndices[name] = _operations.Count;
            _operations.Add(handler);
            return this;
        }

        /// <summary>
        /// Configures a default operation to invoke when the requested operation is not found.
        /// </summary>
        /// <param name="handler">The default operation delegate.</param>
        /// <returns>This builder for chaining.</returns>
        /// <remarks>
        /// Only one default operation can be configured. Calling this multiple times will replace the previous default.
        /// </remarks>
        public Builder Default(Operation handler)
        {
            _default = handler;
            return this;
        }

        /// <summary>
        /// Builds an immutable <see cref="Facade{TIn, TOut}"/> with the registered operations.
        /// </summary>
        /// <returns>An immutable facade instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no operations are registered.</exception>
        public Facade<TIn, TOut> Build()
        {
            if (_operations.Count == 0 && _default is null)
                throw new InvalidOperationException("At least one operation or a default must be configured.");

            return new Facade<TIn, TOut>(
                new Dictionary<string, int>(_operationIndices, _comparer),
                _operations.ToArray(),
                _default);
        }
    }
}
