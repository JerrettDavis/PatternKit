using Microsoft.CodeAnalysis;
using PatternKit.Generators.Adapter;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public class AdapterGeneratorTests
{
    [Scenario("GenerateSimpleAdapter")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Adapter file is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("TestNamespace.LegacyClockToIClockAdapter.Adapter.g.cs", names);

        // Generated code contains expected shape
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace.LegacyClockToIClockAdapter.Adapter.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("public sealed partial class LegacyClockToIClockAdapter : global::TestNamespace.IClock", generatedSource);
        ScenarioExpect.Contains("private readonly global::TestNamespace.LegacyClock _adaptee;", generatedSource);
        ScenarioExpect.Contains("public LegacyClockToIClockAdapter(global::TestNamespace.LegacyClock adaptee)", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateAdapterWithMethods")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Generated code contains method
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("LegacyClockToIClockAdapter"))
            .SourceText.ToString();

        ScenarioExpect.Contains("public global::System.Threading.Tasks.ValueTask DelayAsync", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateAdapterWithCustomName")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Custom named adapter file is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("TestNamespace.ClockAdapter.Adapter.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ErrorWhenHostNotStaticPartial")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP001");
    }

    [Scenario("ErrorWhenTargetNotInterfaceOrAbstract")]
    [Fact]
    public void ErrorWhenTargetNotInterfaceOrAbstract()
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

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenTargetNotInterfaceOrAbstract));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP002 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP002");
    }

    [Scenario("ErrorWhenMissingMapping")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP003");
    }

    [Scenario("ErrorWhenDuplicateMapping")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP004");
    }

    [Scenario("ErrorWhenSignatureMismatch")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP005");
    }

    [Scenario("GenerateThrowingStubWhenPolicySet")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Generated code contains throwing stub
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        ScenarioExpect.Contains("throw new global::System.NotImplementedException", generatedSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateAdapterWithAbstractClassTarget")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Adapter inherits from abstract class
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        ScenarioExpect.Contains(": global::TestNamespace.ClockBase", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateMultipleAdaptersFromSameHost")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Both adapters are generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains(names, n => n.Contains("LegacyClockToIClockAdapter"));
        ScenarioExpect.Contains(names, n => n.Contains("LegacyTimerToITimerAdapter"));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateAdapterInGlobalNamespace")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Adapter is generated without namespace
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        ScenarioExpect.DoesNotContain("namespace", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateNonSealedAdapter")]
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

        ScenarioExpect.DoesNotContain("sealed", generatedSource);
        ScenarioExpect.Contains("public partial class", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateAdapterWithMethodParameters")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        ScenarioExpect.Contains("public int Add(int a, int b)", generatedSource);
        ScenarioExpect.Contains("public int Multiply(int x, int y)", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ErrorWhenAdapteeIsAbstract")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP007");
    }

    [Scenario("ErrorWhenMappingMethodNotStatic")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP008");
    }

    [Scenario("MultipleAdaptersWithOverlappingMemberNames")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Both adapters generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains(names, n => n.Contains("LegacyClockToIClockAdapter"));
        ScenarioExpect.Contains(names, n => n.Contains("LegacyTimerToITimerAdapter"));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("InterfaceDiamondDeduplication")]
    [Fact]
    public void InterfaceDiamondDeduplication()
    {
        // True diamond: IBase1 and IBase2 both declare DoWork(), IChild inherits from both
        // This tests that we de-duplicate by signature and don't emit DoWork twice
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IBase1 { void DoWork(); }
            public interface IBase2 { void DoWork(); }
            public interface IChild : IBase1, IBase2 { void DoExtra(); }

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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Should only have one DoWork method in generated code, not duplicates
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        var doWorkCount = generatedSource.Split("public void DoWork()").Length - 1;
        ScenarioExpect.Equal(1, doWorkCount);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("RefParameterValidation")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP005" && d.GetMessage().Contains("ref kind"));
    }

    [Scenario("ErrorWhenTargetHasEvents")]
    [Fact]
    public void ErrorWhenTargetHasEvents()
    {
        const string source = """
            using PatternKit.Generators.Adapter;
            using System;

            namespace TestNamespace;

            public interface INotifier
            {
                event EventHandler Changed;
            }

            public class Legacy { }

            [GenerateAdapter(Target = typeof(INotifier), Adaptee = typeof(Legacy))]
            public static partial class Adapters { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenTargetHasEvents));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP009 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP009");
    }

    [Scenario("ErrorWhenTargetHasGenericMethods")]
    [Fact]
    public void ErrorWhenTargetHasGenericMethods()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface ISerializer
            {
                T Deserialize<T>(string data);
            }

            public class Legacy { }

            [GenerateAdapter(Target = typeof(ISerializer), Adaptee = typeof(Legacy))]
            public static partial class Adapters { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenTargetHasGenericMethods));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP010 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP010");
    }

    [Scenario("ErrorWhenTargetHasOverloadedMethods")]
    [Fact]
    public void ErrorWhenTargetHasOverloadedMethods()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface ICalculator
            {
                int Add(int a, int b);
                int Add(int a, int b, int c);
            }

            public class Legacy { }

            [GenerateAdapter(Target = typeof(ICalculator), Adaptee = typeof(Legacy))]
            public static partial class Adapters { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenTargetHasOverloadedMethods));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP011 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP011");
    }

    [Scenario("ErrorWhenAbstractClassHasNoParameterlessCtor")]
    [Fact]
    public void ErrorWhenAbstractClassHasNoParameterlessCtor()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public abstract class ServiceBase
            {
                protected ServiceBase(string name) { }
                public abstract void DoWork();
            }

            public class Legacy { }

            [GenerateAdapter(Target = typeof(ServiceBase), Adaptee = typeof(Legacy))]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(ServiceBase.DoWork))]
                public static void MapDoWork(Legacy adaptee) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenAbstractClassHasNoParameterlessCtor));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP012 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP012");
    }

    [Scenario("ErrorWhenAdapterTypeNameConflicts")]
    [Fact]
    public void ErrorWhenAdapterTypeNameConflicts()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                System.DateTimeOffset Now { get; }
            }

            public class LegacyClock { }

            // This type already exists with the same name the generator would use
            public class LegacyClockToIClockAdapter { }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static System.DateTimeOffset MapNow(LegacyClock adaptee) => default;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenAdapterTypeNameConflicts));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP006 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP006");
    }

    [Scenario("StructAdapteeNoNullCheck")]
    [Fact]
    public void StructAdapteeNoNullCheck()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                System.DateTimeOffset Now { get; }
            }

            public struct StructClock
            {
                public System.DateTimeOffset GetNow() => System.DateTimeOffset.UtcNow;
            }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(StructClock))]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static System.DateTimeOffset MapNow(StructClock adaptee) => adaptee.GetNow();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(StructAdapteeNoNullCheck));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Generated code should NOT have null check for struct
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Adapter"))
            .SourceText.ToString();

        ScenarioExpect.DoesNotContain("throw new global::System.ArgumentNullException", generatedSource);
        ScenarioExpect.Contains("_adaptee = adaptee;", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ErrorWhenTargetHasSettableProperty")]
    [Fact]
    public void ErrorWhenTargetHasSettableProperty()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface ISettings
            {
                string Name { get; set; }
            }

            public class Legacy { }

            [GenerateAdapter(Target = typeof(ISettings), Adaptee = typeof(Legacy))]
            public static partial class Adapters { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenTargetHasSettableProperty));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP013 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP013");
    }

    [Scenario("ReadOnlyPropertyIsSupported")]
    [Fact]
    public void ReadOnlyPropertyIsSupported()
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
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static System.DateTimeOffset MapNow(LegacyClock adaptee) => adaptee.GetNow();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReadOnlyPropertyIsSupported));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics (read-only properties are fine)
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ErrorWhenHostIsNested")]
    [Fact]
    public void ErrorWhenHostIsNested()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                System.DateTimeOffset Now { get; }
            }

            public class LegacyClock { }

            public class OuterClass
            {
                [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
                public static partial class Adapters { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenHostIsNested));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP014 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP014");
    }

    [Scenario("ErrorWhenHostIsGeneric")]
    [Fact]
    public void ErrorWhenHostIsGeneric()
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
            public static partial class Adapters<T> { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenHostIsGeneric));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP014 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP014");
    }

    [Scenario("ErrorWhenMappingMethodNotAccessible")]
    [Fact]
    public void ErrorWhenMappingMethodNotAccessible()
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
                private static System.DateTimeOffset MapNow(LegacyClock adaptee) => default; // Private instead of public/internal
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenMappingMethodNotAccessible));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP015 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP015");
    }

    [Scenario("ErrorWhenTargetHasStaticMembers")]
    [Fact]
    public void ErrorWhenTargetHasStaticMembers()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                System.DateTimeOffset Now { get; }
                static abstract void StaticMethod(); // Static abstract member (C# 11+)
            }

            public class LegacyClock { }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
            public static partial class Adapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static System.DateTimeOffset MapNow(LegacyClock adaptee) => default;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenTargetHasStaticMembers));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP016 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP016");
    }

    [Scenario("ErrorWhenTargetHasRefReturnProperty")]
    [Fact]
    public void ErrorWhenTargetHasRefReturnProperty()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                ref int RefProperty { get; } // Ref-return property
            }

            public class LegacyClock { }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
            public static partial class Adapters { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenTargetHasRefReturnProperty));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP017 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP017");
    }

    [Scenario("ErrorWhenTargetHasRefReturnMethod")]
    [Fact]
    public void ErrorWhenTargetHasRefReturnMethod()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                ref int GetRefValue(); // Ref-return method
            }

            public class LegacyClock { }

            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
            public static partial class Adapters { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenTargetHasRefReturnMethod));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP017 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP017");
    }

    [Scenario("ErrorWhenTargetHasIndexer")]
    [Fact]
    public void ErrorWhenTargetHasIndexer()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IIndexable
            {
                string this[int index] { get; } // Indexer
            }

            public class LegacyService { }

            [GenerateAdapter(Target = typeof(IIndexable), Adaptee = typeof(LegacyService))]
            public static partial class Adapters { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenTargetHasIndexer));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP018 diagnostic is reported
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP018");
    }

    [Scenario("ErrorWhenTwoHostsGenerateSameAdapterTypeName")]
    [Fact]
    public void ErrorWhenTwoHostsGenerateSameAdapterTypeName()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IClock
            {
                System.DateTimeOffset Now { get; }
            }

            public interface ITimer
            {
                long Ticks { get; }
            }

            public class LegacyClock { }
            public class LegacyTimer { }

            // First host generates LegacyClockAdapter
            [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock), AdapterTypeName = "SharedAdapter")]
            public static partial class ClockAdapters
            {
                [AdapterMap(TargetMember = nameof(IClock.Now))]
                public static System.DateTimeOffset MapNow(LegacyClock adaptee) => default;
            }

            // Second host attempts to generate the same adapter type name
            [GenerateAdapter(Target = typeof(ITimer), Adaptee = typeof(LegacyTimer), AdapterTypeName = "SharedAdapter")]
            public static partial class TimerAdapters
            {
                [AdapterMap(TargetMember = nameof(ITimer.Ticks))]
                public static long MapTicks(LegacyTimer adaptee) => default;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ErrorWhenTwoHostsGenerateSameAdapterTypeName));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        // PKADP006 diagnostic is reported (at least once, possibly twice)
        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKADP006");
    }

    [Scenario("GenerateAdapter ThrowingStubWithDefaultsRefsAndCustomNamespace")]
    [Fact]
    public void GenerateAdapter_ThrowingStubWithDefaultsRefsAndCustomNamespace()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public enum Mode { Slow = 0, Fast = 1 }

            public interface IService
            {
                string Name { get; }
                void Copy(ref int source, out int destination, in bool enabled, string? label = null, Mode mode = Mode.Fast);
                int Retry(int count = 3);
            }

            public class LegacyService { }

            [GenerateAdapter(
                Target = typeof(IService),
                Adaptee = typeof(LegacyService),
                AdapterTypeName = "ServiceAdapter",
                MissingMap = AdapterMissingMapPolicy.ThrowingStub,
                Sealed = false,
                Namespace = "Generated")]
            public static partial class ServiceAdapters
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateAdapter_ThrowingStubWithDefaultsRefsAndCustomNamespace));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(gs => gs.HintName == "Generated.ServiceAdapter.Adapter.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("namespace Generated;", generatedSource);
        ScenarioExpect.Contains("public partial class ServiceAdapter : global::TestNamespace.IService", generatedSource);
        ScenarioExpect.Contains("Name", generatedSource);
        ScenarioExpect.Contains("get => throw new global::System.NotImplementedException", generatedSource);
        ScenarioExpect.Contains("ref int source", generatedSource);
        ScenarioExpect.Contains("out int destination", generatedSource);
        ScenarioExpect.Contains("in bool enabled", generatedSource);
        ScenarioExpect.Contains("string? label = null", generatedSource);
        ScenarioExpect.Contains("global::TestNamespace.Mode mode = global::TestNamespace.Mode.Fast", generatedSource);
        ScenarioExpect.Contains("int count = 3", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("AdapterDiagnostics OpenGenericAbstractCtorAndBadMapSignatures")]
    [Fact]
    public void AdapterDiagnostics_OpenGenericAbstractCtorAndBadMapSignatures()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public interface IGeneric<T> { T Get(); }
            public abstract class AbstractAdaptee { }
            public abstract class TargetWithoutDefaultCtor
            {
                protected TargetWithoutDefaultCtor(string value) { }
                public abstract string Value { get; }
            }

            public interface IService
            {
                string Name { get; }
                int Get(int value);
                void Copy(ref int value);
            }

            public class LegacyService { }

            [GenerateAdapter(Target = typeof(IGeneric<>), Adaptee = typeof(LegacyService))]
            public static partial class OpenTargetHost { }

            [GenerateAdapter(Target = typeof(IService), Adaptee = typeof(AbstractAdaptee))]
            public static partial class AbstractAdapteeHost { }

            [GenerateAdapter(Target = typeof(TargetWithoutDefaultCtor), Adaptee = typeof(LegacyService))]
            public static partial class MissingCtorHost { }

            [GenerateAdapter(Target = typeof(IService), Adaptee = typeof(LegacyService))]
            public static partial class BadMapHost
            {
                [AdapterMap(TargetMember = nameof(IService.Name))]
                public static int BadProperty(LegacyService adaptee) => 0;

                [AdapterMap(TargetMember = nameof(IService.Get))]
                public static string BadReturn(LegacyService adaptee, string value) => "";

                [AdapterMap(TargetMember = nameof(IService.Copy))]
                public static void BadRef(LegacyService adaptee, int value) { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(AdapterDiagnostics_OpenGenericAbstractCtorAndBadMapSignatures));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKADP002" && d.GetMessage().Contains("IGeneric"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKADP007" && d.GetMessage().Contains("AbstractAdaptee"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKADP012" && d.GetMessage().Contains("TargetWithoutDefaultCtor"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKADP005" && d.GetMessage().Contains("Return type"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKADP005" && d.GetMessage().Contains("ref kind mismatch"));
    }

    [Scenario("AdapterDiagnostics UnboundGenericAdapteeAndInvalidMapParameters")]
    [Fact]
    public void AdapterDiagnostics_UnboundGenericAdapteeAndInvalidMapParameters()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public class LegacyService { }
            public class GenericAdaptee<T> { }

            public interface IName { string Name { get; } }
            public interface IFormat { string Format(int value); }
            public interface IDescribe { string Describe(int value); }

            [GenerateAdapter(Target = typeof(IName), Adaptee = typeof(GenericAdaptee<>))]
            public static partial class OpenAdapteeHost { }

            [GenerateAdapter(Target = typeof(IName), Adaptee = typeof(LegacyService), AdapterTypeName = "MissingAdapteeAdapter")]
            public static partial class MissingAdapteeHost
            {
                [AdapterMap(TargetMember = nameof(IName.Name))]
                public static string MapName() => "";
            }

            [GenerateAdapter(Target = typeof(IName), Adaptee = typeof(LegacyService), AdapterTypeName = "WrongAdapteeAdapter")]
            public static partial class WrongAdapteeHost
            {
                [AdapterMap(TargetMember = nameof(IName.Name))]
                public static string MapName(string adaptee) => "";
            }

            [GenerateAdapter(Target = typeof(IName), Adaptee = typeof(LegacyService), AdapterTypeName = "RefAdapteeAdapter")]
            public static partial class RefAdapteeHost
            {
                [AdapterMap(TargetMember = nameof(IName.Name))]
                public static string MapName(ref LegacyService adaptee) => "";
            }

            [GenerateAdapter(Target = typeof(IName), Adaptee = typeof(LegacyService), AdapterTypeName = "ExtensionAdapteeAdapter")]
            public static partial class ExtensionAdapteeHost
            {
                [AdapterMap(TargetMember = nameof(IName.Name))]
                public static string MapName(this LegacyService adaptee) => "";
            }

            [GenerateAdapter(Target = typeof(IName), Adaptee = typeof(LegacyService), AdapterTypeName = "ScopedAdapteeAdapter")]
            public static partial class ScopedAdapteeHost
            {
                [AdapterMap(TargetMember = nameof(IName.Name))]
                public static string MapName(scoped LegacyService adaptee) => "";
            }

            [GenerateAdapter(Target = typeof(IFormat), Adaptee = typeof(LegacyService), AdapterTypeName = "MissingParameterAdapter")]
            public static partial class MissingParameterHost
            {
                [AdapterMap(TargetMember = nameof(IFormat.Format))]
                public static string MapFormat(LegacyService adaptee) => "";
            }

            [GenerateAdapter(Target = typeof(IDescribe), Adaptee = typeof(LegacyService), AdapterTypeName = "WrongParameterTypeAdapter")]
            public static partial class WrongParameterTypeHost
            {
                [AdapterMap(TargetMember = nameof(IDescribe.Describe))]
                public static string MapDescribe(LegacyService adaptee, string value) => "";
            }

            [GenerateAdapter(Target = typeof(IName), Adaptee = typeof(LegacyService), AdapterTypeName = "PropertyParameterAdapter")]
            public static partial class PropertyParameterHost
            {
                [AdapterMap(TargetMember = nameof(IName.Name))]
                public static string MapName(LegacyService adaptee, int extra) => "";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(AdapterDiagnostics_UnboundGenericAdapteeAndInvalidMapParameters));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKADP007" && d.GetMessage().Contains("GenericAdaptee"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKADP005" && d.GetMessage().Contains("First parameter must be"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKADP005" && d.GetMessage().Contains("but was 'string'"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKADP005" && d.GetMessage().Contains("must not have a ref"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKADP005" && d.GetMessage().Contains("'scoped' modifier"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKADP005" && d.GetMessage().Contains("Expected 1 parameters"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKADP005" && d.GetMessage().Contains("type mismatch"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKADP005" && d.GetMessage().Contains("Property getter mapping"));
    }

    [Scenario("GenerateAdapter PartialConflictAbstractBaseAndRefReadonlyDefaults")]
    [Fact]
    public void GenerateAdapter_PartialConflictAbstractBaseAndRefReadonlyDefaults()
    {
        const string source = """
            using PatternKit.Generators.Adapter;

            namespace TestNamespace;

            public enum Mode { Slow = 0, Fast = 1 }

            public abstract class OperationBase
            {
                internal OperationBase() { }
                public abstract string Name { get; }
            }

            public abstract class OperationContract : OperationBase
            {
                internal OperationContract() { }
                public abstract int Execute(ref int value, out int written, in bool enabled, ref readonly long snapshot, Mode mode = (Mode)99);
            }

            public class LegacyOperation { }

            public partial class LegacyOperationToOperationContractAdapter { }

            [GenerateAdapter(Target = typeof(OperationContract), Adaptee = typeof(LegacyOperation))]
            public static partial class OperationAdapters
            {
                [AdapterMap(TargetMember = nameof(OperationBase.Name))]
                public static string MapName(LegacyOperation adaptee) => "ready";

                [AdapterMap(TargetMember = nameof(OperationContract.Execute))]
                public static int MapExecute(LegacyOperation adaptee, ref int value, out int written, in bool enabled, ref readonly long snapshot, Mode mode)
                {
                    written = value;
                    return enabled ? (int)snapshot : 0;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateAdapter_PartialConflictAbstractBaseAndRefReadonlyDefaults));
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(gs => gs.HintName == "TestNamespace.LegacyOperationToOperationContractAdapter.Adapter.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("public sealed partial class LegacyOperationToOperationContractAdapter", generatedSource);
        ScenarioExpect.Contains("public override string Name", generatedSource);
        ScenarioExpect.Contains("ref readonly long snapshot", generatedSource);
        ScenarioExpect.Contains("in snapshot", generatedSource);
        ScenarioExpect.Contains("global::TestNamespace.Mode mode = (global::TestNamespace.Mode)99", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateAdapter MetadataTargetAndMetadataConflict")]
    [Fact]
    public void GenerateAdapter_MetadataTargetAndMetadataConflict()
    {
        const string contractSource = """
            namespace External
            {
                public interface IExternal
                {
                    string Name { get; }
                    int Execute(int value);
                }

                public class Legacy { }
            }

            namespace Consumer
            {
                public class LegacyToIExternalAdapter { }
            }
            """;

        var contractCompilation = RoslynTestHelpers.CreateCompilation(contractSource, "ExternalContracts");
        using var contractImage = new MemoryStream();
        var contractEmit = contractCompilation.Emit(contractImage);
        ScenarioExpect.True(contractEmit.Success, string.Join("\n", contractEmit.Diagnostics));

        var contractReference = MetadataReference.CreateFromImage(contractImage.ToArray());

        const string generateSource = """
            using External;
            using PatternKit.Generators.Adapter;

            namespace GeneratedConsumer;

            [GenerateAdapter(Target = typeof(IExternal), Adaptee = typeof(Legacy))]
            public static partial class ExternalAdapters
            {
                [AdapterMap(TargetMember = nameof(IExternal.Name))]
                public static string MapName(Legacy adaptee) => "ready";

                [AdapterMap(TargetMember = nameof(IExternal.Execute))]
                public static int MapExecute(Legacy adaptee, int value) => value;
            }
            """;

        var generateCompilation = RoslynTestHelpers.CreateCompilation(
            generateSource,
            nameof(GenerateAdapter_MetadataTargetAndMetadataConflict) + "Generate",
            extra: contractReference);
        var gen = new AdapterGenerator();
        _ = RoslynTestHelpers.Run(generateCompilation, gen, out var generateResult, out var updated);

        ScenarioExpect.All(generateResult.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var generatedSource = generateResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(gs => gs.HintName == "GeneratedConsumer.LegacyToIExternalAdapter.Adapter.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("public string Name", generatedSource);
        ScenarioExpect.Contains("public int Execute(int value)", generatedSource);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success, string.Join("\n", updated.GetDiagnostics()));

        const string conflictSource = """
            using External;
            using PatternKit.Generators.Adapter;

            namespace Consumer;

            [GenerateAdapter(Target = typeof(IExternal), Adaptee = typeof(Legacy))]
            public static partial class ExternalAdapters
            {
                [AdapterMap(TargetMember = nameof(IExternal.Name))]
                public static string MapName(Legacy adaptee) => "ready";

                [AdapterMap(TargetMember = nameof(IExternal.Execute))]
                public static int MapExecute(Legacy adaptee, int value) => value;
            }
            """;

        var conflictCompilation = RoslynTestHelpers.CreateCompilation(
            conflictSource,
            nameof(GenerateAdapter_MetadataTargetAndMetadataConflict) + "Conflict",
            extra: contractReference);
        _ = RoslynTestHelpers.Run(conflictCompilation, gen, out var conflictResult, out _);

        var diagnostics = conflictResult.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKADP006" && d.GetMessage().Contains("LegacyToIExternalAdapter"));
    }
}
