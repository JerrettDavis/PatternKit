using PatternKit.Behavioral.Chain;
using PatternKit.Behavioral.Iterator;
using PatternKit.Behavioral.Observer;
using PatternKit.Behavioral.Strategy;
using PatternKit.Behavioral.TypeDispatcher;
using PatternKit.Creational.AbstractFactory;
using PatternKit.Creational.Factory;
using PatternKit.Creational.Prototype;
using PatternKit.Structural.Decorator;

namespace PatternKit.Examples.EnterpriseDemo;

/// <summary>
/// Comprehensive enterprise demonstration showing multiple GoF patterns working together.
/// This example implements an order processing system for an e-commerce platform.
/// </summary>
/// <remarks>
/// <para>
/// <b>Patterns demonstrated:</b>
/// <list type="bullet">
/// <item><b>Factory</b> - Creates order entities based on type</item>
/// <item><b>Abstract Factory</b> - Creates payment processor families by region</item>
/// <item><b>Prototype</b> - Clones order templates for quick creation</item>
/// <item><b>Strategy</b> - Shipping calculation strategies</item>
/// <item><b>Chain of Responsibility</b> - Order validation pipeline</item>
/// <item><b>Observer</b> - Order status change notifications</item>
/// <item><b>Iterator</b> - Order stream processing</item>
/// <item><b>Decorator</b> - Order price adjustments (discounts, taxes)</item>
/// <item><b>TypeDispatcher</b> - Order item type processing</item>
/// </list>
/// </para>
/// </remarks>
public static class EnterpriseOrderDemo
{
    // ═══════════════════════════════════════════════════════════════════════
    // DOMAIN TYPES
    // ═══════════════════════════════════════════════════════════════════════

    public enum OrderStatus { Pending, Validated, Processing, Shipped, Delivered, Cancelled }
    public enum Region { NorthAmerica, Europe, Asia }
    public enum ShippingMethod { Standard, Express, NextDay }

    public abstract record OrderItem(string Sku, string Name, decimal Price, int Quantity);
    public sealed record PhysicalItem(string Sku, string Name, decimal Price, int Quantity, double WeightKg)
        : OrderItem(Sku, Name, Price, Quantity);
    public sealed record DigitalItem(string Sku, string Name, decimal Price, int Quantity, string DownloadUrl)
        : OrderItem(Sku, Name, Price, Quantity);
    public sealed record SubscriptionItem(string Sku, string Name, decimal Price, int Quantity, int MonthsDuration)
        : OrderItem(Sku, Name, Price, Quantity);

    public sealed class Order
    {
        public required string Id { get; set; }
        public required string CustomerId { get; set; }
        public required Region Region { get; set; }
        public List<OrderItem> Items { get; set; } = [];
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public ShippingMethod ShippingMethod { get; set; } = ShippingMethod.Standard;
        public decimal Subtotal => Items.Sum(i => i.Price * i.Quantity);
        public decimal ShippingCost { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total => Subtotal + ShippingCost + Tax - Discount;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public static Order DeepClone(in Order source) => new()
        {
            Id = $"ORD-{Guid.NewGuid():N}"[..12],
            CustomerId = source.CustomerId,
            Region = source.Region,
            Items = new List<OrderItem>(source.Items),
            Status = OrderStatus.Pending,
            ShippingMethod = source.ShippingMethod,
            ShippingCost = source.ShippingCost,
            Tax = source.Tax,
            Discount = source.Discount,
            CreatedAt = DateTime.UtcNow
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PATTERN 1: FACTORY - Create order entities
    // ═══════════════════════════════════════════════════════════════════════

    public static Factory<string, OrderItem> CreateOrderItemFactory()
    {
        return Factory<string, OrderItem>.Create()
            .Map("physical", static () => new PhysicalItem("SKU-PHY", "Physical Product", 29.99m, 1, 0.5))
            .Map("digital", static () => new DigitalItem("SKU-DIG", "Digital Download", 9.99m, 1, "https://download.example.com"))
            .Map("subscription", static () => new SubscriptionItem("SKU-SUB", "Monthly Subscription", 14.99m, 1, 12))
            .Build();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PATTERN 2: ABSTRACT FACTORY - Payment processors by region
    // ═══════════════════════════════════════════════════════════════════════

    public interface IPaymentProcessor
    {
        string Name { get; }
        bool ProcessPayment(decimal amount);
    }

    public interface IFraudDetector
    {
        bool IsFraudulent(Order order);
    }

    public sealed class StripeProcessor : IPaymentProcessor
    {
        public string Name => "Stripe";
        public bool ProcessPayment(decimal amount)
        {
            Console.WriteLine($"    💳 Stripe: Processing ${amount:F2}...");
            return true;
        }
    }

    public sealed class PayPalProcessor : IPaymentProcessor
    {
        public string Name => "PayPal";
        public bool ProcessPayment(decimal amount)
        {
            Console.WriteLine($"    💳 PayPal: Processing ${amount:F2}...");
            return true;
        }
    }

    public sealed class AlipayProcessor : IPaymentProcessor
    {
        public string Name => "Alipay";
        public bool ProcessPayment(decimal amount)
        {
            Console.WriteLine($"    💳 Alipay: Processing ¥{amount * 7:F2}...");
            return true;
        }
    }

    public sealed class USFraudDetector : IFraudDetector
    {
        public bool IsFraudulent(Order order)
        {
            Console.WriteLine("    🔍 US Fraud Detection: Checking order...");
            return order.Total > 10000; // Flag high-value orders
        }
    }

    public sealed class EUFraudDetector : IFraudDetector
    {
        public bool IsFraudulent(Order order)
        {
            Console.WriteLine("    🔍 EU Fraud Detection (PSD2 compliant): Checking order...");
            return false;
        }
    }

    public sealed class AsiaFraudDetector : IFraudDetector
    {
        public bool IsFraudulent(Order order)
        {
            Console.WriteLine("    🔍 Asia Fraud Detection: Checking order...");
            return false;
        }
    }

    public static AbstractFactory<Region> CreatePaymentFactory()
    {
        return AbstractFactory<Region>.Create()
            .Family(Region.NorthAmerica)
                .Product<IPaymentProcessor>(() => new StripeProcessor())
                .Product<IFraudDetector>(() => new USFraudDetector())
            .Family(Region.Europe)
                .Product<IPaymentProcessor>(() => new PayPalProcessor())
                .Product<IFraudDetector>(() => new EUFraudDetector())
            .Family(Region.Asia)
                .Product<IPaymentProcessor>(() => new AlipayProcessor())
                .Product<IFraudDetector>(() => new AsiaFraudDetector())
            .Build();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PATTERN 3: PROTOTYPE - Order templates
    // ═══════════════════════════════════════════════════════════════════════

    public static Prototype<string, Order> CreateOrderTemplates()
    {
        var standardOrder = new Order
        {
            Id = "template-std",
            CustomerId = "TEMPLATE",
            Region = Region.NorthAmerica,
            ShippingMethod = ShippingMethod.Standard
        };

        var expressOrder = new Order
        {
            Id = "template-exp",
            CustomerId = "TEMPLATE",
            Region = Region.NorthAmerica,
            ShippingMethod = ShippingMethod.Express
        };

        return Prototype<string, Order>.Create()
            .Map("standard", standardOrder, Order.DeepClone)
            .Map("express", expressOrder, Order.DeepClone)
            .Mutate("express", o => o.ShippingCost = 15.00m)
            .Default(standardOrder, Order.DeepClone)
            .Build();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PATTERN 4: STRATEGY - Shipping cost calculation
    // ═══════════════════════════════════════════════════════════════════════

    public static Strategy<Order, decimal> CreateShippingStrategy()
    {
        return Strategy<Order, decimal>.Create()
            .When((in Order o) => o.ShippingMethod == ShippingMethod.Standard)
                .Then((in Order o) =>
                {
                    var weight = o.Items.OfType<PhysicalItem>().Sum(i => i.WeightKg * i.Quantity);
                    return (decimal)(weight * 2.5 + 5.0);
                })
            .When((in Order o) => o.ShippingMethod == ShippingMethod.Express)
                .Then((in Order o) =>
                {
                    var weight = o.Items.OfType<PhysicalItem>().Sum(i => i.WeightKg * i.Quantity);
                    return (decimal)(weight * 5.0 + 15.0);
                })
            .When((in Order o) => o.ShippingMethod == ShippingMethod.NextDay)
                .Then((in Order o) =>
                {
                    var weight = o.Items.OfType<PhysicalItem>().Sum(i => i.WeightKg * i.Quantity);
                    return (decimal)(weight * 10.0 + 30.0);
                })
            .Default((in Order _) => 5.0m) // Free shipping fallback
            .Build();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PATTERN 5: CHAIN OF RESPONSIBILITY - Order validation
    // ═══════════════════════════════════════════════════════════════════════

    public sealed record ValidationResult(bool IsValid, string? Error);

    public static ResultChain<Order, ValidationResult> CreateValidationChain()
    {
        return ResultChain<Order, ValidationResult>.Create()
            // Check for empty orders
            .When((in Order o) => o.Items.Count == 0)
                .Then(_ => new ValidationResult(false, "Order must have at least one item"))
            // Check for invalid quantities
            .When((in Order o) => o.Items.Any(i => i.Quantity <= 0))
                .Then(o => new ValidationResult(false, $"Invalid quantities found"))
            // Check total limits
            .When((in Order o) => o.Subtotal > 50000)
                .Then(_ => new ValidationResult(false, "Order exceeds maximum value of $50,000"))
            // All validations passed (Finally runs if no prior match)
            .Finally((in Order _, out ValidationResult? result, ResultChain<Order, ValidationResult>.Next _) =>
            {
                result = new ValidationResult(true, null);
                return true;
            })
            .Build();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PATTERN 6: OBSERVER - Order status notifications
    // ═══════════════════════════════════════════════════════════════════════

    public sealed record OrderStatusChange(Order Order, OrderStatus OldStatus, OrderStatus NewStatus);

    public static Observer<OrderStatusChange> CreateOrderNotifications()
    {
        var observer = Observer<OrderStatusChange>.Create().Build();

        // Email notification
        observer.Subscribe((in OrderStatusChange change) =>
        {
            Console.WriteLine($"    📧 Email: Order {change.Order.Id} status changed to {change.NewStatus}");
        });

        // SMS notification for shipped orders
        observer.Subscribe(
            (in OrderStatusChange c) => c.NewStatus == OrderStatus.Shipped,
            (in OrderStatusChange change) =>
            {
                Console.WriteLine($"    📱 SMS: Your order {change.Order.Id} has been shipped!");
            });

        // Analytics tracking
        observer.Subscribe((in OrderStatusChange change) =>
        {
            Console.WriteLine($"    📊 Analytics: Recorded status change for {change.Order.Id}");
        });

        return observer;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PATTERN 7: ITERATOR - Order stream processing
    // ═══════════════════════════════════════════════════════════════════════

    public static Flow<Order> CreateOrderProcessingPipeline(IEnumerable<Order> orders)
    {
        return Flow<Order>.From(orders)
            .Filter(o => o.Status == OrderStatus.Pending)
            .Filter(o => o.Items.Count > 0);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PATTERN 8: DECORATOR - Price adjustments
    // ═══════════════════════════════════════════════════════════════════════

    public static Decorator<Order, decimal> CreatePriceCalculator()
    {
        return Decorator<Order, decimal>.Create(static o => o.Subtotal)
            // Add shipping
            .After(static (o, price) => price + o.ShippingCost)
            // Apply regional tax
            .After(static (o, price) =>
            {
                var taxRate = o.Region switch
                {
                    Region.NorthAmerica => 0.08m,
                    Region.Europe => 0.20m,
                    Region.Asia => 0.10m,
                    _ => 0.0m
                };
                return price * (1 + taxRate);
            })
            // Apply discount
            .After(static (o, price) => price - o.Discount)
            .Build();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PATTERN 9: TYPE DISPATCHER - Process different item types
    // ═══════════════════════════════════════════════════════════════════════

    public static TypeDispatcher<OrderItem, string> CreateItemProcessor()
    {
        return TypeDispatcher<OrderItem, string>.Create()
            .On<PhysicalItem>(static item =>
                $"📦 Physical: {item.Name} ({item.WeightKg}kg) - Requires shipping")
            .On<DigitalItem>(static item =>
                $"💾 Digital: {item.Name} - Download ready at {item.DownloadUrl}")
            .On<SubscriptionItem>(static item =>
                $"🔄 Subscription: {item.Name} - {item.MonthsDuration} months")
            .Default(static item =>
                $"❓ Unknown item: {item.Name}")
            .Build();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DEMONSTRATION
    // ═══════════════════════════════════════════════════════════════════════

    public static void Run()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         ENTERPRISE ORDER PROCESSING SYSTEM DEMONSTRATION                  ║");
        Console.WriteLine("║   Combining 9 GoF Patterns in a Real-World E-Commerce Platform           ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════╝\n");

        // Initialize all pattern components
        var itemFactory = CreateOrderItemFactory();
        var paymentFactory = CreatePaymentFactory();
        var orderTemplates = CreateOrderTemplates();
        var shippingStrategy = CreateShippingStrategy();
        var validationChain = CreateValidationChain();
        var orderNotifications = CreateOrderNotifications();
        var priceCalculator = CreatePriceCalculator();
        var itemProcessor = CreateItemProcessor();

        // ── Step 1: Create orders from templates (PROTOTYPE) ──
        Console.WriteLine("▶ Step 1: Create Orders from Templates (Prototype Pattern)");
        Console.WriteLine(new string('─', 70));

        var order1 = orderTemplates.Create("standard", o =>
        {
            o.CustomerId = "CUST-001";
            o.Region = Region.NorthAmerica;
        });

        var order2 = orderTemplates.Create("express", o =>
        {
            o.CustomerId = "CUST-002";
            o.Region = Region.Europe;
        });

        Console.WriteLine($"  Created order {order1.Id} (Standard, North America)");
        Console.WriteLine($"  Created order {order2.Id} (Express, Europe)\n");

        // ── Step 2: Add items using factory (FACTORY) ──
        Console.WriteLine("▶ Step 2: Add Items Using Factory (Factory Pattern)");
        Console.WriteLine(new string('─', 70));

        order1.Items.Add(new PhysicalItem("SKU-001", "Wireless Mouse", 49.99m, 2, 0.2));
        order1.Items.Add(new PhysicalItem("SKU-002", "Mechanical Keyboard", 129.99m, 1, 1.2));
        order1.Items.Add(new DigitalItem("SKU-003", "Software License", 199.99m, 1, "https://license.example.com/abc"));

        order2.Items.Add(new SubscriptionItem("SKU-004", "Premium Membership", 9.99m, 1, 12));
        order2.Items.Add(new PhysicalItem("SKU-005", "USB-C Hub", 79.99m, 1, 0.3));

        Console.WriteLine($"  Order 1 has {order1.Items.Count} items");
        Console.WriteLine($"  Order 2 has {order2.Items.Count} items\n");

        // ── Step 3: Process items by type (TYPE DISPATCHER) ──
        Console.WriteLine("▶ Step 3: Process Items by Type (TypeDispatcher Pattern)");
        Console.WriteLine(new string('─', 70));

        foreach (var item in order1.Items)
        {
            var result = itemProcessor.Dispatch(item);
            Console.WriteLine($"  {result}");
        }
        Console.WriteLine();

        // ── Step 4: Calculate shipping (STRATEGY) ──
        Console.WriteLine("▶ Step 4: Calculate Shipping Costs (Strategy Pattern)");
        Console.WriteLine(new string('─', 70));

        order1.ShippingCost = shippingStrategy.Execute(order1);
        order2.ShippingCost = shippingStrategy.Execute(order2);

        Console.WriteLine($"  Order 1 ({order1.ShippingMethod}): ${order1.ShippingCost:F2}");
        Console.WriteLine($"  Order 2 ({order2.ShippingMethod}): ${order2.ShippingCost:F2}\n");

        // ── Step 5: Validate orders (CHAIN OF RESPONSIBILITY) ──
        Console.WriteLine("▶ Step 5: Validate Orders (Chain of Responsibility Pattern)");
        Console.WriteLine(new string('─', 70));

        validationChain.Execute(order1, out var validation1);
        validationChain.Execute(order2, out var validation2);

        Console.WriteLine($"  Order 1: {(validation1?.IsValid == true ? "✓ Valid" : $"✗ Invalid: {validation1?.Error}")}");
        Console.WriteLine($"  Order 2: {(validation2?.IsValid == true ? "✓ Valid" : $"✗ Invalid: {validation2?.Error}")}\n");

        // ── Step 6: Calculate final prices (DECORATOR) ──
        Console.WriteLine("▶ Step 6: Calculate Final Prices (Decorator Pattern)");
        Console.WriteLine(new string('─', 70));

        order1.Discount = 20.00m; // Apply a discount
        var finalPrice1 = priceCalculator.Execute(order1);
        var finalPrice2 = priceCalculator.Execute(order2);

        Console.WriteLine($"  Order 1: Subtotal=${order1.Subtotal:F2} + Shipping=${order1.ShippingCost:F2} + Tax - Discount=${order1.Discount:F2}");
        Console.WriteLine($"           Final Price = ${finalPrice1:F2}");
        Console.WriteLine($"  Order 2: Subtotal=${order2.Subtotal:F2} + Shipping=${order2.ShippingCost:F2} + Tax (20% EU)");
        Console.WriteLine($"           Final Price = ${finalPrice2:F2}\n");

        // ── Step 7: Process payment by region (ABSTRACT FACTORY) ──
        Console.WriteLine("▶ Step 7: Process Payments (Abstract Factory Pattern)");
        Console.WriteLine(new string('─', 70));

        var usServices = paymentFactory.GetFamily(Region.NorthAmerica);
        var euServices = paymentFactory.GetFamily(Region.Europe);

        var usProcessor = usServices.Create<IPaymentProcessor>();
        var usFraudCheck = usServices.Create<IFraudDetector>();

        var euProcessor = euServices.Create<IPaymentProcessor>();
        var euFraudCheck = euServices.Create<IFraudDetector>();

        Console.WriteLine($"  Processing Order 1 (US) with {usProcessor.Name}:");
        if (!usFraudCheck.IsFraudulent(order1))
        {
            usProcessor.ProcessPayment(finalPrice1);
            order1.Status = OrderStatus.Processing;
        }

        Console.WriteLine($"\n  Processing Order 2 (EU) with {euProcessor.Name}:");
        if (!euFraudCheck.IsFraudulent(order2))
        {
            euProcessor.ProcessPayment(finalPrice2);
            order2.Status = OrderStatus.Processing;
        }
        Console.WriteLine();

        // ── Step 8: Notify status changes (OBSERVER) ──
        Console.WriteLine("▶ Step 8: Send Notifications (Observer Pattern)");
        Console.WriteLine(new string('─', 70));

        orderNotifications.Publish(new OrderStatusChange(order1, OrderStatus.Pending, OrderStatus.Processing));
        Console.WriteLine();

        order1.Status = OrderStatus.Shipped;
        orderNotifications.Publish(new OrderStatusChange(order1, OrderStatus.Processing, OrderStatus.Shipped));
        Console.WriteLine();

        // ── Step 9: Process order stream (ITERATOR) ──
        Console.WriteLine("▶ Step 9: Stream Processing Pipeline (Iterator Pattern)");
        Console.WriteLine(new string('─', 70));

        var allOrders = new[] { order1, order2, orderTemplates.Create("standard") };
        var pendingOrders = CreateOrderProcessingPipeline(allOrders).ToList();

        Console.WriteLine($"  Total orders: {allOrders.Length}");
        Console.WriteLine($"  Pending orders with items: {pendingOrders.Count}");
        foreach (var order in pendingOrders)
        {
            Console.WriteLine($"    - {order.Id}: {order.Items.Count} items, ${order.Subtotal:F2}");
        }

        Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════════");
        Console.WriteLine("Patterns Working Together:");
        Console.WriteLine("  1. PROTOTYPE     → Quick order creation from templates");
        Console.WriteLine("  2. FACTORY       → Create order items by type");
        Console.WriteLine("  3. TYPEDISPATCHER→ Process items differently by concrete type");
        Console.WriteLine("  4. STRATEGY      → Flexible shipping cost calculation");
        Console.WriteLine("  5. CHAIN         → Sequential validation with early exit");
        Console.WriteLine("  6. DECORATOR     → Layer price adjustments (tax, discount, shipping)");
        Console.WriteLine("  7. ABSTRACT FAC  → Region-specific payment & fraud detection");
        Console.WriteLine("  8. OBSERVER      → Notify multiple systems of status changes");
        Console.WriteLine("  9. ITERATOR      → Filter and process order streams");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
    }
}
