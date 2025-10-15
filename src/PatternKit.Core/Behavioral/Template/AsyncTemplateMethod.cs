namespace PatternKit.Behavioral.Template;

/// <summary>
/// Generic async Template Method base class.
/// Defines the skeleton of an algorithm, allowing subclasses to override async steps without changing the structure.
/// </summary>
/// <typeparam name="TContext">The type of the context passed to the algorithm.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the algorithm.</typeparam>
public abstract class AsyncTemplateMethod<TContext, TResult>
{
    private readonly SemaphoreSlim _mutex = new(1, 1);

    /// <summary>
    /// Set to true to serialize ExecuteAsync calls across threads using an async-compatible mutex.
    /// Default is false to allow concurrent executions when subclass is stateless or thread-safe.
    /// </summary>
    protected virtual bool Synchronized => false;

    /// <summary>
    /// Executes the algorithm using the provided context and cancellation token.
    /// Calls <see cref="OnBeforeAsync"/>, then <see cref="StepAsync"/>, then <see cref="OnAfterAsync"/>.
    /// </summary>
    public async Task<TResult> ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        if (Synchronized)
        {
            await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await OnBeforeAsync(context, cancellationToken).ConfigureAwait(false);
                var result = await StepAsync(context, cancellationToken).ConfigureAwait(false);
                await OnAfterAsync(context, result, cancellationToken).ConfigureAwait(false);
                return result;
            }
            finally
            {
                _mutex.Release();
            }
        }

        await OnBeforeAsync(context, cancellationToken).ConfigureAwait(false);
        var res = await StepAsync(context, cancellationToken).ConfigureAwait(false);
        await OnAfterAsync(context, res, cancellationToken).ConfigureAwait(false);
        return res;
    }

    /// <summary>
    /// Optional async hook before the main step.
    /// </summary>
    protected virtual ValueTask OnBeforeAsync(TContext context, CancellationToken cancellationToken) => default;

    /// <summary>
    /// The main async step of the algorithm. Must be implemented by subclasses.
    /// </summary>
    protected abstract ValueTask<TResult> StepAsync(TContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Optional async hook after the main step.
    /// </summary>
    protected virtual ValueTask OnAfterAsync(TContext context, TResult result, CancellationToken cancellationToken) => default;
}