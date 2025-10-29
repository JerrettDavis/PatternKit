using PatternKit.Behavioral.Visitor;

namespace PatternKit.Examples.VisitorDemo;

/// <summary>
/// Base type for payment tenders in a POS/integration flow.
/// Each tender carries an <paramref name="Amount"/> used for totals and fallbacks.
/// </summary>
/// <param name="Amount">The monetary amount represented by this tender.</param>
public abstract record Tender(decimal Amount);

/// <summary>
/// Cash tender representing physical currency.
/// </summary>
/// <param name="Value">The cash value paid.</param>
public sealed record Cash(decimal Value) : Tender(Value);

/// <summary>
/// Card tender (credit/debit) including brand and masked PAN.
/// </summary>
/// <param name="Brand">Card brand (e.g., VISA, MC).</param>
/// <param name="Last4">Masked PAN suffix for audit display.</param>
/// <param name="Value">Authorized charge amount.</param>
public sealed record Card(string Brand, string Last4, decimal Value) : Tender(Value);

/// <summary>
/// Gift card tender redeemed by code.
/// </summary>
/// <param name="Code">Gift card code displayed on receipt.</param>
/// <param name="Value">Redeemed amount.</param>
public sealed record GiftCard(string Code, decimal Value) : Tender(Value);

/// <summary>
/// Store credit tender tied to a customer identity.
/// </summary>
/// <param name="CustomerId">Internal customer identifier.</param>
/// <param name="Value">Credit amount applied.</param>
public sealed record StoreCredit(string CustomerId, decimal Value) : Tender(Value);

/// <summary>
/// Catch‑all tender for sources that do not have a dedicated type.
/// Useful for integration bring‑up and minimizing runtime failures.
/// </summary>
/// <param name="Description">Short label for the tender source.</param>
/// <param name="Value">Applied amount.</param>
public sealed record Unknown(string Description, decimal Value) : Tender(Value);

public static class ReceiptRendering
{
    /// <summary>
    /// Creates a result visitor that formats tenders into printable receipt lines.
    /// Includes a default formatter for unknown tender types.
    /// </summary>
    /// <returns>A reusable, thread‑safe visitor instance.</returns>
    public static Visitor<Tender, string> CreateRenderer() => Visitor<Tender, string>
        .Create()
        .On<Cash>(t => $"Cash          {t.Value,8:C}")
        .On<Card>(t => $"{t.Brand} ****{t.Last4,4} {t.Value,8:C}")
        .On<GiftCard>(t => $"GiftCard {t.Code,-8} {t.Value,8:C}")
        .On<StoreCredit>(t => $"StoreCredit {t.CustomerId,-6} {t.Value,8:C}")
        .Default(t => $"Other           {t.Amount,8:C}")
        .Build();
}

public interface ITenderHandler
{
    /// <summary>Handles <see cref="Cash"/> tenders.</summary>
    /// <param name="t">The cash tender.</param>
    void Cash(Cash t);
    /// <summary>Handles <see cref="Card"/> tenders.</summary>
    /// <param name="t">The card tender.</param>
    void Card(Card t);
    /// <summary>Handles <see cref="GiftCard"/> tenders.</summary>
    /// <param name="t">The gift card tender.</param>
    void Gift(GiftCard t);
    /// <summary>Handles <see cref="StoreCredit"/> tenders.</summary>
    /// <param name="t">The store credit tender.</param>
    void Credit(StoreCredit t);
    /// <summary>Fallback for tenders that have no specialized handler.</summary>
    /// <param name="t">The unknown tender.</param>
    void Fallback(Tender t);
}

public sealed class CountersHandler : ITenderHandler
{
    /// <summary>Total number of cash tenders processed.</summary>
    public int CashCount, CardCount, GiftCount, CreditCount, FallbackCount;
    /// <summary>Aggregated amount across all tenders processed.</summary>
    public decimal Total;
    public void Cash(Cash t) { CashCount++; Total += t.Value; }
    public void Card(Card t) { CardCount++; Total += t.Value; }
    public void Gift(GiftCard t) { GiftCount++; Total += t.Value; }
    public void Credit(StoreCredit t) { CreditCount++; Total += t.Value; }
    public void Fallback(Tender t) { FallbackCount++; Total += t.Amount; }
}

public static class Routing
{
    /// <summary>
    /// Creates an action visitor that routes tenders to the provided handler.
    /// </summary>
    /// <param name="handler">The handler receiving type‑specific callbacks.</param>
    /// <returns>A reusable, thread‑safe router.</returns>
    public static ActionVisitor<Tender> CreateRouter(ITenderHandler handler) => ActionVisitor<Tender>
        .Create()
        .On<Cash>(handler.Cash)
        .On<Card>(handler.Card)
        .On<GiftCard>(handler.Gift)
        .On<StoreCredit>(handler.Credit)
        .Default(handler.Fallback)
        .Build();
}

public static class Demo
{
    /// <summary>
    /// Runs an end‑to‑end demo: routes tenders, then renders receipt lines.
    /// </summary>
    /// <returns>
    /// A tuple containing the rendered receipt lines and the processing counters
    /// collected by <see cref="CountersHandler"/>.
    /// </returns>
    public static (string[] receipt, CountersHandler counters) Run()
    {
        var renderer = ReceiptRendering.CreateRenderer();
        var counters = new CountersHandler();
        var router = Routing.CreateRouter(counters);

        var tenders = new Tender[]
        {
            new Cash(10.00m),
            new Card("VISA", "4242", 15.75m),
            new GiftCard("GFT-001", 5.00m),
            new StoreCredit("C123", 3.25m),
            new Unknown("PromoVoucher", 2.00m),
        };

        foreach (var t in tenders)
        {
            router.Visit(t);
        }

        var lines = tenders.Select(t => renderer.Visit(t)).ToArray();
        return (lines, counters);
    }
}
