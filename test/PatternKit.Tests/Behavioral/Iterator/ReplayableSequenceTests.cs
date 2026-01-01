using System.Collections;
using PatternKit.Behavioral.Iterator;
using TinyBDD;
using TinyBDD.Assertions;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Iterator;

[Feature("ReplayableSequence<T> (forkable, lookahead iterator)")]
public sealed class ReplayableSequenceTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed class CountingEnumerable(int start, int count) : IEnumerable<int>
    {
        public int MoveNextCalls { get; private set; }

        public IEnumerator<int> GetEnumerator()
        {
            for (var i = 0; i < count; i++)
            {
                MoveNextCalls++;
                yield return start + i;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static (ReplayableSequence<int> Seq, ReplayableSequence<int>.Cursor C) BuildSimple()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 5));
        return (seq, seq.GetCursor());
    }

    [Scenario("TryNext advances via returned cursor and yields all elements in order")]
    [Fact]
    public Task TryNextYieldsInOrder()
        => Given("sequence 1..3", () => ReplayableSequence<int>.From(Enumerable.Range(1, 3)).GetCursor())
            .When("reading sequentially", c =>
            {
                var values = new List<int>();
                while (c.TryNext(out var v, out var next))
                {
                    values.Add(v);
                    c = next;
                }

                return (values, final: c);
            })
            .Then("values are 1|2|3", t => Expect.For(string.Join('|', t.values)).ToBe("1|2|3"))
            .AssertPassed();

    [Scenario("Peek does not advance position")]
    [Fact]
    public Task PeekDoesNotAdvance()
        => Given("cursor at start of 1..5", BuildSimple)
            .When("peeking twice", t =>
            {
                t.C.Peek(out var a);
                t.C.Peek(out var b);
                return (t.Seq, t.C, a, b);
            })
            .Then("both peeks see 1", t => Expect.For((t.a, t.b)).ToEqual((1, 1)))
            .And("subsequent TryNext still returns 1", t =>
            {
                var c = t.C;
                c.TryNext(out var first, out c);
                return first == 1;
            })
            .AssertPassed();

    [Scenario("Lookahead does not advance and is stable")]
    [Fact]
    public Task LookaheadStable()
        => Given("cursor at start of 1..5", BuildSimple)
            .When("looking ahead 0 & 2 multiple times", t =>
            {
                var a1 = t.C.Lookahead(0).OrDefault();
                var a2 = t.C.Lookahead(0).OrDefault();
                var c1 = t.C.Lookahead(2).OrDefault();
                var c2 = t.C.Lookahead(2).OrDefault();
                return (t.Seq, t.C, a1, a2, c1, c2);
            })
            .Then("values are consistent (1,1,3,3)", t =>
            {
                Expect.For(t.a1).ToBe(1);
                Expect.For(t.a2).ToBe(1);
                Expect.For(t.c1).ToBe(3);
                Expect.For(t.c2).ToBe(3);
            })
            .And("cursor still at position 0", t => t.C.Position == 0)
            .AssertPassed();

    [Scenario("Forking creates independent cursor")]
    [Fact]
    public Task ForkIndependence()
        => Given("cursor advanced two items", () =>
            {
                var seq = ReplayableSequence<int>.From(Enumerable.Range(10, 5));
                var c = seq.GetCursor();
                c.TryNext(out _, out c); // 10
                c.TryNext(out _, out c); // 11
                return (seq, c);
            })
            .When("forking and advancing fork only", t =>
            {
                var fork = t.c.Fork();
                fork.TryNext(out var v, out fork); // should read 12
                return (t.seq, t.c, fork, read: v);
            })
            .Then("fork read 12", t => t.read == 12)
            .And("original cursor position unchanged (still 2)", t => t.c.Position == 2)
            .AssertPassed();

    [Scenario("Lazy buffering only pulls what is needed")]
    [Fact]
    public Task LazyBuffering()
        => Given("counting enumerable 100..104", () =>
            {
                var src = new CountingEnumerable(100, 5);
                var seq = ReplayableSequence<int>.From(src);
                return (src, seq, c: seq.GetCursor());
            })
            .When("peeking then lookahead(2)", t =>
            {
                t.c.Peek(out _);  // needs first element
                t.c.Lookahead(2); // needs up to index 2 (third element)
                return t;
            })
            .Then("MoveNext calls == 3", t => t.src.MoveNextCalls == 3)
            .And("buffer did not pre-fetch beyond requested", t =>
            {
                var before = t.src.MoveNextCalls;
                t.c.Lookahead(3);
                return t.src.MoveNextCalls == before + 1;
            })
            .AssertPassed();

    [Scenario("Batch groups elements correctly including final partial batch")]
    [Fact]
    public Task BatchGroups()
        => Given("cursor over 1..10", () => ReplayableSequence<int>.From(Enumerable.Range(1, 10)).GetCursor())
            .When("batch size 4 enumerated", c => c.Batch(4).Select(b => string.Join('-', b)).ToList())
            .Then("batches are 1-2-3-4 | 5-6-7-8 | 9-10", batches =>
                Expect.For(string.Join(" | ", batches)).ToBe("1-2-3-4 | 5-6-7-8 | 9-10"))
            .AssertPassed();

    [Scenario("LINQ Where/Select over cursor does not advance original cursor")]
    [Fact]
    public Task LinqDoesNotAdvanceOriginal()
        => Given("cursor at start 1..6", () => ReplayableSequence<int>.From(Enumerable.Range(1, 6)).GetCursor())
            .When("enumerating transformed projection", c =>
            {
                var projected = c.Where(x => x % 2 == 0).Select(x => x * 10).Take(2).ToList();
                return (c, projected);
            })
            .Then("projected is 20|40", t => Expect.For(string.Join('|', t.projected)).ToBe("20|40"))
            .And("original cursor still at pos 0", t => t.c.Position == 0)
            .AssertPassed();

    [Scenario("LINQ Select over cursor does advance original cursor")]
    [Fact]
    public Task LinqSelectDoesNotAdvanceOriginal() 
        => Given("cursor at start 1..6", () => ReplayableSequence<int>.From(Enumerable.Range(1, 6)).GetCursor())
            .When("enumerating transformed projection", c =>
            {
                var projected = c.Select(x => x * 10).Take(2).ToList();
                return (c, projected);
            })
            .Then("projected is 10|20", t => Expect.For(string.Join('|', t.projected)).ToBe("10|20"))
            .And("original cursor still at pos 0", t => t.c.Position == 0)
            .AssertPassed();


    [Scenario("LINQ Select over sequence does not advance cursor")]
    [Fact]
    public Task LinqSelectOverSequenceDoesNotAdvanceCursor()
        => Given("cursor at start 1..6", () => ReplayableSequence<int>.From(Enumerable.Range(1, 6)))
            .When("enumerating transformed projection", c =>
            {
                var projected = c.AsEnumerable().Select(x => x * 10).Take(2).ToList();
                return (c, projected);
            })
            .Then("projected is 10|20", t => Expect.For(string.Join('|', t.projected)).ToBe("10|20"))
            .And("original cursor still at pos 0", t => t.c.GetCursor().Position == 0)
            .AssertPassed();

}

#region Additional ReplayableSequence Tests

public sealed class ReplayableSequenceBuilderTests
{
    [Fact]
    public void Lookahead_NegativeOffset_Throws()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 5));
        var cursor = seq.GetCursor();

        Assert.Throws<ArgumentOutOfRangeException>(() => cursor.Lookahead(-1));
    }

    [Fact]
    public void Lookahead_BeyondSequence_ReturnsNone()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 3));
        var cursor = seq.GetCursor();

        var option = cursor.Lookahead(10); // way beyond end

        Assert.False(option.HasValue);
    }

    [Fact]
    public void Peek_EmptySequence_ReturnsFalse()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Empty<int>());
        var cursor = seq.GetCursor();

        var found = cursor.Peek(out var value);

        Assert.False(found);
        Assert.Equal(default, value);
    }

    [Fact]
    public void TryNext_EmptySequence_ReturnsFalse()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Empty<int>());
        var cursor = seq.GetCursor();

        var found = cursor.TryNext(out var value, out var next);

        Assert.False(found);
        Assert.Equal(default, value);
        Assert.Equal(0, next.Position);
    }

    [Fact]
    public void AsEnumerable_FromNonZeroPosition()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 5));
        var cursor = seq.GetCursor();
        cursor.TryNext(out _, out cursor); // advance to position 1
        cursor.TryNext(out _, out cursor); // advance to position 2

        var remaining = cursor.AsEnumerable().ToList();

        Assert.Equal(new[] { 3, 4, 5 }, remaining);
    }

    [Fact]
    public void Fork_PreservesPosition()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 5));
        var cursor = seq.GetCursor();
        cursor.TryNext(out _, out cursor);
        cursor.TryNext(out _, out cursor);

        var fork = cursor.Fork();

        Assert.Equal(cursor.Position, fork.Position);
        Assert.Equal(2, fork.Position);
    }

    [Fact]
    public void Batch_ZeroSize_Throws()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 5));
        var cursor = seq.GetCursor();

        Assert.Throws<ArgumentOutOfRangeException>(() => cursor.Batch(0).ToList());
    }

    [Fact]
    public void Batch_NegativeSize_Throws()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 5));
        var cursor = seq.GetCursor();

        Assert.Throws<ArgumentOutOfRangeException>(() => cursor.Batch(-1).ToList());
    }

    [Fact]
    public void Batch_EmptySequence_YieldsNothing()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Empty<int>());
        var cursor = seq.GetCursor();

        var batches = cursor.Batch(3).ToList();

        Assert.Empty(batches);
    }

    [Fact]
    public void Batch_ExactMultiple_NoPartial()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 6));
        var cursor = seq.GetCursor();

        var batches = cursor.Batch(3).ToList();

        Assert.Equal(2, batches.Count);
        Assert.Equal(new[] { 1, 2, 3 }, batches[0]);
        Assert.Equal(new[] { 4, 5, 6 }, batches[1]);
    }

    [Fact]
    public void ConcurrentCursors_ShareBuffer()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 100));

        var results = new List<int>[10];
        var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(() =>
        {
            var list = new List<int>();
            var cursor = seq.GetCursor();
            while (cursor.TryNext(out var v, out cursor))
                list.Add(v);
            results[i] = list;
        })).ToArray();

        Task.WaitAll(tasks);

        foreach (var result in results)
        {
            Assert.Equal(100, result.Count);
            Assert.Equal(Enumerable.Range(1, 100), result);
        }
    }

    [Fact]
    public void MultipleForks_IndependentPositions()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 10));
        var c1 = seq.GetCursor();

        // Advance c1 by 2
        c1.TryNext(out _, out c1);
        c1.TryNext(out _, out c1);

        var c2 = c1.Fork();
        var c3 = c1.Fork();

        // Advance c2 by 3 more
        c2.TryNext(out _, out c2);
        c2.TryNext(out _, out c2);
        c2.TryNext(out _, out c2);

        Assert.Equal(2, c1.Position);
        Assert.Equal(5, c2.Position);
        Assert.Equal(2, c3.Position);
    }

#if !NETSTANDARD2_0
    [Fact]
    public async Task AsAsyncEnumerable_YieldsAllElements()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 5));
        var list = new List<int>();

        await foreach (var v in seq.AsAsyncEnumerable())
            list.Add(v);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, list);
    }

    [Fact]
    public async Task AsAsyncEnumerable_RespectsToken()
    {
        var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 100));
        using var cts = new CancellationTokenSource();
        var list = new List<int>();

        var ex = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var v in seq.AsAsyncEnumerable(cts.Token))
            {
                list.Add(v);
                if (list.Count == 5) cts.Cancel();
            }
        });

        Assert.Equal(5, list.Count);
    }
#endif
}

#endregion