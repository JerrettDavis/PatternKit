using PatternKit.Examples.VisitorDemo;
using TinyBDD;

namespace PatternKit.Examples.Tests.VisitorDemoTests;

public sealed class TenderRecordTests
{
    [Scenario("Cash Record Works")]
    [Fact]
    public void Cash_Record_Works()
    {
        var cash = new Cash(10.50m);

        ScenarioExpect.Equal(10.50m, cash.Value);
        ScenarioExpect.Equal(10.50m, cash.Amount);
    }

    [Scenario("Card Record Works")]
    [Fact]
    public void Card_Record_Works()
    {
        var card = new Card("VISA", "4242", 25.75m);

        ScenarioExpect.Equal("VISA", card.Brand);
        ScenarioExpect.Equal("4242", card.Last4);
        ScenarioExpect.Equal(25.75m, card.Value);
        ScenarioExpect.Equal(25.75m, card.Amount);
    }

    [Scenario("GiftCard Record Works")]
    [Fact]
    public void GiftCard_Record_Works()
    {
        var gift = new GiftCard("GFT-123", 15.00m);

        ScenarioExpect.Equal("GFT-123", gift.Code);
        ScenarioExpect.Equal(15.00m, gift.Value);
        ScenarioExpect.Equal(15.00m, gift.Amount);
    }

    [Scenario("StoreCredit Record Works")]
    [Fact]
    public void StoreCredit_Record_Works()
    {
        var credit = new StoreCredit("CUST-456", 5.25m);

        ScenarioExpect.Equal("CUST-456", credit.CustomerId);
        ScenarioExpect.Equal(5.25m, credit.Value);
        ScenarioExpect.Equal(5.25m, credit.Amount);
    }

    [Scenario("Unknown Record Works")]
    [Fact]
    public void Unknown_Record_Works()
    {
        var unknown = new Unknown("PromoVoucher", 2.00m);

        ScenarioExpect.Equal("PromoVoucher", unknown.Description);
        ScenarioExpect.Equal(2.00m, unknown.Value);
        ScenarioExpect.Equal(2.00m, unknown.Amount);
    }
}

public sealed class ReceiptRenderingTests
{
    [Scenario("CreateRenderer Returns NonNull")]
    [Fact]
    public void CreateRenderer_Returns_NonNull()
    {
        var renderer = ReceiptRendering.CreateRenderer();

        ScenarioExpect.NotNull(renderer);
    }

    [Scenario("Renderer Formats Cash")]
    [Fact]
    public void Renderer_Formats_Cash()
    {
        var renderer = ReceiptRendering.CreateRenderer();
        var cash = new Cash(10.00m);

        var line = renderer.Dispatch(cash);

        ScenarioExpect.Contains("Cash", line);
        ScenarioExpect.Contains("10.00", line);
    }

    [Scenario("Renderer Formats Card")]
    [Fact]
    public void Renderer_Formats_Card()
    {
        var renderer = ReceiptRendering.CreateRenderer();
        var card = new Card("VISA", "4242", 15.75m);

        var line = renderer.Dispatch(card);

        ScenarioExpect.Contains("VISA", line);
        ScenarioExpect.Contains("4242", line);
        ScenarioExpect.Contains("15.75", line);
    }

    [Scenario("Renderer Formats GiftCard")]
    [Fact]
    public void Renderer_Formats_GiftCard()
    {
        var renderer = ReceiptRendering.CreateRenderer();
        var gift = new GiftCard("GFT-001", 5.00m);

        var line = renderer.Dispatch(gift);

        ScenarioExpect.Contains("GiftCard", line);
        ScenarioExpect.Contains("GFT-001", line);
        ScenarioExpect.Contains("5.00", line);
    }

    [Scenario("Renderer Formats StoreCredit")]
    [Fact]
    public void Renderer_Formats_StoreCredit()
    {
        var renderer = ReceiptRendering.CreateRenderer();
        var credit = new StoreCredit("C123", 3.25m);

        var line = renderer.Dispatch(credit);

        ScenarioExpect.Contains("StoreCredit", line);
        ScenarioExpect.Contains("C123", line);
        ScenarioExpect.Contains("3.25", line);
    }

    [Scenario("Renderer Formats Unknown With Default")]
    [Fact]
    public void Renderer_Formats_Unknown_With_Default()
    {
        var renderer = ReceiptRendering.CreateRenderer();
        var unknown = new Unknown("PromoVoucher", 2.00m);

        var line = renderer.Dispatch(unknown);

        ScenarioExpect.Contains("Other", line);
        ScenarioExpect.Contains("2.00", line);
    }
}

public sealed class CountersHandlerTests
{
    [Scenario("Cash Increments CashCount")]
    [Fact]
    public void Cash_Increments_CashCount()
    {
        var handler = new CountersHandler();

        handler.Cash(new Cash(10.00m));

        ScenarioExpect.Equal(1, handler.CashCount);
        ScenarioExpect.Equal(10.00m, handler.Total);
    }

    [Scenario("Card Increments CardCount")]
    [Fact]
    public void Card_Increments_CardCount()
    {
        var handler = new CountersHandler();

        handler.Card(new Card("MC", "1234", 20.00m));

        ScenarioExpect.Equal(1, handler.CardCount);
        ScenarioExpect.Equal(20.00m, handler.Total);
    }

    [Scenario("Gift Increments GiftCount")]
    [Fact]
    public void Gift_Increments_GiftCount()
    {
        var handler = new CountersHandler();

        handler.Gift(new GiftCard("G-001", 5.00m));

        ScenarioExpect.Equal(1, handler.GiftCount);
        ScenarioExpect.Equal(5.00m, handler.Total);
    }

    [Scenario("Credit Increments CreditCount")]
    [Fact]
    public void Credit_Increments_CreditCount()
    {
        var handler = new CountersHandler();

        handler.Credit(new StoreCredit("C-001", 3.00m));

        ScenarioExpect.Equal(1, handler.CreditCount);
        ScenarioExpect.Equal(3.00m, handler.Total);
    }

    [Scenario("Fallback Increments FallbackCount")]
    [Fact]
    public void Fallback_Increments_FallbackCount()
    {
        var handler = new CountersHandler();
        var unknown = new Unknown("Misc", 1.50m);

        handler.Fallback(unknown);

        ScenarioExpect.Equal(1, handler.FallbackCount);
        ScenarioExpect.Equal(1.50m, handler.Total);
    }

    [Scenario("Multiple Tenders Accumulate Correctly")]
    [Fact]
    public void Multiple_Tenders_Accumulate_Correctly()
    {
        var handler = new CountersHandler();

        handler.Cash(new Cash(10.00m));
        handler.Cash(new Cash(5.00m));
        handler.Card(new Card("VISA", "1111", 20.00m));
        handler.Gift(new GiftCard("G1", 7.50m));

        ScenarioExpect.Equal(2, handler.CashCount);
        ScenarioExpect.Equal(1, handler.CardCount);
        ScenarioExpect.Equal(1, handler.GiftCount);
        ScenarioExpect.Equal(0, handler.CreditCount);
        ScenarioExpect.Equal(0, handler.FallbackCount);
        ScenarioExpect.Equal(42.50m, handler.Total);
    }
}

public sealed class RoutingTests
{
    [Scenario("CreateRouter Returns NonNull")]
    [Fact]
    public void CreateRouter_Returns_NonNull()
    {
        var handler = new CountersHandler();
        var router = Routing.CreateRouter(handler);

        ScenarioExpect.NotNull(router);
    }

    [Scenario("Router Dispatches Cash")]
    [Fact]
    public void Router_Dispatches_Cash()
    {
        var handler = new CountersHandler();
        var router = Routing.CreateRouter(handler);

        router.Dispatch(new Cash(10.00m));

        ScenarioExpect.Equal(1, handler.CashCount);
    }

    [Scenario("Router Dispatches Card")]
    [Fact]
    public void Router_Dispatches_Card()
    {
        var handler = new CountersHandler();
        var router = Routing.CreateRouter(handler);

        router.Dispatch(new Card("AMEX", "0000", 50.00m));

        ScenarioExpect.Equal(1, handler.CardCount);
    }

    [Scenario("Router Dispatches GiftCard")]
    [Fact]
    public void Router_Dispatches_GiftCard()
    {
        var handler = new CountersHandler();
        var router = Routing.CreateRouter(handler);

        router.Dispatch(new GiftCard("G2", 25.00m));

        ScenarioExpect.Equal(1, handler.GiftCount);
    }

    [Scenario("Router Dispatches StoreCredit")]
    [Fact]
    public void Router_Dispatches_StoreCredit()
    {
        var handler = new CountersHandler();
        var router = Routing.CreateRouter(handler);

        router.Dispatch(new StoreCredit("C2", 15.00m));

        ScenarioExpect.Equal(1, handler.CreditCount);
    }

    [Scenario("Router Dispatches Unknown To Fallback")]
    [Fact]
    public void Router_Dispatches_Unknown_To_Fallback()
    {
        var handler = new CountersHandler();
        var router = Routing.CreateRouter(handler);

        router.Dispatch(new Unknown("External", 5.00m));

        ScenarioExpect.Equal(1, handler.FallbackCount);
    }

    [Scenario("Router Dispatches Multiple Tenders")]
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

        ScenarioExpect.Equal(1, handler.CashCount);
        ScenarioExpect.Equal(1, handler.CardCount);
        ScenarioExpect.Equal(1, handler.GiftCount);
        ScenarioExpect.Equal(1, handler.CreditCount);
        ScenarioExpect.Equal(1, handler.FallbackCount);
        ScenarioExpect.Equal(36.00m, handler.Total);
    }
}

public sealed class DemoTests
{
    [Scenario("Run Returns Receipt And Counters")]
    [Fact]
    public void Run_Returns_Receipt_And_Counters()
    {
        var (receipt, counters) = Demo.Run();

        ScenarioExpect.NotNull(receipt);
        ScenarioExpect.NotNull(counters);
        ScenarioExpect.Equal(5, receipt.Length);
    }

    [Scenario("Run Counters Match Expected")]
    [Fact]
    public void Run_Counters_Match_Expected()
    {
        var (_, counters) = Demo.Run();

        ScenarioExpect.Equal(1, counters.CashCount);
        ScenarioExpect.Equal(1, counters.CardCount);
        ScenarioExpect.Equal(1, counters.GiftCount);
        ScenarioExpect.Equal(1, counters.CreditCount);
        ScenarioExpect.Equal(1, counters.FallbackCount);
        ScenarioExpect.Equal(36.00m, counters.Total);
    }

    [Scenario("Run Receipt Contains All Tender Types")]
    [Fact]
    public void Run_Receipt_Contains_All_Tender_Types()
    {
        var (receipt, _) = Demo.Run();

        ScenarioExpect.Contains(receipt, r => r.Contains("Cash"));
        ScenarioExpect.Contains(receipt, r => r.Contains("VISA"));
        ScenarioExpect.Contains(receipt, r => r.Contains("GiftCard"));
        ScenarioExpect.Contains(receipt, r => r.Contains("StoreCredit"));
        ScenarioExpect.Contains(receipt, r => r.Contains("Other"));
    }
}
