namespace PatternKit.Application.DataMapping;

/// <summary>
/// Maps between an isolated domain model and a persistence or transport data model.
/// </summary>
public interface IDataMapper<TDomain, TData>
{
    /// <summary>Maps a domain model to its data representation.</summary>
    ValueTask<DataMapperResult<TData>> ToDataAsync(TDomain domain, CancellationToken cancellationToken = default);

    /// <summary>Maps a data representation to its domain model.</summary>
    ValueTask<DataMapperResult<TDomain>> ToDomainAsync(TData data, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fluent Data Mapper builder for production composition, examples, and tests.
/// </summary>
public sealed class DataMapper<TDomain, TData> : IDataMapper<TDomain, TData>
{
    private readonly Func<TDomain, TData> _toData;
    private readonly Func<TData, TDomain> _toDomain;
    private readonly IReadOnlyList<Func<TDomain, DataMapperError?>> _domainValidators;
    private readonly IReadOnlyList<Func<TData, DataMapperError?>> _dataValidators;

    private DataMapper(
        Func<TDomain, TData> toData,
        Func<TData, TDomain> toDomain,
        IReadOnlyList<Func<TDomain, DataMapperError?>> domainValidators,
        IReadOnlyList<Func<TData, DataMapperError?>> dataValidators)
    {
        _toData = toData;
        _toDomain = toDomain;
        _domainValidators = domainValidators;
        _dataValidators = dataValidators;
    }

    /// <summary>Creates a Data Mapper builder.</summary>
    public static Builder Create() => new();

    /// <inheritdoc />
    public ValueTask<DataMapperResult<TData>> ToDataAsync(TDomain domain, CancellationToken cancellationToken = default)
    {
        if (domain is null)
            throw new ArgumentNullException(nameof(domain));

        cancellationToken.ThrowIfCancellationRequested();
        var sourceErrors = Validate(_domainValidators, domain);
        if (sourceErrors.Count > 0)
            return new ValueTask<DataMapperResult<TData>>(DataMapperResult<TData>.Failed(sourceErrors));

        var data = _toData(domain);
        var mappedErrors = Validate(_dataValidators, data);
        return new ValueTask<DataMapperResult<TData>>(mappedErrors.Count == 0
            ? DataMapperResult<TData>.Mapped(data)
            : DataMapperResult<TData>.Failed(mappedErrors));
    }

    /// <inheritdoc />
    public ValueTask<DataMapperResult<TDomain>> ToDomainAsync(TData data, CancellationToken cancellationToken = default)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));

        cancellationToken.ThrowIfCancellationRequested();
        var sourceErrors = Validate(_dataValidators, data);
        if (sourceErrors.Count > 0)
            return new ValueTask<DataMapperResult<TDomain>>(DataMapperResult<TDomain>.Failed(sourceErrors));

        var domain = _toDomain(data);
        var mappedErrors = Validate(_domainValidators, domain);
        return new ValueTask<DataMapperResult<TDomain>>(mappedErrors.Count == 0
            ? DataMapperResult<TDomain>.Mapped(domain)
            : DataMapperResult<TDomain>.Failed(mappedErrors));
    }

    private static IReadOnlyList<DataMapperError> Validate<T>(
        IReadOnlyList<Func<T, DataMapperError?>> validators,
        T value)
    {
        if (validators.Count == 0)
            return Array.Empty<DataMapperError>();

        var errors = new List<DataMapperError>();
        foreach (var validator in validators)
        {
            var error = validator(value);
            if (error is not null)
                errors.Add(error);
        }

        return errors;
    }

    /// <summary>Fluent builder for Data Mapper instances.</summary>
    public sealed class Builder
    {
        private readonly List<Func<TDomain, DataMapperError?>> _domainValidators = [];
        private readonly List<Func<TData, DataMapperError?>> _dataValidators = [];
        private Func<TDomain, TData>? _toData;
        private Func<TData, TDomain>? _toDomain;

        /// <summary>Configures the domain-to-data projection.</summary>
        public Builder MapToData(Func<TDomain, TData> mapper)
        {
            _toData = mapper ?? throw new ArgumentNullException(nameof(mapper));
            return this;
        }

        /// <summary>Configures the data-to-domain projection.</summary>
        public Builder MapToDomain(Func<TData, TDomain> mapper)
        {
            _toDomain = mapper ?? throw new ArgumentNullException(nameof(mapper));
            return this;
        }

        /// <summary>Adds validation that runs against domain models.</summary>
        public Builder ValidateDomain(Func<TDomain, DataMapperError?> validator)
        {
            _domainValidators.Add(validator ?? throw new ArgumentNullException(nameof(validator)));
            return this;
        }

        /// <summary>Adds validation that runs against data models.</summary>
        public Builder ValidateData(Func<TData, DataMapperError?> validator)
        {
            _dataValidators.Add(validator ?? throw new ArgumentNullException(nameof(validator)));
            return this;
        }

        /// <summary>Builds the mapper.</summary>
        public DataMapper<TDomain, TData> Build()
            => new(
                _toData ?? throw new InvalidOperationException("A domain-to-data mapper is required."),
                _toDomain ?? throw new InvalidOperationException("A data-to-domain mapper is required."),
                _domainValidators.ToArray(),
                _dataValidators.ToArray());
    }
}

/// <summary>Result returned by a Data Mapper operation.</summary>
public sealed class DataMapperResult<T>
{
    private DataMapperResult(T? value, IReadOnlyList<DataMapperError> errors)
    {
        Value = value;
        Errors = errors;
    }

    /// <summary>The mapped value when the operation succeeds.</summary>
    public T? Value { get; }

    /// <summary>Validation errors that prevented mapping.</summary>
    public IReadOnlyList<DataMapperError> Errors { get; }

    /// <summary>Gets whether the mapping completed without validation errors.</summary>
    public bool Succeeded => Errors.Count == 0;

    /// <summary>Creates a successful mapping result.</summary>
    public static DataMapperResult<T> Mapped(T value)
        => new(value, Array.Empty<DataMapperError>());

    /// <summary>Creates a failed mapping result.</summary>
    public static DataMapperResult<T> Failed(IReadOnlyList<DataMapperError> errors)
    {
        if (errors is null)
            throw new ArgumentNullException(nameof(errors));
        if (errors.Count == 0)
            throw new ArgumentException("At least one Data Mapper error is required.", nameof(errors));

        return new(default, errors);
    }
}

/// <summary>Validation error returned by a Data Mapper.</summary>
public sealed class DataMapperError
{
    public DataMapperError(string code, string message)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("Data Mapper error code is required.", nameof(code))
            : code;
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("Data Mapper error message is required.", nameof(message))
            : message;
    }

    /// <summary>Stable validation code.</summary>
    public string Code { get; }

    /// <summary>User-facing or log-facing validation message.</summary>
    public string Message { get; }
}
