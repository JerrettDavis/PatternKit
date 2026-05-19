using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class ResilientCheckoutDemoTests
{
    [Scenario("Run CompletesPrimaryCardRoute")]
    [Fact]
    public void Run_CompletesPrimaryCardRoute()
    {
        var services = new CheckoutServices();
        var request = CreateRequest("order-primary", total: 75m);

        var result = ResilientCheckoutDemo.Run(request, services);

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.False(result.ManualReview);
        ScenarioExpect.Equal("primary-card", result.FinalRoute);
        ScenarioExpect.Single(result.Attempts);
        ScenarioExpect.StartsWith("primarywarehouse-order-primary-", result.ReservationId, StringComparison.Ordinal);
        ScenarioExpect.StartsWith("card-order-primary-", result.PaymentId, StringComparison.Ordinal);
        ScenarioExpect.StartsWith("primarywarehouse-ship-order-primary-", result.ShipmentId, StringComparison.Ordinal);
        ScenarioExpect.Contains("validate:ok", result.Audit);
        ScenarioExpect.Empty(services.Inventory.ReleasedReservations);
        ScenarioExpect.Empty(services.Payments.RefundedPayments);
    }

    [Scenario("Run RollsBackPrimaryReservationAndRetriesDropshipWhenInventoryUnavailable")]
    [Fact]
    public void Run_RollsBackPrimaryReservationAndRetriesDropshipWhenInventoryUnavailable()
    {
        var services = new CheckoutServices
        {
            Inventory = new InventoryGateway { PrimaryAvailable = false, DropshipAvailable = true }
        };
        var request = CreateRequest("order-dropship", total: 75m);

        var result = ResilientCheckoutDemo.Run(request, services);

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal("dropship-card", result.FinalRoute);
        ScenarioExpect.Equal(["primary-card", "dropship-card"], result.Attempts.Select(static attempt => attempt.Route));
        ScenarioExpect.Equal(CheckoutFailureKind.InventoryUnavailable, result.Attempts[0].FailureKind);
        ScenarioExpect.StartsWith("dropship-order-dropship-", result.ReservationId, StringComparison.Ordinal);
        ScenarioExpect.Empty(services.Inventory.ReleasedReservations);
        ScenarioExpect.Contains("route:primary-card:failed:InventoryUnavailable", result.Audit);
    }

    [Scenario("Run RefundsCardAndRetriesGiftCardWhenPaymentDeclines")]
    [Fact]
    public void Run_RefundsCardAndRetriesGiftCardWhenPaymentDeclines()
    {
        var services = new CheckoutServices
        {
            Payments = new PaymentGateway { CardApproved = false }
        };
        var request = CreateRequest("order-gift", total: 80m, giftCardBalance: 100m);

        var result = ResilientCheckoutDemo.Run(request, services);

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal("primary-gift-card", result.FinalRoute);
        ScenarioExpect.Equal(["primary-card", "primary-gift-card"], result.Attempts.Select(static attempt => attempt.Route));
        ScenarioExpect.Equal(CheckoutFailureKind.PaymentDeclined, result.Attempts[0].FailureKind);
        ScenarioExpect.Single(services.Inventory.ReleasedReservations);
        ScenarioExpect.Empty(services.Payments.RefundedPayments);
        ScenarioExpect.StartsWith("giftcard-order-gift-", result.PaymentId, StringComparison.Ordinal);
        ScenarioExpect.Contains(result.Audit, static entry => entry.StartsWith("inventory:released:", StringComparison.Ordinal));
    }

    [Scenario("Run SendsFraudHoldToManualReviewWithoutSideEffects")]
    [Fact]
    public void Run_SendsFraudHoldToManualReviewWithoutSideEffects()
    {
        var services = new CheckoutServices();
        var request = CreateRequest("order-review", total: 60m) with { FraudHold = true };

        var result = ResilientCheckoutDemo.Run(request, services);

        ScenarioExpect.False(result.Succeeded);
        ScenarioExpect.True(result.ManualReview);
        ScenarioExpect.Equal("manual-review", result.FinalRoute);
        ScenarioExpect.Null(result.ReservationId);
        ScenarioExpect.Null(result.PaymentId);
        ScenarioExpect.Null(result.ShipmentId);
        ScenarioExpect.Contains("manual-review:queued", result.Audit);
        ScenarioExpect.Empty(services.Inventory.ReleasedReservations);
        ScenarioExpect.Empty(services.Payments.RefundedPayments);
    }

    [Scenario("Run SendsUnrecoverableFailureToManualReview")]
    [Fact]
    public void Run_SendsUnrecoverableFailureToManualReview()
    {
        var services = new CheckoutServices
        {
            Inventory = new InventoryGateway { PrimaryAvailable = false, DropshipAvailable = false }
        };
        var request = CreateRequest("order-unrecoverable", total: 75m);

        var result = ResilientCheckoutDemo.Run(request, services);

        ScenarioExpect.False(result.Succeeded);
        ScenarioExpect.True(result.ManualReview);
        ScenarioExpect.Equal("manual-review", result.FinalRoute);
        ScenarioExpect.Equal(["primary-card", "dropship-card", "manual-review"], result.Attempts.Select(static attempt => attempt.Route));
    }

    [Scenario("Run UsesPreferredGiftCardWhenBalanceCoversTotal")]
    [Fact]
    public void Run_UsesPreferredGiftCardWhenBalanceCoversTotal()
    {
        var services = new CheckoutServices();
        var request = CreateRequest("order-preferred-gift", total: 42m, giftCardBalance: 50m) with
        {
            PreferGiftCard = true
        };

        var result = ResilientCheckoutDemo.Run(request, services);

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.False(result.ManualReview);
        ScenarioExpect.Equal("primary-gift-card", result.FinalRoute);
        ScenarioExpect.Single(result.Attempts);
        ScenarioExpect.StartsWith("giftcard-order-preferred-gift-", result.PaymentId, StringComparison.Ordinal);
        ScenarioExpect.Empty(services.Inventory.ReleasedReservations);
        ScenarioExpect.Empty(services.Payments.RefundedPayments);
    }

    [Scenario("Run EmptyCartValidationFailureEscalatesToManualReview")]
    [Fact]
    public void Run_EmptyCartValidationFailureEscalatesToManualReview()
    {
        var services = new CheckoutServices();
        var request = new CheckoutRequest(
            "order-empty",
            "customer-42",
            []);

        var result = ResilientCheckoutDemo.Run(request, services);

        ScenarioExpect.False(result.Succeeded);
        ScenarioExpect.True(result.ManualReview);
        ScenarioExpect.Equal("manual-review", result.FinalRoute);
        ScenarioExpect.Equal(["primary-card", "manual-review"], result.Attempts.Select(static attempt => attempt.Route));
        ScenarioExpect.Equal(CheckoutFailureKind.ValidationFailed, result.Attempts[0].FailureKind);
        ScenarioExpect.Contains("cart-empty", result.Attempts[0].Message, StringComparison.Ordinal);
        ScenarioExpect.Null(result.ReservationId);
        ScenarioExpect.Null(result.PaymentId);
        ScenarioExpect.Null(result.ShipmentId);
    }

    [Scenario("Run CardDeclineWithoutGiftCardFallbackEscalatesToManualReviewAndReleasesInventory")]
    [Fact]
    public void Run_CardDeclineWithoutGiftCardFallbackEscalatesToManualReviewAndReleasesInventory()
    {
        var services = new CheckoutServices
        {
            Payments = new PaymentGateway { CardApproved = false }
        };
        var request = CreateRequest("order-card-decline-review", total: 80m, giftCardBalance: 10m);

        var result = ResilientCheckoutDemo.Run(request, services);

        ScenarioExpect.False(result.Succeeded);
        ScenarioExpect.True(result.ManualReview);
        ScenarioExpect.Equal("manual-review", result.FinalRoute);
        ScenarioExpect.Equal(["primary-card", "manual-review"], result.Attempts.Select(static attempt => attempt.Route));
        ScenarioExpect.Equal(CheckoutFailureKind.PaymentDeclined, result.Attempts[0].FailureKind);
        ScenarioExpect.Single(services.Inventory.ReleasedReservations);
        ScenarioExpect.Empty(services.Payments.RefundedPayments);
        ScenarioExpect.Contains(result.Audit, static entry => entry.StartsWith("inventory:released:", StringComparison.Ordinal));
    }

    private static CheckoutRequest CreateRequest(string orderId, decimal total, decimal giftCardBalance = 0m)
        => new(
            orderId,
            "customer-42",
            [new CheckoutLine("SKU-1", Quantity: 1, UnitPrice: total)],
            GiftCardBalance: giftCardBalance);
}
