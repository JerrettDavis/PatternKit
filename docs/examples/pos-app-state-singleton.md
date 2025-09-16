# POS App State Singleton (eager/lazy + init hooks)

A production-shaped demo that builds an application-wide POS state using the fluent, thread-safe `Singleton<T>`:

- Factory wires together config, caches, and devices
- Init hooks warm the pricing cache and connect devices
- Export both lazy and eager variants for different startup needs

---

## Why

High-throughput POS apps often need a single, shared application state: configuration, warmed caches, and connected device handles. You want:

- Constructed once, thread-safe
- One-time initialization (cache warmup, device connect)
- Choice of lazy (first access) or eager (at startup) creation

`Singleton<T>` gives you that with a tiny, allocation-light API.

---

## Quick look

```csharp
using PatternKit.Examples.Singleton;

// Lazy (create on first access)
var lazy = PosAppStateDemo.BuildLazy();
var state = lazy.Instance; // created here

// Eager (create at Build time)
var eager = PosAppStateDemo.BuildEager(); // created now
var same = eager.Instance;                 // no-op
```

The demo exposes two console-friendly helpers that return the one-time log:

```csharp
var lazyLog  = PosAppStateDemo.RunLazyOnce();  // ["factory:config","init:pricing","init:devices"]
var eagerLog = PosAppStateDemo.RunEagerOnce(); // same as above
```

---

## What it builds

`PosAppState` bundles:

- `StoreConfig` — loaded from a factory (`Load()`)
- `PricingCache` — prewarmed via an init hook
- `DeviceRegistry` — connects printer/cash drawer in an init hook

Init hooks run once, in registration order.

---

## Code sketch

```csharp
// Factory builds the state graph
private static PosAppState Factory()
{
    Interlocked.Increment(ref PosAppStateDemo.FactoryCalls);
    var s = new PosAppState
    {
        Config  = StoreConfig.Load(),
        Pricing = new PricingCache(),
        Devices = new DeviceRegistry(),
    };
    s.Log.Add("factory:config");
    return s;
}

// One-time init hooks (compose in order)
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

// Lazy vs eager
public static Singleton<PosAppState> BuildLazy() =>
    Singleton<PosAppState>
        .Create(Factory)
        .Init(InitPricing)
        .Init(InitDevices)
        .Build();

public static Singleton<PosAppState> BuildEager() =>
    Singleton<PosAppState>
        .Create(Factory)
        .Init(InitPricing)
        .Init(InitDevices)
        .Eager()
        .Build();
```

---

## Semantics verified by tests

- Factory invoked exactly once under heavy concurrency
- Init order preserved: `init:pricing` then `init:devices`
- State invariants: pricing warmed, devices ready, config loaded
- Eager creation runs init during `Build()`; lazy runs at first `Instance`

See: `PatternKit.Examples.Tests/Singleton/PosAppStateDemoTests.cs`.

---

## Tips

- Keep init hooks idempotent for easier tests and retries
- Prefer `static` lambdas/method groups to avoid captures
- Use eager creation when startup time is predictable and you want deterministic failure early; use lazy for optional features

---

## Related

- [Creational.Singleton](../patterns/creational/singleton/singleton.md) — API reference and rationale
- [Auth & Logging Chain](auth-logging-chain.md) — chain style rules with strict stop
- [Mediated Transaction Pipeline](mediated-transaction-pipeline.md) — end-to-end POS pipeline

