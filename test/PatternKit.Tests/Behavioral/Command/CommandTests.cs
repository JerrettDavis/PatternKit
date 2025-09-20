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