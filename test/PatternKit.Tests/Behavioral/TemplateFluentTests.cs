using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using PatternKit.Behavioral.Template;
using Xunit;

namespace PatternKit.Tests.Behavioral
{
    public class TemplateFluentTests
    {
        [Fact]
        public void Execute_RunsHooksAndStep_InOrder()
        {
            var calls = new ConcurrentQueue<string>();
            var tpl = Template<string, int>
                .Create(ctx => { calls.Enqueue($"step:{ctx}"); return ctx.Length; })
                .Before(ctx => calls.Enqueue($"before:{ctx}"))
                .After((ctx, res) => calls.Enqueue($"after:{ctx}:{res}"))
                .Build();

            var result = tpl.Execute("abc");

            Assert.Equal(3, result);
            Assert.Collection(calls,
                s => Assert.Equal("before:abc", s),
                s => Assert.Equal("step:abc", s),
                s => Assert.Equal("after:abc:3", s));
        }

        [Fact]
        public void TryExecute_CatchesErrors_AndInvokesOnError()
        {
            string? observed = null;
            var tpl = Template<int, int>
                .Create(_ => throw new InvalidOperationException("boom"))
                .OnError((ctx, err) => observed = $"ctx={ctx};err={err}")
                .Build();

            var ok = tpl.TryExecute(42, out var result, out var error);

            Assert.False(ok);
            Assert.Equal(default, result);
            Assert.NotNull(error);
            Assert.Equal("ctx=42;err=boom", observed);
        }

        [Fact]
        public async Task Hooks_Compose_Multicast()
        {
            int beforeCount = 0;
            int afterCount = 0;
            var tpl = Template<string, string>
                .Create(ctx => ctx.ToUpperInvariant())
                .Before(_ => Interlocked.Increment(ref beforeCount))
                .Before(_ => Interlocked.Increment(ref beforeCount))
                .After((_, _) => Interlocked.Increment(ref afterCount))
                .After((_, _) => Interlocked.Increment(ref afterCount))
                .Build();

            var t1 = Task.Run(() => tpl.Execute("x"));
            var t2 = Task.Run(() => tpl.Execute("y"));
            await Task.WhenAll(t1, t2);

            Assert.Equal(4, beforeCount);
            Assert.Equal(4, afterCount);
        }

        [Fact]
        public async Task Synchronized_EnforcesMutualExclusion()
        {
            int concurrent = 0;
            int maxConcurrent = 0;

            var tpl = Template<int, int>
                .Create(ctx =>
                {
                    var c = Interlocked.Increment(ref concurrent);
                    maxConcurrent = Math.Max(maxConcurrent, c);
                    Thread.Sleep(20);
                    Interlocked.Decrement(ref concurrent);
                    return ctx * 2;
                })
                .Synchronized()
                .Build();

            var tasks = new Task<int>[8];
            for (int i = 0; i < tasks.Length; i++) tasks[i] = Task.Run(() => tpl.Execute(2));
            var results = await Task.WhenAll(tasks);

            Assert.All(results, r => Assert.Equal(4, r));
            Assert.Equal(1, maxConcurrent);
        }
    }
}

