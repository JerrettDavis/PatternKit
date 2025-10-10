# Reactive Transaction with Observer

This example builds a non-trivial transaction model that computes totals dynamically from line items, loyalty tier, payment kind, and tax rate.
It uses tiny reactive primitives powered by PatternKit’s `Observer<T>` so everything updates automatically.

What it demonstrates
- Reactive inputs: line items, loyalty tier, payment kind, tax rate
- Computed outputs: subtotal, line-item discounts, loyalty and payment discounts, tax, total
- UI-like dependents: `CanCheckout`, `DiscountBadge`
- Change broadcasting via both typed subscriptions and simple “property name” notifications

Code
```csharp
using PatternKit.Examples.ObserverDemo;

var tx = new ReactiveTransaction();

// Listen for total changes
using var _ = tx.Total.Subscribe((old, @new) => Console.WriteLine($"Total: {old:C} -> {@new:C}"));

// Build cart
tx.AddItem(new LineItem("WIDGET", 2, 19.99m));
tx.AddItem(new LineItem("GADGET", 1, 49.99m, DiscountPct: 0.10m)); // 10% line discount

// Promotions
tx.SetTier(LoyaltyTier.Gold);       // ~7%
tx.SetPayment(PaymentKind.StoreCard); // extra 5% after loyalty

// Tax
tx.SetTaxRate(0.07m);

Console.WriteLine($"Subtotal: {tx.Subtotal.Value:C}");
Console.WriteLine($"Discounts: line={tx.LineItemDiscounts.Value:C}, loyalty={tx.LoyaltyDiscount.Value:C}, pay={tx.PaymentDiscount.Value:C}");
Console.WriteLine($"Tax: {tx.Tax.Value:C}");
Console.WriteLine($"Total: {tx.Total.Value:C}");
Console.WriteLine($"CanCheckout: {tx.CanCheckout.Value}");
Console.WriteLine(tx.DiscountBadge.Value ?? "No discounts");
```

How it works
- `ObservableList<T>` and `ObservableVar<T>` publish change events.
- `ReactiveTransaction` recomputes whenever any input changes and pushes new values to reactive outputs.
- Consumers subscribe specifically (e.g., `Total`) or to property-name updates via a small `PropertyChangedHub`.

Where to look
- Source: `src/PatternKit.Examples/ObserverDemo/ReactiveTransaction.cs`
- Reactive primitives: `src/PatternKit.Examples/ObserverDemo/ReactivePrimitives.cs`
- Tests: `test/PatternKit.Examples.Tests/ObserverDemo/ReactiveTransactionTests.cs`

