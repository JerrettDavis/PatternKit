using PatternKit.Application.UnitOfWork;
using TinyBDD;

namespace PatternKit.Tests.Application.UnitOfWork;

public sealed class UnitOfWorkTests
{
    [Scenario("CommitAsync ExecutesStepsInOrder")]
    [Fact]
    public async Task CommitAsync_ExecutesStepsInOrder()
    {
        var log = new List<string>();
        var unit = PatternKit.Application.UnitOfWork.UnitOfWork.Create()
            .Enlist("reserve", _ => { log.Add("reserve"); return default; })
            .Enlist("persist", _ => { log.Add("persist"); return default; })
            .Build();

        var result = await unit.CommitAsync();

        ScenarioExpect.True(result.Committed);
        ScenarioExpect.Equal(["reserve", "persist"], log);
        ScenarioExpect.Equal(["reserve", "persist"], result.CommittedSteps);
    }

    [Scenario("CommitAsync RollsBackCommittedStepsWhenLaterStepFails")]
    [Fact]
    public async Task CommitAsync_RollsBackCommittedStepsWhenLaterStepFails()
    {
        var log = new List<string>();
        var unit = PatternKit.Application.UnitOfWork.UnitOfWork.Create()
            .Enlist("reserve", _ => { log.Add("reserve"); return default; }, _ => { log.Add("undo-reserve"); return default; })
            .Enlist("persist", _ => throw new InvalidOperationException("db failed"))
            .Build();

        var result = await unit.CommitAsync();

        ScenarioExpect.False(result.Committed);
        ScenarioExpect.Equal("persist", result.FailedStep);
        ScenarioExpect.Equal(["reserve", "undo-reserve"], log);
        ScenarioExpect.True(result.Rollback!.Succeeded);
        ScenarioExpect.Equal(["reserve"], result.Rollback.RolledBackSteps);
    }

    [Scenario("RollbackAsync RunsCompensationsInReverseOrder")]
    [Fact]
    public async Task RollbackAsync_RunsCompensationsInReverseOrder()
    {
        var log = new List<string>();
        var unit = PatternKit.Application.UnitOfWork.UnitOfWork.Create()
            .Enlist("one", _ => default, _ => { log.Add("undo-one"); return default; })
            .Enlist("two", _ => default, _ => { log.Add("undo-two"); return default; })
            .Build();

        var result = await unit.RollbackAsync();

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal(["undo-two", "undo-one"], log);
        ScenarioExpect.Equal(["two", "one"], result.RolledBackSteps);
    }

    [Scenario("UnitOfWork ValidatesInputsAndCancellation")]
    [Fact]
    public async Task UnitOfWork_ValidatesInputsAndCancellation()
    {
        ScenarioExpect.Throws<ArgumentException>(() => PatternKit.Application.UnitOfWork.UnitOfWork.Create().Enlist("", _ => default));
        ScenarioExpect.Throws<ArgumentNullException>(() => PatternKit.Application.UnitOfWork.UnitOfWork.Create().Enlist("step", null!));
        ScenarioExpect.Throws<ArgumentException>(() => PatternKit.Application.UnitOfWork.UnitOfWork.Create().Enlist("step", _ => default).Enlist("step", _ => default));
        ScenarioExpect.Throws<ArgumentException>(() => new UnitOfWorkStep("", _ => default, _ => default));
        ScenarioExpect.Throws<ArgumentNullException>(() => new UnitOfWorkStep("step", null!, _ => default));
        ScenarioExpect.Throws<ArgumentNullException>(() => new UnitOfWorkStep("step", _ => default, null!));

        using var source = new CancellationTokenSource();
        source.Cancel();
        var unit = PatternKit.Application.UnitOfWork.UnitOfWork.Create().Enlist("step", _ => default).Build();
        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(async () => await unit.CommitAsync(source.Token));
    }
}
