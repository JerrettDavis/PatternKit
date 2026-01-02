using PatternKit.Examples.VisitorDemo;

namespace PatternKit.Examples.Tests.VisitorDemoTests;

public sealed class TenderRecordTests
{
    [Fact]
    public void Cash_Record_Works()
    {
        var cash = new Cash(10.50m);

        Assert.Equal(10.50m, cash.Value);
        Assert.Equal(10.50m, cash.Amount);
    }

    [Fact]
    public void Card_Record_Works()
    {
        var card = new Card("VISA", "4242", 25.75m);

        Assert.Equal("VISA", card.Brand);
        Assert.Equal("4242", card.Last4);
        Assert.Equal(25.75m, card.Value);
        Assert.Equal(25.75m, card.Amount);
    }

    [Fact]
    public void GiftCard_Record_Works()
    {
        var gift = new GiftCard("GFT-123", 15.00m);

        Assert.Equal("GFT-123", gift.Code);
        Assert.Equal(15.00m, gift.Value);
        Assert.Equal(15.00m, gift.Amount);
    }

    [Fact]
    public void StoreCredit_Record_Works()
    {
        var credit = new StoreCredit("CUST-456", 5.25m);

        Assert.Equal("CUST-456", credit.CustomerId);
        Assert.Equal(5.25m, credit.Value);
        Assert.Equal(5.25m, credit.Amount);
    }

    [Fact]
    public void Unknown_Record_Works()
    {
        var unknown = new Unknown("PromoVoucher", 2.00m);

        Assert.Equal("PromoVoucher", unknown.Description);
        Assert.Equal(2.00m, unknown.Value);
        Assert.Equal(2.00m, unknown.Amount);
    }
}

public sealed class ReceiptRenderingTests
{
    [Fact]
    public void CreateRenderer_Returns_NonNull()
    {
        var renderer = ReceiptRendering.CreateRenderer();

        Assert.NotNull(renderer);
    }

    [Fact]
    public void Renderer_Formats_Cash()
    {
        var renderer = ReceiptRendering.CreateRenderer();
        var cash = new Cash(10.00m);

        var line = renderer.Dispatch(cash);

        Assert.Contains("Cash", line);
        Assert.Contains("10.00", line);
    }

    [Fact]
    public void Renderer_Formats_Card()
    {
        var renderer = ReceiptRendering.CreateRenderer();
        var card = new Card("VISA", "4242", 15.75m);

        var line = renderer.Dispatch(card);

        Assert.Contains("VISA", line);
        Assert.Contains("4242", line);
        Assert.Contains("15.75", line);
    }

    [Fact]
    public void Renderer_Formats_GiftCard()
    {
        var renderer = ReceiptRendering.CreateRenderer();
        var gift = new GiftCard("GFT-001", 5.00m);

        var line = renderer.Dispatch(gift);

        Assert.Contains("GiftCard", line);
        Assert.Contains("GFT-001", line);
        Assert.Contains("5.00", line);
    }

    [Fact]
    public void Renderer_Formats_StoreCredit()
    {
        var renderer = ReceiptRendering.CreateRenderer();
        var credit = new StoreCredit("C123", 3.25m);

        var line = renderer.Dispatch(credit);

        Assert.Contains("StoreCredit", line);
        Assert.Contains("C123", line);
        Assert.Contains("3.25", line);
    }

    [Fact]
    public void Renderer_Formats_Unknown_With_Default()
    {
        var renderer = ReceiptRendering.CreateRenderer();
        var unknown = new Unknown("PromoVoucher", 2.00m);

        var line = renderer.Dispatch(unknown);

        Assert.Contains("Other", line);
        Assert.Contains("2.00", line);
    }
}

public sealed class CountersHandlerTests
{
    [Fact]
    public void Cash_Increments_CashCount()
    {
        var handler = new CountersHandler();

        handler.Cash(new Cash(10.00m));

        Assert.Equal(1, handler.CashCount);
        Assert.Equal(10.00m, handler.Total);
    }

    [Fact]
    public void Card_Increments_CardCount()
    {
        var handler = new CountersHandler();

        handler.Card(new Card("MC", "1234", 20.00m));

        Assert.Equal(1, handler.CardCount);
        Assert.Equal(20.00m, handler.Total);
    }

    [Fact]
    public void Gift_Increments_GiftCount()
    {
        var handler = new CountersHandler();

        handler.Gift(new GiftCard("G-001", 5.00m));

        Assert.Equal(1, handler.GiftCount);
        Assert.Equal(5.00m, handler.Total);
    }

    [Fact]
    public void Credit_Increments_CreditCount()
    {
        var handler = new CountersHandler();

        handler.Credit(new StoreCredit("C-001", 3.00m));

        Assert.Equal(1, handler.CreditCount);
        Assert.Equal(3.00m, handler.Total);
    }

    [Fact]
    public void Fallback_Increments_FallbackCount()
    {
        var handler = new CountersHandler();
        var unknown = new Unknown("Misc", 1.50m);

        handler.Fallback(unknown);

        Assert.Equal(1, handler.FallbackCount);
        Assert.Equal(1.50m, handler.Total);
    }

    [Fact]
    public void Multiple_Tenders_Accumulate_Correctly()
    {
        var handler = new CountersHandler();

        handler.Cash(new Cash(10.00m));
        handler.Cash(new Cash(5.00m));
        handler.Card(new Card("VISA", "1111", 20.00m));
        handler.Gift(new GiftCard("G1", 7.50m));

        Assert.Equal(2, handler.CashCount);
        Assert.Equal(1, handler.CardCount);
        Assert.Equal(1, handler.GiftCount);
        Assert.Equal(0, handler.CreditCount);
        Assert.Equal(0, handler.FallbackCount);
        Assert.Equal(42.50m, handler.Total);
    }
}

public sealed class RoutingTests
{
    [Fact]
    public void CreateRouter_Returns_NonNull()
    {
        var handler = new CountersHandler();
        var router = Routing.CreateRouter(handler);

        Assert.NotNull(router);
    }

    [Fact]
    public void Router_Dispatches_Cash()
    {
        var handler = new CountersHandler();
        var router = Routing.CreateRouter(handler);

        router.Dispatch(new Cash(10.00m));

        Assert.Equal(1, handler.CashCount);
    }

    [Fact]
    public void Router_Dispatches_Card()
    {
        var handler = new CountersHandler();
        var router = Routing.CreateRouter(handler);

        router.Dispatch(new Card("AMEX", "0000", 50.00m));

        Assert.Equal(1, handler.CardCount);
    }

    [Fact]
    public void Router_Dispatches_GiftCard()
    {
        var handler = new CountersHandler();
        var router = Routing.CreateRouter(handler);

        router.Dispatch(new GiftCard("G2", 25.00m));

        Assert.Equal(1, handler.GiftCount);
    }

    [Fact]
    public void Router_Dispatches_StoreCredit()
    {
        var handler = new CountersHandler();
        var router = Routing.CreateRouter(handler);

        router.Dispatch(new StoreCredit("C2", 15.00m));

        Assert.Equal(1, handler.CreditCount);
    }

    [Fact]
    public void Router_Dispatches_Unknown_To_Fallback()
    {
        var handler = new CountersHandler();
        var router = Routing.CreateRouter(handler);

        router.Dispatch(new Unknown("External", 5.00m));

        Assert.Equal(1, handler.FallbackCount);
    }

    [Fact]
    public void Router_Dispatches_Multiple_Tenders()
    {
        var handler = new CountersHandler();
        var router = Routing.CreateRouter(handler);
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
            router.Dispatch(t);
        }

        Assert.Equal(1, handler.CashCount);
        Assert.Equal(1, handler.CardCount);
        Assert.Equal(1, handler.GiftCount);
        Assert.Equal(1, handler.CreditCount);
        Assert.Equal(1, handler.FallbackCount);
        Assert.Equal(36.00m, handler.Total);
    }
}

public sealed class DemoTests
{
    [Fact]
    public void Run_Returns_Receipt_And_Counters()
    {
        var (receipt, counters) = Demo.Run();

        Assert.NotNull(receipt);
        Assert.NotNull(counters);
        Assert.Equal(5, receipt.Length);
    }

    [Fact]
    public void Run_Counters_Match_Expected()
    {
        var (_, counters) = Demo.Run();

        Assert.Equal(1, counters.CashCount);
        Assert.Equal(1, counters.CardCount);
        Assert.Equal(1, counters.GiftCount);
        Assert.Equal(1, counters.CreditCount);
        Assert.Equal(1, counters.FallbackCount);
        Assert.Equal(36.00m, counters.Total);
    }

    [Fact]
    public void Run_Receipt_Contains_All_Tender_Types()
    {
        var (receipt, _) = Demo.Run();

        Assert.Contains(receipt, r => r.Contains("Cash"));
        Assert.Contains(receipt, r => r.Contains("VISA"));
        Assert.Contains(receipt, r => r.Contains("GiftCard"));
        Assert.Contains(receipt, r => r.Contains("StoreCredit"));
        Assert.Contains(receipt, r => r.Contains("Other"));
    }
}
