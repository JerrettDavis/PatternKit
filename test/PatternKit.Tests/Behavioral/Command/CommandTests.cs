using PatternKit.Behavioral.Command;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Command;

[Feature("Behavioral - Command<TCtx> (single and macro with undo)")]
public sealed class CommandTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private readonly record struct Ctx(List<string> Log);

    [Scenario("Single command: Do and Undo run as configured")]
    [Fact]
    public Task Single_Do_Undo()
        => Given("a command that logs Do/Undo", () =>
            {
                var log = new List<string>();
                var cmd = Command<Ctx>.Create()
                    .Do(static (in c, _) =>
                    {
                        c.Log.Add("do");
                        return default;
                    })
                    .Undo(static (in c, _) =>
                    {
                        c.Log.Add("undo");
                        return default;
                    })
                    .Build();
                return (cmd, log);
            })
            .When("execute then undo", async Task<List<string>> (t) =>
            {
                var (cmd, log) = t;
                var ctx = new Ctx(log);
                await cmd.Execute(in ctx);
                if (cmd.TryUndo(in ctx, out var vt)) await vt;
                return log;
            })
            .Then("log is do|undo", log => string.Join('|', log) == "do|undo")
            .AssertPassed();

    [Scenario("Macro: executes in order and undoes in reverse, skipping commands without undo")]
    [Fact]
    public Task Macro_Order_And_Reverse_Undo()
        => Given("three commands: A (undo), B (no undo), C (undo)", () =>
            {
                var log = new List<string>();

                Command<Ctx> A() => Command<Ctx>.Create()
                    .Do(static (in c, _) =>
                    {
                        c.Log.Add("A");
                        return default;
                    })
                    .Undo(static (in c, _) =>
                    {
                        c.Log.Add("A-undo");
                        return default;
                    })
                    .Build();

                Command<Ctx> B() => Command<Ctx>.Create()
                    .Do(static (in c, _) =>
                    {
                        c.Log.Add("B");
                        return default;
                    })
                    .Build();

                Command<Ctx> C() => Command<Ctx>.Create()
                    .Do(static (in c, _) =>
                    {
                        c.Log.Add("C");
                        return default;
                    })
                    .Undo(static (in c, _) =>
                    {
                        c.Log.Add("C-undo");
                        return default;
                    })
                    .Build();

                var macro = Command<Ctx>.Macro().Add(A()).Add(B()).Add(C()).Build();
                return (macro, log);
            })
            .When("execute and undo", async Task<List<string>> (t) =>
            {
                var (m, log) = t;
                var ctx = new Ctx(log);
                await m.Execute(in ctx);
                if (m.TryUndo(in ctx, out var vt)) await vt;
                return log;
            })
            .Then("order then reverse-undo skipping B", log => string.Join('|', log) == "A|B|C|C-undo|A-undo")
            .AssertPassed();
}

#region Additional Command Tests

public sealed class CommandBuilderTests
{
    private readonly record struct Ctx(List<string> Log);

    [Fact]
    public void Command_Build_Without_Do_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Command<Ctx>.Create().Build());
    }

    [Fact]
    public async Task Command_HasUndo_True_When_Configured()
    {
        var cmd = Command<Ctx>.Create()
            .Do(c => { })
            .Undo(c => { })
            .Build();

        Assert.True(cmd.HasUndo);
    }

    [Fact]
    public async Task Command_HasUndo_False_When_Not_Configured()
    {
        var cmd = Command<Ctx>.Create()
            .Do(c => { })
            .Build();

        Assert.False(cmd.HasUndo);
    }

    [Fact]
    public async Task Command_TryUndo_Returns_False_When_No_Undo()
    {
        var cmd = Command<Ctx>.Create()
            .Do(c => { })
            .Build();

        var ctx = new Ctx(new List<string>());
        var result = cmd.TryUndo(in ctx, out _);

        Assert.False(result);
    }

    [Fact]
    public async Task Command_Execute_With_CancellationToken()
    {
        var log = new List<string>();
        var cmd = Command<Ctx>.Create()
            .Do((in c, ct) =>
            {
                c.Log.Add("executed");
                return default;
            })
            .Build();

        var ctx = new Ctx(log);
        await cmd.Execute(in ctx, CancellationToken.None);

        Assert.Single(log);
        Assert.Equal("executed", log[0]);
    }

    [Fact]
    public async Task Command_TryUndo_With_CancellationToken()
    {
        var log = new List<string>();
        var cmd = Command<Ctx>.Create()
            .Do(c => c.Log.Add("do"))
            .Undo((in c, ct) =>
            {
                c.Log.Add("undo");
                return default;
            })
            .Build();

        var ctx = new Ctx(log);
        await cmd.Execute(in ctx);

        if (cmd.TryUndo(in ctx, CancellationToken.None, out var vt))
            await vt;

        Assert.Equal(2, log.Count);
        Assert.Equal("undo", log[1]);
    }

    [Fact]
    public async Task Command_Sync_Do_Handler()
    {
        var executed = false;
        var cmd = Command<int>.Create()
            .Do(x => executed = true)
            .Build();

        var ctx = 5;
        await cmd.Execute(in ctx);

        Assert.True(executed);
    }

    [Fact]
    public async Task Command_Sync_Undo_Handler()
    {
        var undone = false;
        var cmd = Command<int>.Create()
            .Do(x => { })
            .Undo(x => undone = true)
            .Build();

        var ctx = 5;
        await cmd.Execute(in ctx);
        if (cmd.TryUndo(in ctx, out var vt))
            await vt;

        Assert.True(undone);
    }

    [Fact]
    public async Task Command_Async_Do_Handler()
    {
        var log = new List<string>();
        var cmd = Command<Ctx>.Create()
            .Do((in Ctx c, CancellationToken ct) =>
            {
                var logRef = c.Log;
                return DoAsync(logRef, ct);
                static async ValueTask DoAsync(List<string> l, CancellationToken ct2)
                {
                    await Task.Delay(1, ct2);
                    l.Add("async-do");
                }
            })
            .Build();

        var ctx = new Ctx(log);
        await cmd.Execute(in ctx);

        Assert.Single(log);
        Assert.Equal("async-do", log[0]);
    }

    [Fact]
    public async Task Command_Async_Undo_Handler()
    {
        var log = new List<string>();
        var cmd = Command<Ctx>.Create()
            .Do(c => c.Log.Add("do"))
            .Undo((in Ctx c, CancellationToken ct) =>
            {
                var logRef = c.Log;
                return DoAsync(logRef, ct);
                static async ValueTask DoAsync(List<string> l, CancellationToken ct2)
                {
                    await Task.Delay(1, ct2);
                    l.Add("async-undo");
                }
            })
            .Build();

        var ctx = new Ctx(log);
        await cmd.Execute(in ctx);
        if (cmd.TryUndo(in ctx, out var vt))
            await vt;

        Assert.Equal(2, log.Count);
        Assert.Equal("async-undo", log[1]);
    }

    [Fact]
    public async Task Macro_AddIf_True_Includes_Command()
    {
        var log = new List<string>();
        var cmd = Command<Ctx>.Create().Do(c => c.Log.Add("conditional")).Build();

        var macro = Command<Ctx>.Macro()
            .AddIf(true, cmd)
            .Build();

        var ctx = new Ctx(log);
        await macro.Execute(in ctx);

        Assert.Single(log);
        Assert.Equal("conditional", log[0]);
    }

    [Fact]
    public async Task Macro_AddIf_False_Excludes_Command()
    {
        var log = new List<string>();
        var cmd = Command<Ctx>.Create().Do(c => c.Log.Add("conditional")).Build();

        var macro = Command<Ctx>.Macro()
            .AddIf(false, cmd)
            .Build();

        var ctx = new Ctx(log);
        await macro.Execute(in ctx);

        Assert.Empty(log);
    }

    [Fact]
    public async Task Macro_Empty_Does_Nothing()
    {
        var macro = Command<Ctx>.Macro().Build();

        var log = new List<string>();
        var ctx = new Ctx(log);
        await macro.Execute(in ctx);

        Assert.Empty(log);
    }

    [Fact]
    public async Task Macro_HasUndo_True()
    {
        var cmd = Command<Ctx>.Create()
            .Do(c => { })
            .Undo(c => { })
            .Build();

        var macro = Command<Ctx>.Macro()
            .Add(cmd)
            .Build();

        Assert.True(macro.HasUndo);
    }

    [Fact]
    public async Task Macro_Async_Commands()
    {
        var log = new List<string>();
        var cmd1 = Command<Ctx>.Create()
            .Do((in Ctx c, CancellationToken ct) =>
            {
                var logRef = c.Log;
                return DoAsync1(logRef, ct);
                static async ValueTask DoAsync1(List<string> l, CancellationToken ct2)
                {
                    await Task.Delay(5, ct2);
                    l.Add("async-1");
                }
            })
            .Build();

        var cmd2 = Command<Ctx>.Create()
            .Do((in Ctx c, CancellationToken ct) =>
            {
                var logRef = c.Log;
                return DoAsync2(logRef, ct);
                static async ValueTask DoAsync2(List<string> l, CancellationToken ct2)
                {
                    await Task.Delay(5, ct2);
                    l.Add("async-2");
                }
            })
            .Build();

        var macro = Command<Ctx>.Macro()
            .Add(cmd1)
            .Add(cmd2)
            .Build();

        var ctx = new Ctx(log);
        await macro.Execute(in ctx);

        Assert.Equal(2, log.Count);
        Assert.Equal("async-1", log[0]);
        Assert.Equal("async-2", log[1]);
    }

    [Fact]
    public async Task Macro_Mixed_Sync_And_Async()
    {
        var log = new List<string>();
        var syncCmd = Command<Ctx>.Create()
            .Do(c => c.Log.Add("sync"))
            .Build();

        var asyncCmd = Command<Ctx>.Create()
            .Do((in Ctx c, CancellationToken ct) =>
            {
                var logRef = c.Log;
                return DoAsync(logRef, ct);
                static async ValueTask DoAsync(List<string> l, CancellationToken ct2)
                {
                    await Task.Delay(1, ct2);
                    l.Add("async");
                }
            })
            .Build();

        var macro = Command<Ctx>.Macro()
            .Add(syncCmd)
            .Add(asyncCmd)
            .Add(syncCmd)
            .Build();

        var ctx = new Ctx(log);
        await macro.Execute(in ctx);

        Assert.Equal(3, log.Count);
        Assert.Equal("sync", log[0]);
        Assert.Equal("async", log[1]);
        Assert.Equal("sync", log[2]);
    }

    [Fact]
    public async Task Macro_Undo_With_Async()
    {
        var log = new List<string>();
        var cmd = Command<Ctx>.Create()
            .Do(c => c.Log.Add("do"))
            .Undo((in Ctx c, CancellationToken ct) =>
            {
                var logRef = c.Log;
                return UndoAsync(logRef, ct);
                static async ValueTask UndoAsync(List<string> l, CancellationToken ct2)
                {
                    await Task.Delay(1, ct2);
                    l.Add("undo");
                }
            })
            .Build();

        var macro = Command<Ctx>.Macro()
            .Add(cmd)
            .Build();

        var ctx = new Ctx(log);
        await macro.Execute(in ctx);
        if (macro.TryUndo(in ctx, out var vt))
            await vt;

        Assert.Equal(2, log.Count);
        Assert.Equal("undo", log[1]);
    }

    [Fact]
    public async Task Macro_All_Commands_Without_Undo()
    {
        var log = new List<string>();
        var cmd1 = Command<Ctx>.Create().Do(c => c.Log.Add("1")).Build();
        var cmd2 = Command<Ctx>.Create().Do(c => c.Log.Add("2")).Build();

        var macro = Command<Ctx>.Macro()
            .Add(cmd1)
            .Add(cmd2)
            .Build();

        var ctx = new Ctx(log);
        await macro.Execute(in ctx);

        // Macro still HasUndo (it's generated), but sub-commands have no undo
        if (macro.TryUndo(in ctx, out var vt))
            await vt;

        Assert.Equal(2, log.Count); // No undo entries added
    }

    [Fact]
    public async Task Command_Execute_Overload_Without_CancellationToken()
    {
        var executed = false;
        var cmd = Command<int>.Create()
            .Do(x => executed = true)
            .Build();

        var ctx = 5;
        await cmd.Execute(in ctx); // No CancellationToken

        Assert.True(executed);
    }
}

#endregion