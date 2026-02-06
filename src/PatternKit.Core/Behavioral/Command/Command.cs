using PatternKit.Creational.Builder;

namespace PatternKit.Behavioral.Command;

/// <summary>
/// <para><b>Command Pattern (allocation-light)</b></para>
/// Encapsulates a unit of work (an action) plus an optional inverse (undo) so it can be
/// executed, queued, composed, retried, or reversed in a uniform way. This implementation
/// focuses on very low allocation overhead while still supporting synchronous and asynchronous
/// (<see cref="ValueTask"/>) execution and macro (composite) commands.
/// </summary>
/// <remarks>
/// <para><b>When should I use this?</b></para>
/// <list type="bullet">
/// <item><description>You need to encapsulate business operations so callers do not depend on concrete implementation details.</description></item>
/// <item><description>You want an optional <i>undo</i> step (optimistic UI, reversible batch / maintenance tasks, editor actions).</description></item>
/// <item><description>You want to compose several smaller operations into an ordered macro (with automatic reverse-order undo).</description></item>
/// <item><description>You plan to queue, schedule, audit, retry, or log operations uniformly.</description></item>
/// <item><description>You need both sync and async handlers without separate abstractions.</description></item>
/// </list>
/// <para><b>When NOT to use:</b> If you simply need to call a method once with no intent to compose or undo, introducing a command adds unnecessary indirection.</para>
/// <para><b>Thread safety:</b> A <see cref="Command{TCtx}"/> instance is immutable after <see cref="Builder.Build"/> and therefore reusable across threads. Thread safety of the underlying action depends on the provided delegates and the <typeparamref name="TCtx"/> contents.</para>
/// <para><b>Performance notes:</b> Delegates are captured once; execution avoids allocations for the fast path when underlying work completes synchronously. Undo is optional; if you do not configure it, <see cref="HasUndo"/> is <c>false</c> and <see cref="TryUndo(in TCtx, out ValueTask)"/> returns <c>false</c> without allocations.</para>
/// <para><b>Value semantics of <c>in TCtx</c>:</b> Passing the context by readonly reference avoids copying large structs. For mutable operations prefer reference types or ensure struct methods mutate internal state safely.</para>
/// <para><b>Related patterns:</b> See also Composite (macro commands), Memento (for more complex state restoration), and Strategy (for pluggable behavior without undo).</para>
/// <para><b>Failure handling:</b> Exceptions thrown inside <c>Do</c> or <c>Undo</c> propagate to the caller. Macro commands stop on first failure; previously executed sub-commands are not automatically undone unless you explicitly call the macro's undo afterward. If you need transactional semantics, wrap macro execution in a try/catch and invoke undo only when appropriate.</para>
///
/// <para><b>Examples</b></para>
/// <example>
/// <para><b>1. Basic synchronous command with undo</b></para>
/// <code language="csharp"><![CDATA[
/// public sealed class Counter { public int Value; }
/// var counter = new Counter();
/// var increment = Command<Counter>.Create()
///     .Do(c => c.Value++)
///     .Undo(c => c.Value--)
///     .Build();
/// await increment.Execute(in counter);           // Value: 1
/// if (increment.TryUndo(in counter, out var undoTask))
///     await undoTask;                            // Value: 0
/// ]]></code>
/// </example>
/// <example>
/// <para><b>2. Asynchronous command (I/O) with cancellation</b></para>
/// <code language="csharp"><![CDATA[
/// var save = Command<string>.Create()
///     .Do(async (in string path, CancellationToken ct) => {
///         using var fs = File.Create(path);
///         await fs.WriteAsync(new byte[]{1,2,3}, ct);
///     })
///     .Build();
/// await save.Execute(in filePath, cancellationToken);
/// ]]></code>
/// </example>
/// <example>
/// <para><b>3. Macro (composite) command with conditional stage and reverse-order undo</b></para>
/// <code language="csharp"><![CDATA[
/// record BuildCtx(List<string> Log);
/// var compile = Command<BuildCtx>.Create().Do(c => c.Log.Add("compile")).Undo(c => c.Log.Add("undo-compile")).Build();
/// var test    = Command<BuildCtx>.Create().Do(c => c.Log.Add("test")).Undo(c => c.Log.Add("undo-test")).Build();
/// var pack    = Command<BuildCtx>.Create().Do(c => c.Log.Add("pack")).Build(); // no undo
/// bool runTests = true;
/// var pipeline = Command<BuildCtx>.Macro()
///     .Add(compile)
///     .AddIf(runTests, test)
///     .Add(pack)
///     .Build();
/// var ctx = new BuildCtx(new List<string>());
/// await pipeline.Execute(in ctx);   // Log: compile, test, pack
/// if (pipeline.TryUndo(in ctx, out var undoVt))
///     await undoVt;                 // Log adds: undo-test, undo-compile (reverse, skipping pack)
/// ]]></code>
/// </example>
/// <example>
/// <para><b>4. Optimistic UI action with optional undo</b></para>
/// <code language="csharp"><![CDATA[
/// // Immediately show item in UI, but support undo if server rejects later.
/// var addItem = Command<List<string>>.Create()
///     .Do(list => list.Add("draft"))
///     .Undo(list => list.Remove("draft"))
///     .Build();
/// ]]></code>
/// </example>
/// </remarks>
/// <typeparam name="TCtx">Context type the command operates on.</typeparam>
public sealed class Command<TCtx>
{
    /// <summary>
    /// Asynchronous (or synchronous) execution delegate signature.
    /// Return <c>default</c> / completed <see cref="ValueTask"/> for synchronous completion.
    /// </summary>
    /// <param name="ctx">Context (readonly reference).</param>
    /// <param name="ct">Cancellation token.</param>
    public delegate ValueTask Exec(in TCtx ctx, CancellationToken ct);

    private readonly Exec _do;
    private readonly Exec? _undo;

    private Command(Exec @do, Exec? undo) => (_do, _undo) = (@do, undo);

    /// <summary>
    /// Execute the command logic. Throws if the underlying delegate throws.
    /// </summary>
    /// <param name="ctx">Context value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="ValueTask"/> enabling allocation-free sync completion.</returns>
    public ValueTask Execute(in TCtx ctx, CancellationToken ct) => _do(in ctx, ct);

    /// <summary>Execute with <see cref="CancellationToken.None"/>.</summary>
    public ValueTask Execute(in TCtx ctx) => _do(in ctx, CancellationToken.None);

    /// <summary>
    /// Attempt to undo. Returns <c>false</c> if no undo handler was configured.
    /// The returned <see cref="ValueTask"/> must be awaited if <c>true</c>.
    /// </summary>
    /// <param name="ctx">Context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="undoTask">Undo value task (valid only when result is true).</param>
    /// <returns><c>true</c> if an undo handler exists; otherwise false.</returns>
    public bool TryUndo(in TCtx ctx, CancellationToken ct, out ValueTask undoTask)
    {
        if (_undo is null)
        {
            undoTask = default;
            return false;
        }
        undoTask = _undo(in ctx, ct);
        return true;
    }

    /// <summary>Convenience overload using <see cref="CancellationToken.None"/>.</summary>
    public bool TryUndo(in TCtx ctx, out ValueTask undoTask) => TryUndo(in ctx, default, out undoTask);

    /// <summary>Indicates whether this command has an undo handler.</summary>
    public bool HasUndo => _undo is not null;

    // ---- Builder ----
    /// <summary>
    /// Start building a new command. Provide a <c>Do</c> delegate (required) and optionally an <c>Undo</c> delegate.
    /// </summary>
    public static Builder Create() => new();

    /// <summary>
    /// Fluent builder for <see cref="Command{TCtx}"/>. Not thread-safe; configure on one thread then call <see cref="Build"/> once.
    /// </summary>
    public sealed class Builder
    {
        private Exec? _do;
        private Exec? _undo;

        /// <summary>Set the required asynchronous (or synchronous) Do handler.</summary>
        public Builder Do(Exec @do)
        {
            _do = @do;
            return this;
        }

        /// <summary>Set a synchronous Do handler (adapter wrapped in a <see cref="ValueTask"/>).</summary>
        public Builder Do(Action<TCtx> @do)
        {
            _do = (in c, _) => { @do(c); return default; };
            return this;
        }

        /// <summary>Set the optional asynchronous Undo handler.</summary>
        public Builder Undo(Exec undo)
        {
            _undo = undo;
            return this;
        }

        /// <summary>Set a synchronous Undo handler (adapter).</summary>
        public Builder Undo(Action<TCtx> undo)
        {
            _undo = (in c, _) => { undo(c); return default; };
            return this;
        }

        /// <summary>Build an immutable command instance. Throws if no Do handler was supplied.</summary>
        public Command<TCtx> Build()
            => new(_do ?? throw new InvalidOperationException("Command requires Do handler."), _undo);
    }

    // ---- Macro composition ----
    /// <summary>
    /// Begin a macro (composite) command definition. Sub-commands execute in registration order; undo runs in reverse order and skips those without undo handlers.
    /// </summary>
    public static MacroBuilder Macro() => new();

    /// <summary>
    /// Builder for a macro command that runs sub-commands in order and undoes in reverse. Supports conditional inclusion via <see cref="AddIf"/>.
    /// </summary>
    public sealed class MacroBuilder
    {
        private readonly ChainBuilder<Command<TCtx>> _chain = ChainBuilder<Command<TCtx>>.Create();

        /// <summary>Add a sub-command to the macro.</summary>
        public MacroBuilder Add(Command<TCtx> cmd)
        {
            _chain.Add(cmd);
            return this;
        }

        /// <summary>Add a sub-command only when <paramref name="condition"/> is true.</summary>
        public MacroBuilder AddIf(bool condition, Command<TCtx> cmd)
        {
            _chain.AddIf(condition, cmd);
            return this;
        }

        /// <summary>Finalize and build a macro command.</summary>
        public Command<TCtx> Build()
        {
            var items = _chain.Build(static cmds => cmds);

            // do: run in order without marking a method that has 'in' params as async
            ValueTask Do(in TCtx ctx, CancellationToken ct)
            {
                // Fast path: all complete synchronously
                for (var i = 0; i < items.Length; i++)
                {
                    var vt = items[i].Execute(in ctx, ct);
                    if (vt.IsCompletedSuccessfully)
                        continue;

                    // Not completed successfully: enter slow path (await then continue); copy ctx to avoid capturing 'in' in async state machine
                    var copy = ctx;
                    return AwaitNext(i, vt, copy, ct, items);
                }

                return default;

                static async ValueTask AwaitNext(int index, ValueTask pending, TCtx localCtx, CancellationToken ct2, Command<TCtx>[] itemsArr)
                {
                    await pending.ConfigureAwait(false);
                    for (var j = index + 1; j < itemsArr.Length; j++)
                    {
                        var vt = itemsArr[j].Execute(in localCtx, ct2);
                        if (!vt.IsCompletedSuccessfully)
                        {
                            await vt.ConfigureAwait(false);
                        }
                    }
                }
            }

            // undo: run in reverse; skip items without undo
            ValueTask Undo(in TCtx ctx, CancellationToken ct)
            {
                for (var i = items.Length - 1; i >= 0; i--)
                {
                    if (!items[i].TryUndo(in ctx, ct, out var vt) || vt.IsCompletedSuccessfully)
                        continue;

                    var copy = ctx;
                    return AwaitUndo(i, vt, copy, ct, items);
                }

                return default;

                static async ValueTask AwaitUndo(int startIndex, ValueTask pending, TCtx localCtx, CancellationToken ct2, Command<TCtx>[] itemsArr)
                {
                    await pending.ConfigureAwait(false);
                    for (var j = startIndex - 1; j >= 0; j--)
                    {
                        if (!itemsArr[j].TryUndo(in localCtx, ct2, out var t) ||
                            t.IsCompletedSuccessfully)
                            continue;

                        await t.ConfigureAwait(false);
                    }
                }
            }

            return new Command<TCtx>(Do, Undo);
        }
    }
}