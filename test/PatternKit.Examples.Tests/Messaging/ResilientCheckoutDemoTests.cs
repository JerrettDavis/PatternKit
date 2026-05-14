using PatternKit.Examples.Messaging;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class ResilientCheckoutDemoTests
{
    [Fact]
    public void Run_CompletesPrimaryCardRoute()
    {
        var services = new CheckoutServices();
        var request = CreateRequest("order-primary", total: 75m);

        var result = ResilientCheckoutDemo.Run(request, services);

        Assert.True(result.Succeeded);
        Assert.False(result.ManualReview);
        Assert.Equal("primary-card", result.FinalRoute);
        Assert.Single(result.Attempts);
        Assert.StartsWith("primarywarehouse-order-primary-", result.ReservationId, StringComparison.Ordinal);
        Assert.StartsWith("card-order-primary-", result.PaymentId, StringComparison.Ordinal);
        Assert.StartsWith("primarywarehouse-ship-order-primary-", result.ShipmentId, StringComparison.Ordinal);
        Assert.Contains("validate:ok", result.Audit);
        Assert.Empty(services.Inventory.ReleasedReservations);
        Assert.Empty(services.Payments.RefundedPayments);
    }

    [Fact]
    public void Run_RollsBackPrimaryReservationAndRetriesDropshipWhenInventoryUnavailable()
    {
        var services = new CheckoutServices
        {
            Inventory = new InventoryGateway { PrimaryAvailable = false, DropshipAvailable = true }
        };
        var request = CreateRequest("order-dropship", total: 75m);

        var result = ResilientCheckoutDemo.Run(request, services);

        Assert.True(result.Succeeded);
        Assert.Equal("dropship-card", result.FinalRoute);
        Assert.Equal(["primary-card", "dropship-card"], result.Attempts.Select(static attempt => attempt.Route));
        Assert.Equal(CheckoutFailureKind.InventoryUnavailable, result.Attempts[0].FailureKind);
        Assert.StartsWith("dropship-order-dropship-", result.ReservationId, StringComparison.Ordinal);
        Assert.Empty(services.Inventory.ReleasedReservations);
        Assert.Contains("route:primary-card:failed:InventoryUnavailable", result.Audit);
    }

    [Fact]
    public void Run_RefundsCardAndRetriesGiftCardWhenPaymentDeclines()
    {
        var services = new CheckoutServices
        {
            Payments = new PaymentGateway { CardApproved = false }
        };
        var request = CreateRequest("order-gift", total: 80m, giftCardBalance: 100m);

        var result = ResilientCheckoutDemo.Run(request, services);

        Assert.True(result.Succeeded);
        Assert.Equal("primary-gift-card", result.FinalRoute);
        Assert.Equal(["primary-card", "primary-gift-card"], result.Attempts.Select(static attempt => attempt.Route));
        Assert.Equal(CheckoutFailureKind.PaymentDeclined, result.Attempts[0].FailureKind);
        Assert.Single(services.Inventory.ReleasedReservations);
        Assert.Empty(services.Payments.RefundedPayments);
        Assert.StartsWith("giftcard-order-gift-", result.PaymentId, StringComparison.Ordinal);
        Assert.Contains(result.Audit, static entry => entry.StartsWith("inventory:released:", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_SendsFraudHoldToManualReviewWithoutSideEffects()
    {
        var services = new CheckoutServices();
        var request = CreateRequest("order-review", total: 60m) with { FraudHold = true };

        var result = ResilientCheckoutDemo.Run(request, services);

        Assert.False(result.Succeeded);
        Assert.True(result.ManualReview);
        Assert.Equal("manual-review", result.FinalRoute);
        Assert.Null(result.ReservationId);
        Assert.Null(result.PaymentId);
        Assert.Null(result.ShipmentId);
        Assert.Contains("manual-review:queued", result.Audit);
        Assert.Empty(services.Inventory.ReleasedReservations);
        Assert.Empty(services.Payments.RefundedPayments);
    }

    [Fact]
    public void Run_SendsUnrecoverableFailureToManualReview()
    {
        var services = new CheckoutServices
        {
            Inventory = new InventoryGateway { PrimaryAvailable = false, DropshipAvailable = false }
        };
        var request = CreateRequest("order-unrecoverable", total: 75m);

        var result = ResilientCheckoutDemo.Run(request, services);

        Assert.False(result.Succeeded);
        Assert.True(result.ManualReview);
        Assert.Equal("manual-review", result.FinalRoute);
        Assert.Equal(["primary-card", "dropship-card", "manual-review"], result.Attempts.Select(static attempt => attempt.Route));
    }

    private static CheckoutRequest CreateRequest(string orderId, decimal total, decimal giftCardBalance = 0m)
        => new(
            orderId,
            "customer-42",
            [new CheckoutLine("SKU-1", Quantity: 1, UnitPrice: total)],
            GiftCardBalance: giftCardBalance);
}
