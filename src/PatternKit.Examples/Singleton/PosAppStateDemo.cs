using PatternKit.Creational.Singleton;

namespace PatternKit.Examples.Singleton;

// ---------------- Domain-ish types for the demo ----------------

public sealed record StoreConfig(string StoreId, decimal TaxRate, string[] Flags)
{
    public static StoreConfig Load() => new("STORE-001", 0.0875m, ["cash-discount", "nickel-rounding"]);
}

public sealed class PricingCache
{
    public int WarmedCount { get; private set; }
    public void Prewarm(IEnumerable<string> popularSkus)
    {
        // simulate work
        WarmedCount += popularSkus.Count();
    }
}

public sealed class DeviceRegistry
{
    public bool PrinterReady { get; private set; }
    public bool DrawerReady { get; private set; }
    public void ConnectAll()
    {
        PrinterReady = true;
        DrawerReady = true;
    }
}

/// <summary>
/// Application-wide state for a high-performance POS app: config, warmed caches, and connected devices.
/// </summary>
public sealed class PosAppState
{
    public required StoreConfig Config { get; init; }
    public required PricingCache Pricing { get; init; }
    public required DeviceRegistry Devices { get; init; }

    // handy for verifying creation/init sequencing in examples
    public List<string> Log { get; } = [];
}

/// <summary>
/// Demonstrates building a fluent, thread-safe singleton for POS app state with init hooks and eager/lazy creation.
/// </summary>
public static class PosAppStateDemo
{
    // --- Counters for tests/verification ---
    public static int FactoryCalls;

    public static void ResetCounters() => FactoryCalls = 0;

    private static PosAppState Factory()
    {
        Interlocked.Increment(ref FactoryCalls);
        var state = new PosAppState
        {
            Config = StoreConfig.Load(),
            Pricing = new PricingCache(),
            Devices = new DeviceRegistry(),
        };
        state.Log.Add("factory:config");
        return state;
    }

    private static void InitPricing(PosAppState s)
    {
        s.Pricing.Prewarm(["SKU-1", "SKU-2", "SKU-3"]);
        s.Log.Add("init:pricing");
    }

    private static void InitDevices(PosAppState s)
    {
        s.Devices.ConnectAll();
        s.Log.Add("init:devices");
    }

    /// <summary>
    /// Builds a lazy singleton (created on first access) with composed init hooks.
    /// </summary>
    public static Singleton<PosAppState> BuildLazy()
        => Singleton<PosAppState>
            .Create(Factory)
            .Init(InitPricing)
            .Init(InitDevices)
            .Build();

    /// <summary>
    /// Builds an eager singleton (created at Build time).
    /// </summary>
    public static Singleton<PosAppState> BuildEager()
        => Singleton<PosAppState>
            .Create(Factory)
            .Init(InitPricing)
            .Init(InitDevices)
            .Eager()
            .Build();

    /// <summary>
    /// Small console-friendly demo: returns log lines showing factory/init order.
    /// </summary>
    public static List<string> RunLazyOnce()
    {
        ResetCounters();
        var s = BuildLazy();
        var _ = s.Instance; // trigger creation
        return s.Instance.Log.ToList();
    }

    /// <summary>
    /// Same as RunLazyOnce but demonstrates eager creation; useful for docs/tests.
    /// </summary>
    public static List<string> RunEagerOnce()
    {
        ResetCounters();
        var s = BuildEager(); // created at Build()
        var _ = s.Instance;   // no-op creation-wise
        return s.Instance.Log.ToList();
    }
}
