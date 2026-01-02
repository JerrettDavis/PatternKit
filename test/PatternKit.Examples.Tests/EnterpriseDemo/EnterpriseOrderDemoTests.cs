using PatternKit.Examples.EnterpriseDemo;
using static PatternKit.Examples.EnterpriseDemo.EnterpriseOrderDemo;

namespace PatternKit.Examples.Tests.EnterpriseDemoTests;

public sealed class EnterpriseOrderDemoTests
{
    [Fact]
    public void CreateOrderItemFactory_Creates_Physical_Items()
    {
        var factory = CreateOrderItemFactory();

        var item = factory.Create("physical");

        Assert.NotNull(item);
        Assert.IsType<PhysicalItem>(item);
    }

    [Fact]
    public void CreateOrderItemFactory_Creates_Digital_Items()
    {
        var factory = CreateOrderItemFactory();

        var item = factory.Create("digital");

        Assert.NotNull(item);
        Assert.IsType<DigitalItem>(item);
    }

    [Fact]
    public void CreateOrderItemFactory_Creates_Subscription_Items()
    {
        var factory = CreateOrderItemFactory();

        var item = factory.Create("subscription");

        Assert.NotNull(item);
        Assert.IsType<SubscriptionItem>(item);
    }

    [Fact]
    public void CreatePaymentFactory_Creates_All_Regions()
    {
        var factory = CreatePaymentFactory();

        Assert.True(factory.TryGetFamily(Region.NorthAmerica, out _));
        Assert.True(factory.TryGetFamily(Region.Europe, out _));
        Assert.True(factory.TryGetFamily(Region.Asia, out _));
    }

    [Fact]
    public void NorthAmerica_Uses_Stripe()
    {
        var factory = CreatePaymentFactory();
        var family = factory.GetFamily(Region.NorthAmerica);

        var processor = family.Create<IPaymentProcessor>();

        Assert.Equal("Stripe", processor.Name);
        Assert.True(processor.ProcessPayment(100m));
    }

    [Fact]
    public void Europe_Uses_PayPal()
    {
        var factory = CreatePaymentFactory();
        var family = factory.GetFamily(Region.Europe);

        var processor = family.Create<IPaymentProcessor>();

        Assert.Equal("PayPal", processor.Name);
        Assert.True(processor.ProcessPayment(100m));
    }

    [Fact]
    public void Asia_Uses_Alipay()
    {
        var factory = CreatePaymentFactory();
        var family = factory.GetFamily(Region.Asia);

        var processor = family.Create<IPaymentProcessor>();

        Assert.Equal("Alipay", processor.Name);
        Assert.True(processor.ProcessPayment(100m));
    }

    [Fact]
    public void USFraudDetector_Flags_High_Value_Orders()
    {
        var detector = new USFraudDetector();
        var highValueOrder = new Order
        {
            Id = "test",
            CustomerId = "cust",
            Region = Region.NorthAmerica,
            Items = [new PhysicalItem("SKU", "Item", 15000m, 1, 1.0)]
        };

        Assert.True(detector.IsFraudulent(highValueOrder));
    }

    [Fact]
    public void USFraudDetector_Passes_Normal_Orders()
    {
        var detector = new USFraudDetector();
        var normalOrder = new Order
        {
            Id = "test",
            CustomerId = "cust",
            Region = Region.NorthAmerica,
            Items = [new PhysicalItem("SKU", "Item", 100m, 1, 1.0)]
        };

        Assert.False(detector.IsFraudulent(normalOrder));
    }

    [Fact]
    public void EUFraudDetector_Always_Passes()
    {
        var detector = new EUFraudDetector();
        var order = new Order
        {
            Id = "test",
            CustomerId = "cust",
            Region = Region.Europe
        };

        Assert.False(detector.IsFraudulent(order));
    }

    [Fact]
    public void CreateOrderTemplates_Creates_Standard_Order()
    {
        var templates = CreateOrderTemplates();

        var order = templates.Create("standard");

        Assert.NotNull(order);
        Assert.Equal(ShippingMethod.Standard, order.ShippingMethod);
    }

    [Fact]
    public void CreateOrderTemplates_Creates_Express_Order()
    {
        var templates = CreateOrderTemplates();

        var order = templates.Create("express");

        Assert.NotNull(order);
        Assert.Equal(ShippingMethod.Express, order.ShippingMethod);
        Assert.Equal(15.00m, order.ShippingCost);
    }

    [Fact]
    public void CreateOrderTemplates_With_Mutation()
    {
        var templates = CreateOrderTemplates();

        var order = templates.Create("standard", o => o.CustomerId = "CUSTOM-123");

        Assert.Equal("CUSTOM-123", order.CustomerId);
    }

    [Fact]
    public void CreateShippingStrategy_Standard_Shipping()
    {
        var strategy = CreateShippingStrategy();
        var order = new Order
        {
            Id = "test",
            CustomerId = "cust",
            Region = Region.NorthAmerica,
            ShippingMethod = ShippingMethod.Standard,
            Items = [new PhysicalItem("SKU", "Item", 50m, 1, 2.0)]
        };

        var cost = strategy.Execute(order);

        // (2.0 * 2.5 + 5.0) = 10.0
        Assert.Equal(10.0m, cost);
    }

    [Fact]
    public void CreateShippingStrategy_Express_Shipping()
    {
        var strategy = CreateShippingStrategy();
        var order = new Order
        {
            Id = "test",
            CustomerId = "cust",
            Region = Region.NorthAmerica,
            ShippingMethod = ShippingMethod.Express,
            Items = [new PhysicalItem("SKU", "Item", 50m, 1, 2.0)]
        };

        var cost = strategy.Execute(order);

        // (2.0 * 5.0 + 15.0) = 25.0
        Assert.Equal(25.0m, cost);
    }

    [Fact]
    public void CreateShippingStrategy_NextDay_Shipping()
    {
        var strategy = CreateShippingStrategy();
        var order = new Order
        {
            Id = "test",
            CustomerId = "cust",
            Region = Region.NorthAmerica,
            ShippingMethod = ShippingMethod.NextDay,
            Items = [new PhysicalItem("SKU", "Item", 50m, 1, 2.0)]
        };

        var cost = strategy.Execute(order);

        // (2.0 * 10.0 + 30.0) = 50.0
        Assert.Equal(50.0m, cost);
    }

    [Fact]
    public void CreateValidationChain_Fails_Empty_Order()
    {
        var chain = CreateValidationChain();
        var order = new Order
        {
            Id = "test",
            CustomerId = "cust",
            Region = Region.NorthAmerica
        };

        chain.Execute(order, out var result);

        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Contains("at least one item", result.Error);
    }

    [Fact]
    public void CreateValidationChain_Fails_Invalid_Quantity()
    {
        var chain = CreateValidationChain();
        var order = new Order
        {
            Id = "test",
            CustomerId = "cust",
            Region = Region.NorthAmerica,
            Items = [new PhysicalItem("SKU", "Item", 50m, 0, 1.0)]
        };

        chain.Execute(order, out var result);

        Assert.NotNull(result);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateValidationChain_Fails_High_Value()
    {
        var chain = CreateValidationChain();
        var order = new Order
        {
            Id = "test",
            CustomerId = "cust",
            Region = Region.NorthAmerica,
            Items = [new PhysicalItem("SKU", "Item", 60000m, 1, 1.0)]
        };

        chain.Execute(order, out var result);

        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Contains("exceeds maximum", result.Error);
    }

    [Fact]
    public void CreateValidationChain_Passes_Valid_Order()
    {
        var chain = CreateValidationChain();
        var order = new Order
        {
            Id = "test",
            CustomerId = "cust",
            Region = Region.NorthAmerica,
            Items = [new PhysicalItem("SKU", "Item", 50m, 1, 1.0)]
        };

        chain.Execute(order, out var result);

        Assert.NotNull(result);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateOrderNotifications_Creates_Observer()
    {
        var observer = CreateOrderNotifications();

        Assert.NotNull(observer);
        Assert.True(observer.SubscriberCount > 0);
    }

    [Fact]
    public void CreateOrderProcessingPipeline_Filters_Pending_Orders()
    {
        var orders = new[]
        {
            new Order { Id = "1", CustomerId = "c", Region = Region.NorthAmerica, Status = OrderStatus.Pending, Items = [new PhysicalItem("SKU", "Item", 50m, 1, 1.0)] },
            new Order { Id = "2", CustomerId = "c", Region = Region.NorthAmerica, Status = OrderStatus.Shipped, Items = [new PhysicalItem("SKU", "Item", 50m, 1, 1.0)] },
            new Order { Id = "3", CustomerId = "c", Region = Region.NorthAmerica, Status = OrderStatus.Pending, Items = [] }
        };

        var pipeline = CreateOrderProcessingPipeline(orders);
        var result = pipeline.ToList();

        Assert.Single(result);
        Assert.Equal("1", result[0].Id);
    }

    [Fact]
    public void CreatePriceCalculator_Applies_Tax_And_Discount()
    {
        var calculator = CreatePriceCalculator();
        var order = new Order
        {
            Id = "test",
            CustomerId = "cust",
            Region = Region.NorthAmerica,
            Items = [new PhysicalItem("SKU", "Item", 100m, 1, 1.0)],
            ShippingCost = 10m,
            Discount = 5m
        };

        var total = calculator.Execute(order);

        // Verify price calculation includes tax and discount
        Assert.True(total > order.Subtotal);
        Assert.True(total < order.Subtotal * 2);
    }

    [Fact]
    public void CreateItemProcessor_Dispatches_Physical_Items()
    {
        var processor = CreateItemProcessor();
        var item = new PhysicalItem("SKU", "Mouse", 50m, 1, 0.5);

        var result = processor.Dispatch(item);

        Assert.Contains("Physical", result);
        Assert.Contains("Mouse", result);
        Assert.Contains("0.5kg", result);
    }

    [Fact]
    public void CreateItemProcessor_Dispatches_Digital_Items()
    {
        var processor = CreateItemProcessor();
        var item = new DigitalItem("SKU", "Software", 100m, 1, "https://download.com");

        var result = processor.Dispatch(item);

        Assert.Contains("Digital", result);
        Assert.Contains("Software", result);
    }

    [Fact]
    public void CreateItemProcessor_Dispatches_Subscription_Items()
    {
        var processor = CreateItemProcessor();
        var item = new SubscriptionItem("SKU", "Premium", 10m, 1, 12);

        var result = processor.Dispatch(item);

        Assert.Contains("Subscription", result);
        Assert.Contains("12 months", result);
    }

    [Fact]
    public void Order_DeepClone_Creates_Independent_Copy()
    {
        var original = new Order
        {
            Id = "original",
            CustomerId = "cust",
            Region = Region.NorthAmerica,
            Items = [new PhysicalItem("SKU", "Item", 50m, 1, 1.0)]
        };

        var clone = Order.DeepClone(original);

        Assert.NotSame(original, clone);
        Assert.NotEqual(original.Id, clone.Id);
        Assert.NotSame(original.Items, clone.Items);
    }

    [Fact]
    public void Run_Executes_Without_Errors()
    {
        PatternKit.Examples.EnterpriseDemo.EnterpriseOrderDemo.Run();
    }
}
