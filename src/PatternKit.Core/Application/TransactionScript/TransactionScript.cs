namespace PatternKit.Application.TransactionScript;

/// <summary>Application service operation that executes one request workflow as an explicit transaction script.</summary>
public interface ITransactionScript<TRequest, TResponse>
{
    string Name { get; }

    ValueTask<TransactionScriptResult<TResponse>> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Fluent Transaction Script implementation for request/application workflows.</summary>
public sealed class TransactionScript<TRequest, TResponse> : ITransactionScript<TRequest, TResponse>
{
    private readonly Func<TRequest, IEnumerable<TransactionScriptError>> _validator;
    private readonly Func<TRequest, CancellationToken, ValueTask<TResponse>> _handler;

    private TransactionScript(
        string name,
        Func<TRequest, IEnumerable<TransactionScriptError>> validator,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> handler)
    {
        Name = name;
        _validator = validator;
        _handler = handler;
    }

    public string Name { get; }

    public static Builder Create(string name)
        => new(name);

    public async ValueTask<TransactionScriptResult<TResponse>> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        cancellationToken.ThrowIfCancellationRequested();
        var errors = (_validator(request) ?? Array.Empty<TransactionScriptError>())
            .Where(static error => error is not null)
            .ToArray();
        if (errors.Length > 0)
            return TransactionScriptResult<TResponse>.Rejected(errors);

        try
        {
            var response = await _handler(request, cancellationToken).ConfigureAwait(false);
            return TransactionScriptResult<TResponse>.Completed(response);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TransactionScriptResult<TResponse>.Failed(ex);
        }
    }

    public sealed class Builder
    {
        private readonly string _name;
        private Func<TRequest, IEnumerable<TransactionScriptError>> _validator = static _ => Array.Empty<TransactionScriptError>();
        private Func<TRequest, CancellationToken, ValueTask<TResponse>>? _handler;

        internal Builder(string name)
        {
            _name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Transaction script name is required.", nameof(name))
                : name;
        }

        public Builder Validate(Func<TRequest, IEnumerable<TransactionScriptError>> validator)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            return this;
        }

        public Builder Execute(Func<TRequest, CancellationToken, ValueTask<TResponse>> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        public TransactionScript<TRequest, TResponse> Build()
            => new(_name, _validator, _handler ?? throw new InvalidOperationException("Transaction script handler is required."));
    }
}

/// <summary>Result returned by a Transaction Script execution.</summary>
public sealed class TransactionScriptResult<TResponse>
{
    private TransactionScriptResult(
        TResponse? response,
        TransactionScriptStatus status,
        IReadOnlyList<TransactionScriptError> errors,
        Exception? exception)
    {
        Response = response;
        Status = status;
        Errors = errors;
        Exception = exception;
    }

    public TResponse? Response { get; }

    public TransactionScriptStatus Status { get; }

    public IReadOnlyList<TransactionScriptError> Errors { get; }

    public Exception? Exception { get; }

    public bool Succeeded => Status == TransactionScriptStatus.Completed;

    public static TransactionScriptResult<TResponse> Completed(TResponse response)
        => new(response, TransactionScriptStatus.Completed, Array.Empty<TransactionScriptError>(), null);

    public static TransactionScriptResult<TResponse> Rejected(IReadOnlyList<TransactionScriptError> errors)
    {
        if (errors is null)
            throw new ArgumentNullException(nameof(errors));
        if (errors.Count == 0)
            throw new ArgumentException("Transaction script rejection requires at least one error.", nameof(errors));

        return new(default, TransactionScriptStatus.Rejected, errors, null);
    }

    public static TransactionScriptResult<TResponse> Failed(Exception exception)
        => new(default, TransactionScriptStatus.Failed, Array.Empty<TransactionScriptError>(), exception ?? throw new ArgumentNullException(nameof(exception)));
}

/// <summary>Execution status for a Transaction Script.</summary>
public enum TransactionScriptStatus
{
    Completed,
    Rejected,
    Failed
}

/// <summary>Validation or precondition failure reported before a Transaction Script handler runs.</summary>
public sealed class TransactionScriptError
{
    public TransactionScriptError(string code, string message)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("Transaction script error code is required.", nameof(code))
            : code;
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("Transaction script error message is required.", nameof(message))
            : message;
    }

    public string Code { get; }

    public string Message { get; }
}
