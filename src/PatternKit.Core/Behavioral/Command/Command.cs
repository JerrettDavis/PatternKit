using PatternKit.Creational.Builder;

namespace PatternKit.Behavioral.Command;

/// <summary>
/// Allocation-light command that executes an action (and optional undo) over an input context.
/// Supports sync/async via ValueTask and can be composed into a macro with ordered execution and reverse undo.
/// </summary>
public sealed class Command<TCtx>
{
    /// <summary>Asynchronous execution delegate for a command.</summary>
    public delegate ValueTask Exec(in TCtx ctx, CancellationToken ct);

    private readonly Exec _do;
    private readonly Exec? _undo;

    private Command(Exec @do, Exec? undo) => (_do, _undo) = (@do, undo);

    /// <summary>Execute the command against <paramref name="ctx"/>.</summary>
    public ValueTask Execute(in TCtx ctx, CancellationToken ct) => _do(in ctx, ct);

    public ValueTask Execute(in TCtx ctx) => _do(in ctx, CancellationToken.None);

    /// <summary>Attempt to undo the command; returns false when no undo was configured.</summary>
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

    public bool TryUndo(in TCtx ctx, out ValueTask undoTask) => TryUndo(in ctx, default, out undoTask);

    /// <summary>Whether this command has an undo handler.</summary>
    public bool HasUndo => _undo is not null;

    // ---- Builder ----
    public static Builder Create() => new();

    /// <summary>Fluent builder for <see cref="Command{TCtx}"/>.</summary>
    public sealed class Builder
    {
        private Exec? _do;
        private Exec? _undo;

        /// <summary>Set the required Do handler.</summary>
        public Builder Do(Exec @do)
        {
            _do = @do;
            return this;
        }

        /// <summary>Set sync Do handler (adapter).</summary>
        public Builder Do(Action<TCtx> @do)
        {
            _do = (in TCtx c, CancellationToken _) =>
            {
                @do(c);
                return default;
            };
            return this;
        }

        /// <summary>Set optional Undo handler.</summary>
        public Builder Undo(Exec undo)
        {
            _undo = undo;
            return this;
        }

        /// <summary>Set sync Undo handler (adapter).</summary>
        public Builder Undo(Action<TCtx> undo)
        {
            _undo = (in TCtx c, CancellationToken _) =>
            {
                undo(c);
                return default;
            };
            return this;
        }

        /// <summary>Build an immutable command.</summary>
        public Command<TCtx> Build()
            => new(_do ?? throw new InvalidOperationException("Command requires Do handler."), _undo);
    }

    // ---- Macro composition ----
    public static MacroBuilder Macro() => new();

    /// <summary>Builder for a macro command that runs sub-commands in order and undoes in reverse.</summary>
    public sealed class MacroBuilder
    {
        private readonly ChainBuilder<Command<TCtx>> _chain = ChainBuilder<Command<TCtx>>.Create();

        public MacroBuilder Add(Command<TCtx> cmd)
        {
            _chain.Add(cmd);
            return this;
        }

        public MacroBuilder AddIf(bool condition, Command<TCtx> cmd)
        {
            _chain.AddIf(condition, cmd);
            return this;
        }

        /// <summary>Build a macro command.</summary>
        public Command<TCtx> Build()
        {
            var items = _chain.Build(static cmds => cmds);

            // do: run in order without marking a method that has 'in' params as async
            ValueTask Do(in TCtx ctx, CancellationToken ct)
            {
                // Fast path: all complete synchronously
                for (int i = 0; i < items.Length; i++)
                {
                    var vt = items[i].Execute(in ctx, ct);
                    if (!vt.IsCompletedSuccessfully)
                    {
                        // Slow path: await then continue; copy ctx to avoid capturing 'in' in async state machine
                        var copy = ctx;
                        return AwaitNext(i, vt, copy, ct, items);
                    }
                }

                return default;

                static async ValueTask AwaitNext(int index, ValueTask pending, TCtx localCtx, CancellationToken ct2, Command<TCtx>[] itemsArr)
                {
                    await pending.ConfigureAwait(false);
                    for (int j = index + 1; j < itemsArr.Length; j++)
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
                for (int i = items.Length - 1; i >= 0; i--)
                {
                    if (items[i].TryUndo(in ctx, ct, out var vt))
                    {
                        if (!vt.IsCompletedSuccessfully)
                        {
                            var copy = ctx;
                            return AwaitUndo(i, vt, copy, ct, items);
                        }
                    }
                }

                return default;

                static async ValueTask AwaitUndo(int startIndex, ValueTask pending, TCtx localCtx, CancellationToken ct2, Command<TCtx>[] itemsArr)
                {
                    await pending.ConfigureAwait(false);
                    for (int j = startIndex - 1; j >= 0; j--)
                    {
                        if (itemsArr[j].TryUndo(in localCtx, ct2, out var t))
                        {
                            if (!t.IsCompletedSuccessfully)
                                await t.ConfigureAwait(false);
                        }
                    }
                }
            }

            return new Command<TCtx>(Do, Undo);
        }
    }
}