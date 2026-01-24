using System;
using System.Collections.Generic;
using PatternKit.Generators.Facade;

namespace PatternKit.Examples.Generators.Facade;

// ============================================================================
// EXAMPLE: Billing Facade - Complex Business Operations
// ============================================================================
// This demonstrates coordinating multiple subsystems for billing operations:
// - Tax calculation and compliance
// - Payment processing
// - Invoice generation and management
// - Receipt tracking
// ============================================================================

#region Subsystem Services

/// <summary>
/// Service for calculating taxes based on jurisdiction and product type.
/// </summary>
public class TaxService
{
    private readonly Dictionary<string, decimal> _jurisdictionRates = new()
    {
        ["US-CA"] = 0.0725m,  // California: 7.25%
        ["US-NY"] = 0.08875m, // New York: 8.875%
        ["US-TX"] = 0.0625m,  // Texas: 6.25%
        ["CA-ON"] = 0.13m,    // Ontario: 13%
        ["UK"] = 0.20m,       // UK VAT: 20%
    };

    /// <summary>
    /// Calculates tax amount for a transaction.
    /// </summary>
    public decimal CalculateTax(decimal amount, string jurisdiction)
    {
        if (_jurisdictionRates.TryGetValue(jurisdiction, out var rate))
        {
            return Math.Round(amount * rate, 2);
        }
        return 0m; // No tax for unknown jurisdictions
    }

    /// <summary>
    /// Gets the tax rate for a jurisdiction.
    /// </summary>
    public decimal GetTaxRate(string jurisdiction)
    {
        return _jurisdictionRates.TryGetValue(jurisdiction, out var rate) ? rate : 0m;
    }
}

/// <summary>
/// Service for generating and managing invoices.
/// </summary>
public class InvoiceService
{
    private readonly Dictionary<string, Invoice> _invoices = new();

    /// <summary>
    /// Creates a new invoice.
    /// </summary>
    public Invoice CreateInvoice(string customerId, decimal subtotal, decimal tax, decimal total)
    {
        var invoice = new Invoice
        {
            InvoiceNumber = $"INV-{Guid.NewGuid():N}",
            CustomerId = customerId,
            Subtotal = subtotal,
            TaxAmount = tax,
            Total = total,
            CreatedDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = "Pending"
        };

        _invoices[invoice.InvoiceNumber] = invoice;
        return invoice;
    }

    /// <summary>
    /// Updates invoice status to paid.
    /// </summary>
    public void MarkPaid(string invoiceNumber, string receiptNumber)
    {
        if (_invoices.TryGetValue(invoiceNumber, out var invoice))
        {
            invoice.Status = "Paid";
            invoice.ReceiptNumber = receiptNumber;
            invoice.PaidDate = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Gets an invoice by number.
    /// </summary>
    public Invoice? GetInvoice(string invoiceNumber)
    {
        return _invoices.TryGetValue(invoiceNumber, out var invoice) ? invoice : null;
    }
}

/// <summary>
/// Service for processing payments.
/// </summary>
public class PaymentProcessor
{
    private readonly Dictionary<string, PaymentRecord> _payments = new();

    /// <summary>
    /// Processes a payment and generates a receipt.
    /// </summary>
    public PaymentResult ProcessPayment(string customerId, decimal amount, string paymentMethod)
    {
        if (amount <= 0)
        {
            return new PaymentResult
            {
                Success = false,
                ErrorMessage = "Amount must be greater than zero"
            };
        }

        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            return new PaymentResult
            {
                Success = false,
                ErrorMessage = "Payment method is required"
            };
        }

        var receiptNumber = $"REC-{Guid.NewGuid():N}";
        var record = new PaymentRecord
        {
            ReceiptNumber = receiptNumber,
            CustomerId = customerId,
            Amount = amount,
            PaymentMethod = paymentMethod,
            ProcessedDate = DateTime.UtcNow,
            Status = "Completed"
        };

        _payments[receiptNumber] = record;

        return new PaymentResult
        {
            Success = true,
            ReceiptNumber = receiptNumber,
            ProcessedDate = record.ProcessedDate
        };
    }

    /// <summary>
    /// Refunds a payment.
    /// </summary>
    public RefundResult RefundPayment(string receiptNumber, decimal amount)
    {
        if (!_payments.TryGetValue(receiptNumber, out var payment))
        {
            return new RefundResult
            {
                Success = false,
                ErrorMessage = "Payment not found"
            };
        }

        if (amount > payment.Amount)
        {
            return new RefundResult
            {
                Success = false,
                ErrorMessage = "Refund amount exceeds original payment"
            };
        }

        var refundId = $"REF-{Guid.NewGuid():N}";
        return new RefundResult
        {
            Success = true,
            RefundId = refundId,
            RefundedAmount = amount,
            ProcessedDate = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Service for sending notifications.
/// </summary>
public class NotificationService
{
    /// <summary>
    /// Sends a payment confirmation notification.
    /// </summary>
    public void SendPaymentConfirmation(string customerId, string invoiceNumber, decimal total)
    {
        Console.WriteLine($"   📧 Sent payment confirmation to {customerId} for invoice {invoiceNumber} (${total:F2})");
    }

    /// <summary>
    /// Sends a refund notification.
    /// </summary>
    public void SendRefundNotification(string customerId, string refundId, decimal amount)
    {
        Console.WriteLine($"   📧 Sent refund notification to {customerId} for {refundId} (${amount:F2})");
    }
}

#endregion

#region Domain Models

/// <summary>
/// Represents an invoice.
/// </summary>
public class Invoice
{
    public required string InvoiceNumber { get; init; }
    public required string CustomerId { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal TaxAmount { get; init; }
    public required decimal Total { get; init; }
    public required DateTime CreatedDate { get; init; }
    public required DateTime DueDate { get; init; }
    public required string Status { get; set; }
    public string? ReceiptNumber { get; set; }
    public DateTime? PaidDate { get; set; }
}

/// <summary>
/// Represents a payment record.
/// </summary>
public class PaymentRecord
{
    public required string ReceiptNumber { get; init; }
    public required string CustomerId { get; init; }
    public required decimal Amount { get; init; }
    public required string PaymentMethod { get; init; }
    public required DateTime ProcessedDate { get; init; }
    public required string Status { get; set; }
}

/// <summary>
/// Result of a payment operation.
/// </summary>
public class PaymentResult
{
    public required bool Success { get; init; }
    public string? ReceiptNumber { get; init; }
    public DateTime? ProcessedDate { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of a refund operation.
/// </summary>
public class RefundResult
{
    public required bool Success { get; init; }
    public string? RefundId { get; init; }
    public decimal RefundedAmount { get; init; }
    public DateTime? ProcessedDate { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of a billing operation.
/// </summary>
public class BillingResult
{
    public required bool Success { get; init; }
    public string? InvoiceNumber { get; init; }
    public string? ReceiptNumber { get; init; }
    public decimal TotalAmount { get; init; }
    public string? ErrorMessage { get; init; }
}

#endregion

#region Host-First Facade

/// <summary>
/// Billing operations host - coordinates tax, invoice, payment, and notification services.
/// </summary>
/// <remarks>
/// This facade simplifies complex billing workflows by coordinating multiple subsystems:
/// - TaxService: Tax calculation
/// - InvoiceService: Invoice generation and tracking
/// - PaymentProcessor: Payment processing and refunds
/// - NotificationService: Customer notifications
/// </remarks>
[GenerateFacade(FacadeTypeName = "BillingFacade")]
public static partial class BillingHost
{
    /// <summary>
    /// Processes a complete billing transaction including tax calculation, invoice generation,
    /// payment processing, and notifications.
    /// </summary>
    /// <param name="taxService">Tax calculation service</param>
    /// <param name="invoiceService">Invoice management service</param>
    /// <param name="paymentProcessor">Payment processing service</param>
    /// <param name="notificationService">Notification service</param>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="subtotal">Subtotal amount before tax</param>
    /// <param name="jurisdiction">Tax jurisdiction code</param>
    /// <param name="paymentMethod">Payment method identifier</param>
    /// <returns>Billing result with invoice and receipt details</returns>
    [FacadeExpose]
    public static BillingResult ProcessPayment(
        TaxService taxService,
        InvoiceService invoiceService,
        PaymentProcessor paymentProcessor,
        NotificationService notificationService,
        string customerId,
        decimal subtotal,
        string jurisdiction,
        string paymentMethod)
    {
        // Step 1: Calculate tax
        var tax = taxService.CalculateTax(subtotal, jurisdiction);
        var total = subtotal + tax;

        // Step 2: Create invoice
        var invoice = invoiceService.CreateInvoice(customerId, subtotal, tax, total);

        // Step 3: Process payment
        var paymentResult = paymentProcessor.ProcessPayment(customerId, total, paymentMethod);
        if (!paymentResult.Success)
        {
            return new BillingResult
            {
                Success = false,
                ErrorMessage = paymentResult.ErrorMessage
            };
        }

        // Step 4: Mark invoice as paid
        invoiceService.MarkPaid(invoice.InvoiceNumber, paymentResult.ReceiptNumber!);

        // Step 5: Send confirmation
        notificationService.SendPaymentConfirmation(customerId, invoice.InvoiceNumber, total);

        return new BillingResult
        {
            Success = true,
            InvoiceNumber = invoice.InvoiceNumber,
            ReceiptNumber = paymentResult.ReceiptNumber,
            TotalAmount = total
        };
    }

    /// <summary>
    /// Processes a refund for a previous payment.
    /// </summary>
    /// <param name="paymentProcessor">Payment processing service</param>
    /// <param name="notificationService">Notification service</param>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="receiptNumber">Original receipt number</param>
    /// <param name="amount">Amount to refund</param>
    /// <returns>Refund result</returns>
    [FacadeExpose]
    public static RefundResult ProcessRefund(
        PaymentProcessor paymentProcessor,
        NotificationService notificationService,
        string customerId,
        string receiptNumber,
        decimal amount)
    {
        var refundResult = paymentProcessor.RefundPayment(receiptNumber, amount);
        
        if (refundResult.Success)
        {
            notificationService.SendRefundNotification(customerId, refundResult.RefundId!, amount);
        }

        return refundResult;
    }

    /// <summary>
    /// Calculates the total amount including tax for a given subtotal and jurisdiction.
    /// </summary>
    /// <param name="taxService">Tax calculation service</param>
    /// <param name="subtotal">Subtotal amount</param>
    /// <param name="jurisdiction">Tax jurisdiction</param>
    /// <returns>Total amount including tax</returns>
    [FacadeExpose]
    public static decimal CalculateTotalWithTax(
        TaxService taxService,
        decimal subtotal,
        string jurisdiction)
    {
        var tax = taxService.CalculateTax(subtotal, jurisdiction);
        return subtotal + tax;
    }

    /// <summary>
    /// Retrieves an invoice by its number.
    /// </summary>
    /// <param name="invoiceService">Invoice management service</param>
    /// <param name="invoiceNumber">Invoice number to retrieve</param>
    /// <returns>Invoice if found, null otherwise</returns>
    [FacadeExpose]
    public static Invoice? GetInvoice(
        InvoiceService invoiceService,
        string invoiceNumber)
    {
        return invoiceService.GetInvoice(invoiceNumber);
    }
}

#endregion

#region Demo Usage

/// <summary>
/// Demonstrates the billing facade coordinating multiple subsystems.
/// </summary>
public static class BillingFacadeDemo
{
    /// <summary>
    /// Runs the billing facade demonstration.
    /// </summary>
    public static void Run()
    {
        Console.WriteLine("=== Billing Facade Example ===\n");
        Console.WriteLine("Demonstrates coordinating tax, invoice, payment, and notification services.\n");

        // Create subsystem services
        var taxService = new TaxService();
        var invoiceService = new InvoiceService();
        var paymentProcessor = new PaymentProcessor();
        var notificationService = new NotificationService();

        // Create the generated facade with dependencies
        // Note: Constructor parameters are alphabetically ordered by type name
        var facade = new BillingFacade(
            invoiceService,      // InvoiceService
            notificationService, // NotificationService
            paymentProcessor,    // PaymentProcessor
            taxService          // TaxService
        );

        // Example 1: Process a payment in California
        Console.WriteLine("1. Processing payment for $100 in California...");
        var result = facade.ProcessPayment(
            customerId: "CUST-001",
            subtotal: 100m,
            jurisdiction: "US-CA",
            paymentMethod: "VISA-****1234"
        );

        if (result.Success)
        {
            Console.WriteLine($"   ✓ Payment successful!");
            Console.WriteLine($"   Invoice: {result.InvoiceNumber}");
            Console.WriteLine($"   Receipt: {result.ReceiptNumber}");
            Console.WriteLine($"   Total: ${result.TotalAmount:F2}\n");

            // Example 2: Retrieve the invoice
            Console.WriteLine("2. Retrieving invoice details...");
            var invoice = facade.GetInvoice(result.InvoiceNumber!);
            if (invoice != null)
            {
                Console.WriteLine($"   Invoice: {invoice.InvoiceNumber}");
                Console.WriteLine($"   Subtotal: ${invoice.Subtotal:F2}");
                Console.WriteLine($"   Tax: ${invoice.TaxAmount:F2}");
                Console.WriteLine($"   Total: ${invoice.Total:F2}");
                Console.WriteLine($"   Status: {invoice.Status}");
                Console.WriteLine($"   Receipt: {invoice.ReceiptNumber}\n");
            }

            // Example 3: Process a refund
            Console.WriteLine("3. Processing refund of $50...");
            var refundResult = facade.ProcessRefund(
                customerId: "CUST-001",
                receiptNumber: result.ReceiptNumber!,
                amount: 50m
            );

            if (refundResult.Success)
            {
                Console.WriteLine($"   ✓ Refund successful!");
                Console.WriteLine($"   Refund ID: {refundResult.RefundId}");
                Console.WriteLine($"   Amount: ${refundResult.RefundedAmount:F2}\n");
            }
        }

        // Example 4: Calculate total with tax (without processing payment)
        Console.WriteLine("4. Calculating total for $200 in New York...");
        var total = facade.CalculateTotalWithTax(
            subtotal: 200m,
            jurisdiction: "US-NY"
        );
        Console.WriteLine($"   Total with tax: ${total:F2}\n");

        // Example 5: Process payment in UK
        Console.WriteLine("5. Processing payment for £150 in UK...");
        var ukResult = facade.ProcessPayment(
            customerId: "CUST-002",
            subtotal: 150m,
            jurisdiction: "UK",
            paymentMethod: "MC-****5678"
        );

        if (ukResult.Success)
        {
            Console.WriteLine($"   ✓ Payment successful!");
            Console.WriteLine($"   Total with VAT: £{ukResult.TotalAmount:F2}\n");
        }

        // Example 6: Error handling
        Console.WriteLine("6. Testing error handling (invalid amount)...");
        var errorResult = facade.ProcessPayment(
            customerId: "CUST-003",
            subtotal: -50m,
            jurisdiction: "US-CA",
            paymentMethod: "VISA-****9999"
        );

        if (!errorResult.Success)
        {
            Console.WriteLine($"   ✗ Error: {errorResult.ErrorMessage}\n");
        }

        Console.WriteLine("=== Benefits Demonstrated ===");
        Console.WriteLine("✓ Complex multi-step billing workflow simplified into single method");
        Console.WriteLine("✓ Four subsystems coordinated seamlessly");
        Console.WriteLine("✓ Dependencies injected once through constructor");
        Console.WriteLine("✓ Client code is clean and doesn't need to know subsystem details");
        Console.WriteLine("✓ Easy to test and maintain");
        Console.WriteLine("✓ Consistent error handling across operations");
    }
}

#endregion
