using PatternKit.Behavioral.Template;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - AsyncTemplateMethod<TContext,TResult> (async skeleton with hooks)")]
public sealed class AsyncTemplateMethodTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed class SampleAsyncTemplate : AsyncTemplateMethod<int, string>
    {
        public int BeforeCount;
        public int AfterCount;
        public int Concurrent;
        public int MaxConcurrent;
        private readonly int _delayMs;
        private readonly bool _sync;

        public SampleAsyncTemplate(int delayMs = 10, bool sync = false)
        {
            _delayMs = delayMs;
            _sync = sync;
        }

        protected override bool Synchronized => _sync;

        protected override async ValueTask OnBeforeAsync(int context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref BeforeCount);
            var c = Interlocked.Increment(ref Concurrent);
            Volatile.Write(ref MaxConcurrent, Math.Max(Volatile.Read(ref MaxConcurrent), c));
            await Task.Yield();
        }

        protected override async ValueTask<string> StepAsync(int context, CancellationToken cancellationToken)
        {
            await Task.Delay(_delayMs, cancellationToken);
            return $"R-{context}";
        }

        protected override async ValueTask OnAfterAsync(int context, string result, CancellationToken cancellationToken)
        {
            Interlocked.Decrement(ref Concurrent);
            Interlocked.Increment(ref AfterCount);
            await Task.Yield();
        }
    }

    [Scenario("ExecuteAsync runs hooks and returns result")]
    [Fact]
    public Task ExecuteAsync_RunsHooks_And_Returns()
        => Given("a sample async template", () => new SampleAsyncTemplate())
           .When("ExecuteAsync(7)", t => (tpl: t, res: t.ExecuteAsync(7).GetAwaiter().GetResult()))
           .Then("result is R-7", r => r.res == "R-7")
           .And("before called once", r => r.tpl.BeforeCount == 1)
           .And("after called once", r => r.tpl.AfterCount == 1)
           .AssertPassed();

    [Scenario("Not synchronized allows concurrency")]
    [Fact]
    public Task Allows_Concurrency_When_Not_Synchronized()
        => Given("a non-synchronized template with delay", () => new SampleAsyncTemplate(delayMs: 25, sync: false))
           .When("ExecuteAsync on 1..4 in parallel", t =>
           {
               var tasks = new[] { t.ExecuteAsync(1), t.ExecuteAsync(2), t.ExecuteAsync(3), t.ExecuteAsync(4) };
               var results = Task.WhenAll(tasks).GetAwaiter().GetResult();
               return (tpl: t, results);
           })
           .Then("contains R-1", r => r.results.Contains("R-1"))
           .And("max concurrency > 1", r => r.tpl.MaxConcurrent > 1)
           .AssertPassed();

    [Scenario("Synchronized serializes ExecuteAsync calls")]
    [Fact]
    public Task Serializes_When_Synchronized()
        => Given("a synchronized template with delay", () => new SampleAsyncTemplate(delayMs: 20, sync: true))
           .When("ExecuteAsync on 1..4 in parallel", t =>
           {
               var tasks = new[] { t.ExecuteAsync(1), t.ExecuteAsync(2), t.ExecuteAsync(3), t.ExecuteAsync(4) };
               var results = Task.WhenAll(tasks).GetAwaiter().GetResult();
               return (tpl: t, results);
           })
           .Then("contains R-1", r => r.results.Contains("R-1"))
           .And("max concurrency == 1", r => r.tpl.MaxConcurrent == 1)
           .AssertPassed();

    [Scenario("Cancellation is observed")]
    [Fact]
    public async Task Cancellation_Observed()
    {
        var template = new SampleAsyncTemplate(delayMs: 100);
        using var cts = new CancellationTokenSource(10);
        
        // Assert.ThrowsAnyAsync verifies that OperationCanceledException or derived types are thrown
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await template.ExecuteAsync(1, cts.Token));
    }
}

#region Additional AsyncTemplateMethod Tests

public sealed class AsyncTemplateMethodEdgeCaseTests
{
    private sealed class MinimalAsyncTemplate : AsyncTemplateMethod<int, string>
    {
        protected override ValueTask<string> StepAsync(int context, CancellationToken cancellationToken)
            => new($"Result-{context}");
    }

    private sealed class HooksAsyncTemplate : AsyncTemplateMethod<int, string>
    {
        public List<string> Log { get; } = [];

        protected override async ValueTask OnBeforeAsync(int context, CancellationToken cancellationToken)
        {
            await Task.Yield();
            Log.Add($"before:{context}");
        }

        protected override ValueTask<string> StepAsync(int context, CancellationToken cancellationToken)
            => new($"step:{context}");

        protected override async ValueTask OnAfterAsync(int context, string result, CancellationToken cancellationToken)
        {
            await Task.Yield();
            Log.Add($"after:{result}");
        }
    }

    private sealed class ThrowingStepTemplate : AsyncTemplateMethod<int, string>
    {
        public int BeforeCount;
        public int AfterCount;
        public bool Sync;

        protected override bool Synchronized => Sync;

        protected override ValueTask OnBeforeAsync(int context, CancellationToken cancellationToken)
        {
            BeforeCount++;
            return default;
        }

        protected override ValueTask<string> StepAsync(int context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Step failed");

        protected override ValueTask OnAfterAsync(int context, string result, CancellationToken cancellationToken)
        {
            AfterCount++;
            return default;
        }
    }

    [Fact]
    public async Task MinimalTemplate_NoHooks_Works()
    {
        var template = new MinimalAsyncTemplate();
        var result = await template.ExecuteAsync(42);
        Assert.Equal("Result-42", result);
    }

    [Fact]
    public async Task HooksTemplate_ExecutesInOrder()
    {
        var template = new HooksAsyncTemplate();
        var result = await template.ExecuteAsync(5);

        Assert.Equal("step:5", result);
        Assert.Equal(2, template.Log.Count);
        Assert.Equal("before:5", template.Log[0]);
        Assert.Equal("after:step:5", template.Log[1]);
    }

    [Fact]
    public async Task ThrowingStep_NotSynchronized_DoesNotCallAfter()
    {
        var template = new ThrowingStepTemplate { Sync = false };

        await Assert.ThrowsAsync<InvalidOperationException>(() => template.ExecuteAsync(1));

        Assert.Equal(1, template.BeforeCount);
        Assert.Equal(0, template.AfterCount);
    }

    [Fact]
    public async Task ThrowingStep_Synchronized_MutexIsReleased()
    {
        var template = new ThrowingStepTemplate { Sync = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() => template.ExecuteAsync(1));

        // The mutex should be released, so a second call should not deadlock
        await Assert.ThrowsAsync<InvalidOperationException>(() => template.ExecuteAsync(2));

        Assert.Equal(2, template.BeforeCount);
    }

    [Fact]
    public async Task Synchronized_Cancellation_DuringMutexWait()
    {
        var template = new SynchronizedSlowTemplate();

        // Start first execution that holds mutex
        var first = template.ExecuteAsync(1);

        // Try to acquire with immediate cancellation
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // TaskCanceledException is a subclass of OperationCanceledException
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => template.ExecuteAsync(2, cts.Token));
        Assert.True(ex is OperationCanceledException or TaskCanceledException);

        // First should still complete
        var result = await first;
        Assert.Equal("slow:1", result);
    }

    private sealed class SynchronizedSlowTemplate : AsyncTemplateMethod<int, string>
    {
        protected override bool Synchronized => true;

        protected override async ValueTask<string> StepAsync(int context, CancellationToken cancellationToken)
        {
            await Task.Delay(50, cancellationToken);
            return $"slow:{context}";
        }
    }
}

#endregion