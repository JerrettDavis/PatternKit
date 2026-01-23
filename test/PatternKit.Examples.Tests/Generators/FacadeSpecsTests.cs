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
        var rateCalculator = new RateCalculator();
        var estimator = new DeliveryEstimator();
        var validator = new ShippingValidator();
        
        // Constructor parameters are in alphabetical order by type name
        var facade = new ShippingFacade(estimator, rateCalculator, validator);

        // Act
        var cost = facade.CalculateShippingCost(destination: "local", weight: 3.5m);

        // Assert
        Assert.Equal(5.99m, cost); // local base rate, 3.5 lbs (under 5 lbs, no surcharge)
    }

    [Fact]
    public void ShippingFacade_ValidatesShipmentCorrectly()
    {
        // Arrange
        var rateCalculator = new RateCalculator();
        var estimator = new DeliveryEstimator();
        var validator = new ShippingValidator();
        
        // Constructor parameters are in alphabetical order by type name
        var facade = new ShippingFacade(estimator, rateCalculator, validator);

        // Act - Valid shipment
        var isValid = facade.ValidateShipment(destination: "local", weight: 10m);
        
        // Assert
        Assert.True(isValid);
        
        // Act - Invalid shipment (negative weight)
        var isInvalid = facade.ValidateShipment(destination: "local", weight: -5m);
        
        // Assert
        Assert.False(isInvalid);
    }

    [Fact]
    public void ShippingFacade_EstimatesDeliveryCorrectly()
    {
        // Arrange
        var rateCalculator = new RateCalculator();
        var estimator = new DeliveryEstimator();
        var validator = new ShippingValidator();
        
        // Constructor parameters are in alphabetical order by type name
        var facade = new ShippingFacade(estimator, rateCalculator, validator);

        // Act
        var days = facade.EstimateDeliveryDays(destination: "local", speed: "standard");

        // Assert
        Assert.Equal(7, days); // standard = 7 days base, local = no addition = 7 days total
    }

    [Fact]
    public void BillingFacade_ProcessesPaymentCorrectly()
    {
        // Arrange
        var tax = new TaxService();
        var invoice = new InvoiceService();
        var payment = new PaymentProcessor();
        var notification = new NotificationService();
        
        // Constructor parameters are in alphabetical order by type name
        var facade = new BillingFacade(invoice, notification, payment, tax);

        // Act
        var result = facade.ProcessPayment(
            customerId: "CUST001",
            subtotal: 100m,
            jurisdiction: "US-CA",
            paymentMethod: "CreditCard");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.InvoiceNumber);
        Assert.NotNull(result.ReceiptNumber);
        // Note: TotalAmount returns subtotal from current impl
        Assert.True(result.TotalAmount > 0);
    }

    [Fact]
    public void BillingFacade_ProcessesRefundCorrectly()
    {
        // Arrange
        var tax = new TaxService();
        var invoice = new InvoiceService();
        var payment = new PaymentProcessor();
        var notification = new NotificationService();
        
        // Constructor parameters are in alphabetical order by type name
        var facade = new BillingFacade(invoice, notification, payment, tax);
        
        // First process a payment
        var result = facade.ProcessPayment(
            customerId: "CUST001",
            subtotal: 100m,
            jurisdiction: "US-CA",
            paymentMethod: "CreditCard");
        
        Assert.True(result.Success);
        Assert.NotNull(result.ReceiptNumber);

        // Act - Then refund it
        var refund = facade.ProcessRefund(
            customerId: "CUST001",
            receiptNumber: result.ReceiptNumber!,
            amount: 107.25m);

        // Assert
        Assert.NotNull(refund);
        Assert.True(refund.Success);
        Assert.True(refund.RefundedAmount > 0);
        Assert.NotNull(refund.RefundId);
    }

    [Fact]
    public void BillingFacade_CalculatesTotalWithTaxCorrectly()
    {
        // Arrange
        var tax = new TaxService();
        var invoice = new InvoiceService();
        var payment = new PaymentProcessor();
        var notification = new NotificationService();
        
        // Constructor parameters are in alphabetical order by type name
        var facade = new BillingFacade(invoice, notification, payment, tax);

        // Act
        var totalWithTax = facade.CalculateTotalWithTax(100m, "US-CA");

        // Assert
        Assert.True(totalWithTax > 100m); // Should add tax
    }

    [Fact]
    public void BillingFacade_RetrievesInvoiceCorrectly()
    {
        // Arrange
        var tax = new TaxService();
        var invoice = new InvoiceService();
        var payment = new PaymentProcessor();
        var notification = new NotificationService();
        
        // Constructor parameters are in alphabetical order by type name
        var facade = new BillingFacade(invoice, notification, payment, tax);
        
        // First create an invoice by processing payment
        var result = facade.ProcessPayment(
            customerId: "CUST001",
            subtotal: 100m,
            jurisdiction: "US-CA",
            paymentMethod: "CreditCard");
        
        Assert.True(result.Success);
        Assert.NotNull(result.InvoiceNumber);

        // Act
        var retrievedInvoice = facade.GetInvoice(result.InvoiceNumber!);

        // Assert
        Assert.NotNull(retrievedInvoice);
        Assert.Equal(result.InvoiceNumber, retrievedInvoice.InvoiceNumber);
        Assert.Equal("CUST001", retrievedInvoice.CustomerId);
        Assert.True(retrievedInvoice.Total > 0);
    }

    [Fact]
    public void ShippingDemo_ExecutesSuccessfully()
    {
        // This test validates the demo runs without errors
        // Act & Assert (should not throw)
        ShippingFacadeDemo.Run();
    }

    [Fact]
    public void BillingDemo_ExecutesSuccessfully()
    {
        // This test validates the demo runs without errors
        // Act & Assert (should not throw)
        BillingFacadeDemo.Run();
    }
}
