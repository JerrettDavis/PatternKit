using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

public class ObserverGeneratorTests
{
    [Fact]
    public void Generates_Observer_Without_Diagnostics()
    {
        var source = """
            using PatternKit.Generators.Observer;

            namespace TestApp;

            public record OrderEvent(string OrderId, decimal Amount);

            [Observer(typeof(OrderEvent))]
            public partial class OrderEventStream { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Observer_Without_Diagnostics));
        _ = RoslynTestHelpers.Run(comp, new ObserverGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("OrderEventStream.Observer.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generated_Subscribe_And_Publish_Compile()
    {
        var source = """
            using PatternKit.Generators.Observer;
            using System;
            using System.Collections.Generic;

            namespace TestApp;

            public record OrderEvent(string OrderId, decimal Amount);

            [Observer(typeof(OrderEvent))]
            public partial class OrderEventStream { }

            public static class TestRunner
            {
                public static List<string> Run()
                {
                    var log = new List<string>();
                    var stream = new OrderEventStream();

                    var sub1 = stream.Subscribe(e => log.Add($"Sub1: {e.OrderId}"));
                    var sub2 = stream.Subscribe(e => log.Add($"Sub2: {e.OrderId}"));

                    stream.Publish(new OrderEvent("ORD-1", 99.99m));

                    sub1.Dispose();

                    stream.Publish(new OrderEvent("ORD-2", 50.00m));

                    sub2.Dispose();
                    return log;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generated_Subscribe_And_Publish_Compile));
        _ = RoslynTestHelpers.Run(comp, new ObserverGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("OrderEventStream"))
            .SourceText.ToString();

        Assert.Contains("public System.IDisposable Subscribe(", generatedSource);
        Assert.Contains("public void Publish(", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Async_Methods_When_ForceAsync()
    {
        var source = """
            using PatternKit.Generators.Observer;

            namespace TestApp;

            public record OrderEvent(string OrderId);

            [Observer(typeof(OrderEvent), ForceAsync = true)]
            public partial class OrderEventStream { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Async_Methods_When_ForceAsync));
        _ = RoslynTestHelpers.Run(comp, new ObserverGenerator(), out var run, out var updated);

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("OrderEventStream"))
            .SourceText.ToString();

        Assert.Contains("public async System.Threading.Tasks.ValueTask PublishAsync(", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Reports_Error_When_Type_Not_Partial()
    {
        var source = """
            using PatternKit.Generators.Observer;

            namespace TestApp;

            public record OrderEvent(string OrderId);

            [Observer(typeof(OrderEvent))]
            public class OrderEventStream { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Type_Not_Partial));
        _ = RoslynTestHelpers.Run(comp, new ObserverGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKOBS001");
    }

    [Fact]
    public void Generates_With_Aggregate_Exception_Policy()
    {
        var source = """
            using PatternKit.Generators.Observer;

            namespace TestApp;

            public record OrderEvent(string OrderId);

            [Observer(typeof(OrderEvent), Exceptions = ObserverExceptionPolicy.Aggregate)]
            public partial class OrderEventStream { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_With_Aggregate_Exception_Policy));
        _ = RoslynTestHelpers.Run(comp, new ObserverGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("OrderEventStream"))
            .SourceText.ToString();

        Assert.Contains("AggregateException", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_With_Stop_Exception_Policy()
    {
        var source = """
            using PatternKit.Generators.Observer;

            namespace TestApp;

            public record OrderEvent(string OrderId);

            [Observer(typeof(OrderEvent), Exceptions = ObserverExceptionPolicy.Stop)]
            public partial class OrderEventStream { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_With_Stop_Exception_Policy));
        _ = RoslynTestHelpers.Run(comp, new ObserverGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("OrderEventStream"))
            .SourceText.ToString();

        // Stop policy: no try/catch in publish loop
        Assert.DoesNotContain("firstException", generatedSource);
        Assert.DoesNotContain("AggregateException", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_With_SingleThreadedFast_Threading()
    {
        var source = """
            using PatternKit.Generators.Observer;

            namespace TestApp;

            public record OrderEvent(string OrderId);

            [Observer(typeof(OrderEvent), Threading = ObserverThreadingPolicy.SingleThreadedFast)]
            public partial class OrderEventStream { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_With_SingleThreadedFast_Threading));
        _ = RoslynTestHelpers.Run(comp, new ObserverGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("OrderEventStream"))
            .SourceText.ToString();

        // No locking in single-threaded mode
        Assert.DoesNotContain("_subscriberLock", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Snapshot_Semantics()
    {
        var source = """
            using PatternKit.Generators.Observer;

            namespace TestApp;

            public record OrderEvent(string OrderId);

            [Observer(typeof(OrderEvent))]
            public partial class OrderEventStream { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Snapshot_Semantics));
        _ = RoslynTestHelpers.Run(comp, new ObserverGenerator(), out var run, out _);

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("OrderEventStream"))
            .SourceText.ToString();

        // Snapshot: copies subscriber list before iterating
        Assert.Contains("snapshot", generatedSource);
    }

    [Fact]
    public void Generates_IDisposable_Subscription()
    {
        var source = """
            using PatternKit.Generators.Observer;

            namespace TestApp;

            public record OrderEvent(string OrderId);

            [Observer(typeof(OrderEvent))]
            public partial class OrderEventStream { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_IDisposable_Subscription));
        _ = RoslynTestHelpers.Run(comp, new ObserverGenerator(), out var run, out _);

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("OrderEventStream"))
            .SourceText.ToString();

        Assert.Contains("System.IDisposable", generatedSource);
        Assert.Contains("class Subscription", generatedSource);
        Assert.Contains("Dispose()", generatedSource);
    }

    [Fact]
    public void Reports_Warning_When_SingleThreadedFast_With_Async()
    {
        var source = """
            using PatternKit.Generators.Observer;

            namespace TestApp;

            public record OrderEvent(string OrderId);

            [Observer(typeof(OrderEvent), Threading = ObserverThreadingPolicy.SingleThreadedFast, ForceAsync = true)]
            public partial class OrderEventStream { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Warning_When_SingleThreadedFast_With_Async));
        _ = RoslynTestHelpers.Run(comp, new ObserverGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKOBS004");
    }

    [Fact]
    public void Generates_With_Concurrent_Threading()
    {
        var source = """
            using PatternKit.Generators.Observer;

            namespace TestApp;

            public record OrderEvent(string OrderId);

            [Observer(typeof(OrderEvent), Threading = ObserverThreadingPolicy.Concurrent)]
            public partial class OrderEventStream { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_With_Concurrent_Threading));
        _ = RoslynTestHelpers.Run(comp, new ObserverGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("OrderEventStream"))
            .SourceText.ToString();

        // Concurrent uses ConcurrentDictionary
        Assert.Contains("ConcurrentDictionary", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Async_Methods_When_GenerateAsync()
    {
        var source = """
            using PatternKit.Generators.Observer;

            namespace TestApp;

            public record OrderEvent(string OrderId);

            [Observer(typeof(OrderEvent), GenerateAsync = true)]
            public partial class OrderEventStream { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Async_Methods_When_GenerateAsync));
        _ = RoslynTestHelpers.Run(comp, new ObserverGenerator(), out var run, out var updated);

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("OrderEventStream"))
            .SourceText.ToString();

        Assert.Contains("PublishAsync(", generatedSource);
        Assert.Contains("System.Func<", generatedSource);
        Assert.Contains("ValueTask", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_With_Locking_Threading()
    {
        var source = """
            using PatternKit.Generators.Observer;

            namespace TestApp;

            public record OrderEvent(string OrderId);

            [Observer(typeof(OrderEvent), Threading = ObserverThreadingPolicy.Locking)]
            public partial class OrderEventStream { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_With_Locking_Threading));
        _ = RoslynTestHelpers.Run(comp, new ObserverGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("OrderEventStream"))
            .SourceText.ToString();

        // Locking uses _subscriberLock
        Assert.Contains("_subscriberLock", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Concurrent_With_Async()
    {
        var source = """
            using PatternKit.Generators.Observer;

            namespace TestApp;

            public record OrderEvent(string OrderId);

            [Observer(typeof(OrderEvent), Threading = ObserverThreadingPolicy.Concurrent, GenerateAsync = true)]
            public partial class OrderEventStream { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Concurrent_With_Async));
        _ = RoslynTestHelpers.Run(comp, new ObserverGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("OrderEventStream"))
            .SourceText.ToString();

        Assert.Contains("ConcurrentDictionary", generatedSource);
        Assert.Contains("PublishAsync(", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
