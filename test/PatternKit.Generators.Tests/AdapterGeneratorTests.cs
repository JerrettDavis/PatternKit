using Microsoft.CodeAnalysis;
using PatternKit.Generators.Adapter;

namespace PatternKit.Generators.Tests;

public class AdapterGeneratorTests
{
    [Fact]
    public void GenerateSimpleAdapter()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                System.DateTimeOffset Now { get; }
            }

            public class LegacyClock
            {
                public System.DateTimeOffset GetNow() => System.DateTimeOffset.UtcNow;
            }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
            public static partial class ClockAdapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static System.DateTimeOffset MapNow(LegacyClock adaptee) => adaptee.GetNow();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateSimpleAdapter));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Adapter file is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("TestNamespace.LegacyClockToIClockAdapter.Adapter.g.cs", names);

        // Generated code contains expected shape
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace.LegacyClockToIClockAdapter.Adapter.g.cs")
            .SourceText.ToString();

        Assert.Contains("public sealed partial class LegacyClockToIClockAdapter : TestNamespace.IClock", generatedSource);
        Assert.Contains("private readonly TestNamespace.LegacyClock _adaptee;", generatedSource);
        Assert.Contains("public LegacyClockToIClockAdapter(TestNamespace.LegacyClock adaptee)", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateAdapterWithMethods()
    {
        const string source = """
            using PatternKit.Generators.Adapter;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public interface IClock
            {
                DateTimeOffset Now { get; }
                ValueTask DelayAsync(TimeSpan duration, CancellationToken ct = default);
            }

            public class LegacyClock
            {
                public DateTimeOffset GetNow() => DateTimeOffset.UtcNow;
                public Task SleepAsync(int milliseconds, CancellationToken ct) => Task.Delay(milliseconds, ct);
            }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
            public static partial class ClockAdapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static DateTimeOffset MapNow(LegacyClock adaptee) => adaptee.GetNow();

                [AdapterMap(TargetMember = nameof(IClock.DelayAsync))]
                public static ValueTask MapDelayAsync(LegacyClock adaptee, TimeSpan duration, CancellationToken ct)
                    => new(adaptee.SleepAsync((int)duration.TotalMilliseconds, ct));
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateAdapterWithMethods));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Generated code contains method
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("LegacyClockToIClockAdapter"))
            .SourceText.ToString();

        Assert.Contains("public System.Threading.Tasks.ValueTask DelayAsync", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateAdapterWithCustomName()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                System.DateTimeOffset Now { get; }
            }

            public class LegacyClock
            {
                public System.DateTimeOffset GetNow() => System.DateTimeOffset.UtcNow;
            }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock), AdapterTypeName = "ClockAdapter")]
            public static partial class ClockAdapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static System.DateTimeOffset MapNow(LegacyClock adaptee) => adaptee.GetNow();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateAdapterWithCustomName));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Custom named adapter file is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("TestNamespace.ClockAdapter.Adapter.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void ErrorWhenHostNotStaticPartial()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                System.DateTimeOffset Now { get; }
            }

            public class LegacyClock { }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
            public class NotStaticPartial
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenHostNotStaticPartial));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP001 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKADP001");
    }

    [Fact]
    public void ErrorWhenTargetNotInterface()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public class NotInterface
            {
                public System.DateTimeOffset Now { get; }
            }

            public class LegacyClock { }

            [GenerateAdapter(Target = typeof(NotInterface), Adaptee = typeof(LegacyClock))]
            public static partial class Adapters { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenTargetNotInterface));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP002 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKADP002");
    }

    [Fact]
    public void ErrorWhenMissingMapping()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                System.DateTimeOffset Now { get; }
                void Tick();
            }

            public class LegacyClock { }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static System.DateTimeOffset MapNow(LegacyClock adaptee) => default;
                // Missing mapping for Tick()
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenMissingMapping));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP003 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKADP003");
    }

    [Fact]
    public void ErrorWhenDuplicateMapping()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                System.DateTimeOffset Now { get; }
            }

            public class LegacyClock { }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static System.DateTimeOffset MapNow1(LegacyClock adaptee) => default;

                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static System.DateTimeOffset MapNow2(LegacyClock adaptee) => default;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenDuplicateMapping));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP004 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKADP004");
    }

    [Fact]
    public void ErrorWhenSignatureMismatch()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                System.DateTimeOffset Now { get; }
            }

            public class LegacyClock { }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static string MapNow(LegacyClock adaptee) => "wrong type"; // Should return DateTimeOffset
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenSignatureMismatch));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP005 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKADP005");
    }

    [Fact]
    public void GenerateThrowingStubWhenPolicySet()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                System.DateTimeOffset Now { get; }
                void Tick();
            }

            public class LegacyClock { }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock), MissingMap = AdapterMissingMapPolicy.ThrowingStub)]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static System.DateTimeOffset MapNow(LegacyClock adaptee) => default;
                // No mapping for Tick() - should generate throwing stub
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateThrowingStubWhenPolicySet));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics (ThrowingStub policy allows missing maps)
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Generated code contains throwing stub
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        Assert.Contains("throw new global::System.NotImplementedException", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateAdapterWithAbstractClassTarget()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public abstract class ClockBase
            {
                public abstract System.DateTimeOffset Now { get; }
            }

            public class LegacyClock
            {
                public System.DateTimeOffset GetNow() => System.DateTimeOffset.UtcNow;
            }

            [GenerateAdapter(Target = typeof(ClockBase), Adaptee = typeof(LegacyClock))]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(ClockBase.Now))]
                public static System.DateTimeOffset MapNow(LegacyClock adaptee) => adaptee.GetNow();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateAdapterWithAbstractClassTarget));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Adapter inherits from abstract class
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        Assert.Contains(": TestNamespace.ClockBase", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateMultipleAdaptersFromSameHost()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock { System.DateTimeOffset Now { get; } }
            public interface ITimer { void Start(); }

            public class LegacyClock
            {
                public System.DateTimeOffset GetNow() => System.DateTimeOffset.UtcNow;
            }

            public class LegacyTimer
            {
                public void Begin() { }
            }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
            [GenerateAdapter(Target = typeof(ITimer), Adaptee = typeof(LegacyTimer))]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static System.DateTimeOffset MapNow(LegacyClock adaptee) => adaptee.GetNow();

                [AdapterMap(TargetMember = nameof(ITimer.Start))]
                public static void MapStart(LegacyTimer adaptee) => adaptee.Begin();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateMultipleAdaptersFromSameHost));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Both adapters are generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains(names, n => n.Contains("LegacyClockToIClockAdapter"));
        Assert.Contains(names, n => n.Contains("LegacyTimerToITimerAdapter"));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateAdapterInGlobalNamespace()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            public interface IClock { System.DateTimeOffset Now { get; } }
            public class LegacyClock { public System.DateTimeOffset GetNow() => default; }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static System.DateTimeOffset MapNow(LegacyClock adaptee) => adaptee.GetNow();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateAdapterInGlobalNamespace));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Adapter is generated without namespace
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        Assert.DoesNotContain("namespace", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateNonSealedAdapter()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock { System.DateTimeOffset Now { get; } }
            public class LegacyClock { public System.DateTimeOffset GetNow() => default; }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock), Sealed = false)]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static System.DateTimeOffset MapNow(LegacyClock adaptee) => adaptee.GetNow();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateNonSealedAdapter));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        Assert.DoesNotContain("sealed", generatedSource);
        Assert.Contains("public partial class", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateAdapterWithMethodParameters()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface ICalculator
            {
                int Add(int a, int b);
                int Multiply(int x, int y);
            }

            public class OldCalculator
            {
                public int Sum(int first, int second) => first + second;
                public int Product(int m, int n) => m * n;
            }

            [GenerateAdapter(Target = typeof(ICalculator), Adaptee = typeof(OldCalculator))]
            public static partial class CalculatorAdapters
            {
                [AdapterMap(TargetMember = nameof(ICalculator.Add))]
                public static int MapAdd(OldCalculator adaptee, int a, int b) => adaptee.Sum(a, b);

                [AdapterMap(TargetMember = nameof(ICalculator.Multiply))]
                public static int MapMultiply(OldCalculator adaptee, int x, int y) => adaptee.Product(x, y);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateAdapterWithMethodParameters));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        Assert.Contains("public int Add(int a, int b)", generatedSource);
        Assert.Contains("public int Multiply(int x, int y)", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void ErrorWhenAdapteeIsAbstract()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                System.DateTimeOffset Now { get; }
            }

            public abstract class AbstractClock { }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(AbstractClock))]
            public static partial class Adapters { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenAdapteeIsAbstract));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP007 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKADP007");
    }

    [Fact]
    public void ErrorWhenMappingMethodNotStatic()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                System.DateTimeOffset Now { get; }
            }

            public class LegacyClock { }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public System.DateTimeOffset MapNow(LegacyClock adaptee) => default; // Missing static
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenMappingMethodNotStatic));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP008 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKADP008");
    }

    [Fact]
    public void MultipleAdaptersWithOverlappingMemberNames()
    {
        // Both IClock and ITimer have a Name property - should not cause false duplicate errors
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock { string Name { get; } }
            public interface ITimer { string Name { get; } }

            public class LegacyClock { public string ClockName => "Clock"; }
            public class LegacyTimer { public string TimerName => "Timer"; }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
            [GenerateAdapter(Target = typeof(ITimer), Adaptee = typeof(LegacyTimer))]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Name))]
                public static string MapClockName(LegacyClock adaptee) => adaptee.ClockName;

                [AdapterMap(TargetMember = nameof(ITimer.Name))]
                public static string MapTimerName(LegacyTimer adaptee) => adaptee.TimerName;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(MultipleAdaptersWithOverlappingMemberNames));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics (mappings are distinguished by adaptee type)
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Both adapters generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains(names, n => n.Contains("LegacyClockToIClockAdapter"));
        Assert.Contains(names, n => n.Contains("LegacyTimerToITimerAdapter"));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void InterfaceDiamondDeduplication()
    {
        // IChild inherits from both IBase1 and IBase2 which both have DoWork()
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IBase { void DoWork(); }
            public interface IChild : IBase { void DoExtra(); }

            public class Legacy
            {
                public void Work() { }
                public void Extra() { }
            }

            [GenerateAdapter(Target = typeof(IChild), Adaptee = typeof(Legacy))]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IChild.DoWork))]
                public static void MapDoWork(Legacy adaptee) => adaptee.Work();

                [AdapterMap(TargetMember = nameof(IChild.DoExtra))]
                public static void MapDoExtra(Legacy adaptee) => adaptee.Extra();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(InterfaceDiamondDeduplication));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Should only have one DoWork method in generated code, not duplicates
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        var doWorkCount = generatedSource.Split("public void DoWork()").Length - 1;
        Assert.Equal(1, doWorkCount);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void RefParameterValidation()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IProcessor
            {
                void Process(ref int value);
            }

            public class Legacy { }

            [GenerateAdapter(Target = typeof(IProcessor), Adaptee = typeof(Legacy))]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IProcessor.Process))]
                public static void MapProcess(Legacy adaptee, int value) { } // Missing ref
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(RefParameterValidation));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP005 diagnostic is reported for ref kind mismatch
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKADP005" && d.GetMessage().Contains("ref kind"));
    }
}
