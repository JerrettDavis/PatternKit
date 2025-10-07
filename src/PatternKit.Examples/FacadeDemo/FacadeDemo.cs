using PatternKit.Structural.Facade;

namespace PatternKit.Examples.FacadeDemo;

/// <summary>
/// Demonstrates the Facade pattern by providing a simplified interface to a complex e-commerce order processing subsystem.
/// </summary>
public static class FacadeDemo
{
    // Simulated subsystem services
    public sealed class InventoryService
    {
        private readonly Dictionary<string, int> _stock = new()
        {
            ["WIDGET-001"] = 100,
            ["GADGET-002"] = 50,
            ["DEVICE-003"] = 25
        };

        public bool Reserve(string productId, int quantity, out string reservationId)
        {
            if (!_stock.TryGetValue(productId, out var available) || available < quantity)
            {
                reservationId = string.Empty;
                return false;
            }

            _stock[productId] -= quantity;
            reservationId = $"RES-{Guid.NewGuid():N}";
            return true;
        }

        public void Release(string reservationId) => Console.WriteLine($"Released reservation: {reservationId}");
        
        public void Restock(string productId, int quantity)
        {
            if (_stock.ContainsKey(productId))
                _stock[productId] += quantity;
        }
    }

    public sealed class PaymentService
    {
        public static bool Charge(string paymentMethod, decimal amount, out string transactionId)
        {
            if (amount <= 0)
            {
                transactionId = string.Empty;
                return false;
            }

            transactionId = $"TX-{Guid.NewGuid():N}";
            Console.WriteLine($"Charged ${amount:F2} to {paymentMethod}");
            return true;
        }

        public static void Refund(string transactionId) => Console.WriteLine($"Refunded transaction: {transactionId}");
        
        public static void Void(string transactionId) => Console.WriteLine($"Voided transaction: {transactionId}");
    }

    public sealed class ShippingService
    {
        public static string Schedule(string address, string productId, int quantity)
        {
            var shipmentId = $"SHIP-{Guid.NewGuid():N}";
            Console.WriteLine($"Scheduled shipment {shipmentId} to {address}");
            return shipmentId;
        }

        public static void Cancel(string shipmentId) => Console.WriteLine($"Cancelled shipment: {shipmentId}");
        
        public static string InitiateReturn(string shipmentId)
        {
            var returnId = $"RET-{shipmentId[5..]}";
            Console.WriteLine($"Initiated return: {returnId}");
            return returnId;
        }
    }

    public sealed class NotificationService
    {
        public static void SendOrderConfirmation(string email, string orderId) 
            => Console.WriteLine($"Sent confirmation to {email} for order {orderId}");
        
        public static void SendCancellation(string email, string orderId) 
            => Console.WriteLine($"Sent cancellation notice to {email} for order {orderId}");
        
        public static void SendRefundNotice(string email, decimal amount) 
            => Console.WriteLine($"Sent refund notice to {email} for ${amount:F2}");
    }

    // Request/Result DTOs
    public sealed record OrderRequest(
        string ProductId,
        int Quantity,
        string CustomerEmail,
        string ShippingAddress,
        string PaymentMethod,
        decimal Price);

    public sealed record OrderResult(
        bool Success,
        string? OrderId = null,
        string? ErrorMessage = null,
        string? TransactionId = null,
        string? ShipmentId = null);

    /// <summary>
    /// Facade that simplifies complex order processing workflow
    /// </summary>
    public sealed class OrderProcessingFacade(
        InventoryService inventory,
        PaymentService payment,
        ShippingService shipping,
        NotificationService notification
    )
    {
        private readonly Dictionary<string, (string reservationId, string transactionId, string shipmentId)> _orders = new();

        public Facade<OrderRequest, OrderResult> BuildFacade()
        {
            return Facade<OrderRequest, OrderResult>.Create()
                .Operation("place-order", PlaceOrder)
                .Operation("cancel-order", CancelOrder)
                .Operation("process-return", ProcessReturn)
                .Default((in OrderRequest _) => 
                    new OrderResult(false, ErrorMessage: "Unknown operation"))
                .Build();
        }

        private OrderResult PlaceOrder(in OrderRequest request)
        {
            Console.WriteLine($"\n=== Placing Order ===");
            Console.WriteLine($"Product: {request.ProductId}, Qty: {request.Quantity}");

            // Step 1: Reserve inventory
            if (!inventory.Reserve(request.ProductId, request.Quantity, out var reservationId))
            {
                return new OrderResult(false, ErrorMessage: "Insufficient inventory");
            }

            // Step 2: Process payment
            var total = request.Price * request.Quantity;
            if (!PaymentService.Charge(request.PaymentMethod, total, out var transactionId))
            {
                inventory.Release(reservationId);
                return new OrderResult(false, ErrorMessage: "Payment failed");
            }

            // Step 3: Schedule shipping
            var shipmentId = ShippingService.Schedule(request.ShippingAddress, request.ProductId, request.Quantity);

            // Step 4: Generate order ID and store
            var orderId = $"ORD-{Guid.NewGuid():N}";
            _orders[orderId] = (reservationId, transactionId, shipmentId);

            // Step 5: Send confirmation
            NotificationService.SendOrderConfirmation(request.CustomerEmail, orderId);

            Console.WriteLine($"Order {orderId} placed successfully!\n");

            return new OrderResult(
                Success: true,
                OrderId: orderId,
                TransactionId: transactionId,
                ShipmentId: shipmentId);
        }

        private OrderResult CancelOrder(in OrderRequest request)
        {
            Console.WriteLine($"\n=== Cancelling Order ===");
            
            if (string.IsNullOrEmpty(request.ProductId) || !_orders.TryGetValue(request.ProductId, out var orderData))
            {
                return new OrderResult(false, ErrorMessage: "Order not found");
            }

            // Step 1: Cancel shipment
            ShippingService.Cancel(orderData.shipmentId);

            // Step 2: Void payment
            PaymentService.Void(orderData.transactionId);

            // Step 3: Release inventory
            inventory.Release(orderData.reservationId);

            // Step 4: Send notification
            NotificationService.SendCancellation(request.CustomerEmail, request.ProductId);

            // Step 5: Remove order
            _orders.Remove(request.ProductId);

            Console.WriteLine($"Order {request.ProductId} cancelled successfully!\n");

            return new OrderResult(Success: true, OrderId: request.ProductId);
        }

        private OrderResult ProcessReturn(in OrderRequest request)
        {
            Console.WriteLine($"\n=== Processing Return ===");
            
            if (string.IsNullOrEmpty(request.ProductId) || !_orders.TryGetValue(request.ProductId, out var orderData))
            {
                return new OrderResult(false, ErrorMessage: "Order not found");
            }

            // Step 1: Initiate return shipment
            var returnId = ShippingService.InitiateReturn(orderData.shipmentId);

            // Step 2: Process refund
            var refundAmount = request.Price * request.Quantity;
            PaymentService.Refund(orderData.transactionId);

            // Step 3: Restock inventory
            inventory.Restock(request.ProductId, request.Quantity);

            // Step 4: Send refund notice
            NotificationService.SendRefundNotice(request.CustomerEmail, refundAmount);

            // Step 5: Remove order
            _orders.Remove(request.ProductId);

            Console.WriteLine($"Return processed successfully!\n");

            return new OrderResult(Success: true, OrderId: request.ProductId);
        }
    }

    /// <summary>
    /// Demonstrates the facade pattern simplifying complex order operations
    /// </summary>
    public static void Run()
    {
        Console.WriteLine("=== Facade Pattern Demo: E-Commerce Order Processing ===\n");

        // Create subsystem services
        var inventory = new InventoryService();
        var payment = new PaymentService();
        var shipping = new ShippingService();
        var notification = new NotificationService();

        // Create facade
        var orderProcessor = new OrderProcessingFacade(inventory, payment, shipping, notification);
        var facade = orderProcessor.BuildFacade();

        // Example 1: Place an order (complex operation simplified)
        var orderRequest = new OrderRequest(
            ProductId: "WIDGET-001",
            Quantity: 5,
            CustomerEmail: "customer@example.com",
            ShippingAddress: "123 Main St, Springfield",
            PaymentMethod: "VISA-****1234",
            Price: 29.99m);

        var result = facade.Execute("place-order", orderRequest);
        
        if (result.Success)
        {
            Console.WriteLine($"✓ Order placed: {result.OrderId}");
            Console.WriteLine($"  Transaction: {result.TransactionId}");
            Console.WriteLine($"  Shipment: {result.ShipmentId}");

            // Example 2: Cancel the order
            var cancelRequest = orderRequest with { ProductId = result.OrderId };
            var cancelResult = facade.Execute("cancel-order", cancelRequest);
            
            if (cancelResult.Success)
            {
                Console.WriteLine($"✓ Order cancelled: {cancelResult.OrderId}");
            }
        }

        // Example 3: Place another order and process return
        var order2 = new OrderRequest(
            ProductId: "GADGET-002",
            Quantity: 2,
            CustomerEmail: "another@example.com",
            ShippingAddress: "456 Oak Ave, Shelbyville",
            PaymentMethod: "MC-****5678",
            Price: 49.99m);

        var result2 = facade.Execute("place-order", order2);
        
        if (result2.Success)
        {
            Console.WriteLine($"✓ Second order placed: {result2.OrderId}");

            // Process return
            var returnRequest = order2 with { ProductId = result2.OrderId };
            var returnResult = facade.Execute("process-return", returnRequest);
            
            if (returnResult.Success)
            {
                Console.WriteLine($"✓ Return processed: {returnResult.OrderId}");
            }
        }

        // Example 4: Try unknown operation with default fallback
        var unknownResult = facade.Execute("unknown-operation", orderRequest);
        Console.WriteLine($"\n✗ Unknown operation handled: {unknownResult.ErrorMessage}");

        // Example 5: Using TryExecute
        Console.WriteLine("\n=== Using TryExecute ===");
        if (facade.TryExecute("place-order", orderRequest, out var tryResult))
        {
            Console.WriteLine($"✓ TryExecute succeeded: {tryResult.Success}");
        }

        Console.WriteLine("\n=== Benefits Demonstrated ===");
        Console.WriteLine("1. Complex multi-step workflow hidden behind simple 'place-order' operation");
        Console.WriteLine("2. Subsystem coordination (inventory, payment, shipping, notification) abstracted");
        Console.WriteLine("3. Error handling and rollback logic encapsulated");
        Console.WriteLine("4. Client code is clean and doesn't need to know subsystem details");
        Console.WriteLine("5. Reusable facade can be used across the application");
    }
}

