# Visitor — POS Tender Routing & Receipt Rendering

This example demonstrates using Visitors to route payment tenders to handlers and to render receipt lines. It mirrors common POS/integration needs: dispatch by tender type for processing and projection to a printable receipt.

---

## The Domain

```csharp
abstract record Tender(decimal Amount);
record Cash(decimal Value) : Tender(Value);
record Card(string Brand, string Last4, decimal Value) : Tender(Value);
record GiftCard(string Code, decimal Value) : Tender(Value);
record StoreCredit(string CustomerId, decimal Value) : Tender(Value);
```

Full example source: `src/PatternKit.Examples/VisitorDemo/VisitorDemo.cs:1`

---

## Rendering (Result Visitor)

Goal: Turn tenders into receipt lines.

```csharp
public static class ReceiptRendering
{
    public static Visitor<Tender, string> CreateRenderer() => Visitor<Tender, string>
        .Create()
        .On<Cash>(t => $"Cash          {t.Value,8:C}")
        .On<Card>(t => $"{t.Brand} ****{t.Last4,4} {t.Value,8:C}")
        .On<GiftCard>(t => $"GiftCard {t.Code,-8} {t.Value,8:C}")
        .On<StoreCredit>(t => $"StoreCredit {t.CustomerId,-6} {t.Value,8:C}")
        .Default(t => $"Other           {t.Amount,8:C}")
        .Build();
}
```

Usage
```csharp
var renderer = ReceiptRendering.CreateRenderer();
var lines = tenders.Select(t => renderer.Visit(t)).ToArray();
```

---

## Routing (Action Visitor)

Goal: Route tenders to a handler interface and keep counts.

```csharp
public interface ITenderHandler
{
    void Cash(Cash t);
    void Card(Card t);
    void Gift(GiftCard t);
    void Credit(StoreCredit t);
    void Fallback(Tender t);
}

public sealed class CountersHandler : ITenderHandler
{
    public int CashCount, CardCount, GiftCount, CreditCount, FallbackCount;
    public decimal Total;
    public void Cash(Cash t) { CashCount++; Total += t.Value; }
    public void Card(Card t) { CardCount++; Total += t.Value; }
    public void Gift(GiftCard t) { GiftCount++; Total += t.Value; }
    public void Credit(StoreCredit t) { CreditCount++; Total += t.Value; }
    public void Fallback(Tender t) { FallbackCount++; Total += t.Amount; }
}

public static class Routing
{
    public static ActionVisitor<Tender> CreateRouter(ITenderHandler handler) => ActionVisitor<Tender>
        .Create()
        .On<Cash>(handler.Cash)
        .On<Card>(handler.Card)
        .On<GiftCard>(handler.Gift)
        .On<StoreCredit>(handler.Credit)
        .Default(handler.Fallback)
        .Build();
}
```

Usage
```csharp
var counters = new CountersHandler();
var router = Routing.CreateRouter(counters);
foreach (var t in tenders) router.Visit(t);
```

---

## End‑to‑End Demo

```csharp
var tenders = new Tender[]
{
    new Cash(10.00m),
    new Card("VISA", "4242", 15.75m),
    new GiftCard("GFT-001", 5.00m),
    new StoreCredit("C123", 3.25m),
};

var renderer = ReceiptRendering.CreateRenderer();
var counters  = new CountersHandler();
var router    = Routing.CreateRouter(counters);

foreach (var t in tenders)
    router.Visit(t);

var lines = tenders.Select(t => renderer.Visit(t)).ToArray();
```

---

## Why Visitor Here

- Clean separation of types and operations (routes, receipts).
- First‑match‑wins mapping keeps specific types first.
- Built visitors are immutable and thread‑safe.

Operational notes
- Keep visitors as application singletons; rebuild only when behavior changes.
- For multi‑tenant rules, compose visitors per tenant from shared primitives.
- Add a default to avoid runtime errors on unknown tender types; log and continue.
