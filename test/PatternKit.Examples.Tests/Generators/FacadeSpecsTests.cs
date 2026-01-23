using PatternKit.Examples.Generators.Facade;

namespace PatternKit.Examples.Tests.Generators;

/// <summary>
/// Integration tests for Facade pattern examples.
/// Validates that generated facades correctly coordinate subsystem operations.
/// </summary>
public class FacadeSpecsTests
{
    [Fact]
    public void ShippingFacade_CalculatesCostCorrectly()
    {
        // Arrange
        var rateCalculator = new ShippingFacadeExample.RateCalculator();
        var estimator = new ShippingFacadeExample.DeliveryEstimator();
        var validator = new ShippingFacadeExample.ShippingValidator();
        
        var facade = new ShippingFacade(rateCalculator, estimator, validator);
        var details = new ShippingFacadeExample.ShipmentDetails
        {
            Weight = 10.0m,
            Distance = 500,
            IsExpress = false
        };

        // Act
        var cost = facade.CalculateShippingCost(details);

        // Assert
        Assert.Equal(55m, cost); // (10 * 0.5) + (500 * 0.1) = 5 + 50 = 55
    }

    [Fact]
    public void ShippingFacade_ValidatesShipmentCorrectly()
    {
        // Arrange
        var rateCalculator = new ShippingFacadeExample.RateCalculator();
        var estimator = new ShippingFacadeExample.DeliveryEstimator();
        var validator = new ShippingFacadeExample.ShippingValidator();
        
        var facade = new ShippingFacade(rateCalculator, estimator, validator);
        var invalidDetails = new ShippingFacadeExample.ShipmentDetails
        {
            Weight = -5.0m, // Invalid
            Distance = 500,
            IsExpress = false
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => 
            facade.ValidateShipment(invalidDetails));
        Assert.Contains("Weight must be positive", ex.Message);
    }

    [Fact]
    public void ShippingFacade_EstimatesDeliveryCorrectly()
    {
        // Arrange
        var rateCalculator = new ShippingFacadeExample.RateCalculator();
        var estimator = new ShippingFacadeExample.DeliveryEstimator();
        var validator = new ShippingFacadeExample.ShippingValidator();
        
        var facade = new ShippingFacade(rateCalculator, estimator, validator);
        var details = new ShippingFacadeExample.ShipmentDetails
        {
            Weight = 10.0m,
            Distance = 500,
            IsExpress = true
        };

        // Act
        var days = facade.EstimateDeliveryDays(details);

        // Assert
        Assert.Equal(3, days); // 500 / 200 (express) = 2.5, rounded up = 3
    }

    [Fact]
    public void BillingFacade_ProcessesPaymentCorrectly()
    {
        // Arrange
        var tax = new BillingFacadeExample.TaxService();
        var invoice = new BillingFacadeExample.InvoiceService();
        var payment = new BillingFacadeExample.PaymentProcessor();
        var notification = new BillingFacadeExample.NotificationService();
        
        var facade = new BillingFacade(invoice, notification, payment, tax);
        var request = new BillingFacadeExample.PaymentRequest
        {
            CustomerId = "CUST001",
            Amount = 100m,
            TaxRate = 0.08m
        };

        // Act
        var receipt = facade.ProcessPayment(request);

        // Assert
        Assert.NotNull(receipt);
        Assert.Equal("CUST001", receipt.CustomerId);
        Assert.Equal(108m, receipt.AmountCharged); // 100 + 8% tax
        Assert.True(receipt.Success);
    }

    [Fact]
    public void BillingFacade_ProcessesRefundCorrectly()
    {
        // Arrange
        var tax = new BillingFacadeExample.TaxService();
        var invoice = new BillingFacadeExample.InvoiceService();
        var payment = new BillingFacadeExample.PaymentProcessor();
        var notification = new BillingFacadeExample.NotificationService();
        
        var facade = new BillingFacade(invoice, notification, payment, tax);
        
        // First process a payment
        var paymentRequest = new BillingFacadeExample.PaymentRequest
        {
            CustomerId = "CUST001",
            Amount = 100m,
            TaxRate = 0.08m
        };
        var receipt = facade.ProcessPayment(paymentRequest);

        // Then refund it
        var refundRequest = new BillingFacadeExample.RefundRequest
        {
            CustomerId = "CUST001",
            OriginalTransactionId = receipt.TransactionId,
            Amount = 108m,
            Reason = "Customer request"
        };

        // Act
        var refund = facade.ProcessRefund(refundRequest);

        // Assert
        Assert.NotNull(refund);
        Assert.Equal("CUST001", refund.CustomerId);
        Assert.Equal(108m, refund.AmountRefunded);
        Assert.True(refund.Success);
    }

    [Fact]
    public void BillingFacade_CalculatesTaxCorrectly()
    {
        // Arrange
        var tax = new BillingFacadeExample.TaxService();
        var invoice = new BillingFacadeExample.InvoiceService();
        var payment = new BillingFacadeExample.PaymentProcessor();
        var notification = new BillingFacadeExample.NotificationService();
        
        var facade = new BillingFacade(invoice, notification, payment, tax);

        // Act
        var taxAmount = facade.CalculateTax(100m, 0.08m);

        // Assert
        Assert.Equal(8m, taxAmount);
    }

    [Fact]
    public void BillingFacade_RetrievesInvoiceCorrectly()
    {
        // Arrange
        var tax = new BillingFacadeExample.TaxService();
        var invoice = new BillingFacadeExample.InvoiceService();
        var payment = new BillingFacadeExample.PaymentProcessor();
        var notification = new BillingFacadeExample.NotificationService();
        
        var facade = new BillingFacade(invoice, notification, payment, tax);
        
        // First create an invoice by processing payment
        var paymentRequest = new BillingFacadeExample.PaymentRequest
        {
            CustomerId = "CUST001",
            Amount = 100m,
            TaxRate = 0.08m
        };
        var receipt = facade.ProcessPayment(paymentRequest);

        // Act
        var retrievedInvoice = facade.GetInvoice(receipt.TransactionId);

        // Assert
        Assert.NotNull(retrievedInvoice);
        Assert.Equal(receipt.TransactionId, retrievedInvoice.InvoiceId);
        Assert.Equal("CUST001", retrievedInvoice.CustomerId);
        Assert.Equal(108m, retrievedInvoice.TotalAmount);
    }

    [Fact]
    public void ShippingDemo_ExecutesSuccessfully()
    {
        // This test validates the demo runs without errors
        // Act & Assert (should not throw)
        ShippingFacadeExample.ShippingFacadeDemo.Run();
    }

    [Fact]
    public void BillingDemo_ExecutesSuccessfully()
    {
        // This test validates the demo runs without errors
        // Act & Assert (should not throw)
        BillingFacadeExample.BillingFacadeDemo.Run();
    }
}
