using PatternKit.Examples.EnterpriseDemo;
using TinyBDD;
using static PatternKit.Examples.EnterpriseDemo.EnterpriseOrderDemo;

namespace PatternKit.Examples.Tests.EnterpriseDemoTests;

public sealed class EnterpriseOrderDemoTests
{
    [Scenario("CreateOrderItemFactory Creates Physical Items")]
    [Fact]
    public void CreateOrderItemFactory_Creates_Physical_Items()
    {
        var factory = CreateOrderItemFactory();

        var item = factory.Create("physical");

        ScenarioExpect.NotNull(item);
        ScenarioExpect.IsType<PhysicalItem>(item);
    }

    [Scenario("CreateOrderItemFactory Creates Digital Items")]
    [Fact]
    public void CreateOrderItemFactory_Creates_Digital_Items()
    {
        var factory = CreateOrderItemFactory();

        var item = factory.Create("digital");

        ScenarioExpect.NotNull(item);
        ScenarioExpect.IsType<DigitalItem>(item);
    }

    [Scenario("CreateOrderItemFactory Creates Subscription Items")]
    [Fact]
    public void CreateOrderItemFactory_Creates_Subscription_Items()
    {
        var factory = CreateOrderItemFactory();

        var item = factory.Create("subscription");

        ScenarioExpect.NotNull(item);
        ScenarioExpect.IsType<SubscriptionItem>(item);
    }

    [Scenario("CreatePaymentFactory Creates All Regions")]
    [Fact]
    public void CreatePaymentFactory_Creates_All_Regions()
    {
        var factory = CreatePaymentFactory();

        ScenarioExpect.True(factory.TryGetFamily(Region.NorthAmerica, out _));
        ScenarioExpect.True(factory.TryGetFamily(Region.Europe, out _));
        ScenarioExpect.True(factory.TryGetFamily(Region.Asia, out _));
    }

    [Scenario("NorthAmerica Uses Stripe")]
    [Fact]
    public void NorthAmerica_Uses_Stripe()
    {
        var factory = CreatePaymentFactory();
        var family = factory.GetFamily(Region.NorthAmerica);

        var processor = family.Create<IPaymentProcessor>();

        ScenarioExpect.Equal("Stripe", processor.Name);
        ScenarioExpect.True(processor.ProcessPayment(100m));
    }

    [Scenario("Europe Uses PayPal")]
    [Fact]
    public void Europe_Uses_PayPal()
    {
        var factory = CreatePaymentFactory();
        var family = factory.GetFamily(Region.Europe);

        var processor = family.Create<IPaymentProcessor>();

        ScenarioExpect.Equal("PayPal", processor.Name);
        ScenarioExpect.True(processor.ProcessPayment(100m));
    }

    [Scenario("Asia Uses Alipay")]
    [Fact]
    public void Asia_Uses_Alipay()
    {
        var factory = CreatePaymentFactory();
        var family = factory.GetFamily(Region.Asia);

        var processor = family.Create<IPaymentProcessor>();

        ScenarioExpect.Equal("Alipay", processor.Name);
        ScenarioExpect.True(processor.ProcessPayment(100m));
    }

    [Scenario("USFraudDetector Flags High Value Orders")]
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

        ScenarioExpect.True(detector.IsFraudulent(highValueOrder));
    }

    [Scenario("USFraudDetector Passes Normal Orders")]
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

        ScenarioExpect.False(detector.IsFraudulent(normalOrder));
    }

    [Scenario("EUFraudDetector Always Passes")]
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

        ScenarioExpect.False(detector.IsFraudulent(order));
    }

    [Scenario("CreateOrderTemplates Creates Standard Order")]
    [Fact]
    public void CreateOrderTemplates_Creates_Standard_Order()
    {
        var templates = CreateOrderTemplates();

        var order = templates.Create("standard");

        ScenarioExpect.NotNull(order);
        ScenarioExpect.Equal(ShippingMethod.Standard, order.ShippingMethod);
    }

    [Scenario("CreateOrderTemplates Creates Express Order")]
    [Fact]
    public void CreateOrderTemplates_Creates_Express_Order()
    {
        var templates = CreateOrderTemplates();

        var order = templates.Create("express");

        ScenarioExpect.NotNull(order);
        ScenarioExpect.Equal(ShippingMethod.Express, order.ShippingMethod);
        ScenarioExpect.Equal(15.00m, order.ShippingCost);
    }

    [Scenario("CreateOrderTemplates With Mutation")]
    [Fact]
    public void CreateOrderTemplates_With_Mutation()
    {
        var templates = CreateOrderTemplates();

        var order = templates.Create("standard", o => o.CustomerId = "CUSTOM-123");

        ScenarioExpect.Equal("CUSTOM-123", order.CustomerId);
    }

    [Scenario("CreateShippingStrategy Standard Shipping")]
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
        ScenarioExpect.Equal(10.0m, cost);
    }

    [Scenario("CreateShippingStrategy Express Shipping")]
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
        ScenarioExpect.Equal(25.0m, cost);
    }

    [Scenario("CreateShippingStrategy NextDay Shipping")]
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
        ScenarioExpect.Equal(50.0m, cost);
    }

    [Scenario("CreateValidationChain Fails Empty Order")]
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

        ScenarioExpect.NotNull(result);
        ScenarioExpect.False(result.IsValid);
        ScenarioExpect.Contains("at least one item", result.Error);
    }

    [Scenario("CreateValidationChain Fails Invalid Quantity")]
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

        ScenarioExpect.NotNull(result);
        ScenarioExpect.False(result.IsValid);
    }

    [Scenario("CreateValidationChain Fails High Value")]
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

        ScenarioExpect.NotNull(result);
        ScenarioExpect.False(result.IsValid);
        ScenarioExpect.Contains("exceeds maximum", result.Error);
    }

    [Scenario("CreateValidationChain Passes Valid Order")]
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

        ScenarioExpect.NotNull(result);
        ScenarioExpect.True(result.IsValid);
    }

    [Scenario("CreateOrderNotifications Creates Observer")]
    [Fact]
    public void CreateOrderNotifications_Creates_Observer()
    {
        var observer = CreateOrderNotifications();

        ScenarioExpect.NotNull(observer);
        ScenarioExpect.True(observer.SubscriberCount > 0);
    }

    [Scenario("CreateOrderProcessingPipeline Filters Pending Orders")]
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

        ScenarioExpect.Single(result);
        ScenarioExpect.Equal("1", result[0].Id);
    }

    [Scenario("CreatePriceCalculator Applies Tax And Discount")]
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
        ScenarioExpect.True(total > order.Subtotal);
        ScenarioExpect.True(total < order.Subtotal * 2);
    }

    [Scenario("CreateItemProcessor Dispatches Physical Items")]
    [Fact]
    public void CreateItemProcessor_Dispatches_Physical_Items()
    {
        var processor = CreateItemProcessor();
        var item = new PhysicalItem("SKU", "Mouse", 50m, 1, 0.5);

        var result = processor.Dispatch(item);

        ScenarioExpect.Contains("Physical", result);
        ScenarioExpect.Contains("Mouse", result);
        ScenarioExpect.Contains("0.5kg", result);
    }

    [Scenario("CreateItemProcessor Dispatches Digital Items")]
    [Fact]
    public void CreateItemProcessor_Dispatches_Digital_Items()
    {
        var processor = CreateItemProcessor();
        var item = new DigitalItem("SKU", "Software", 100m, 1, "https://download.com");

        var result = processor.Dispatch(item);

        ScenarioExpect.Contains("Digital", result);
        ScenarioExpect.Contains("Software", result);
    }

    [Scenario("CreateItemProcessor Dispatches Subscription Items")]
    [Fact]
    public void CreateItemProcessor_Dispatches_Subscription_Items()
    {
        var processor = CreateItemProcessor();
        var item = new SubscriptionItem("SKU", "Premium", 10m, 1, 12);

        var result = processor.Dispatch(item);

        ScenarioExpect.Contains("Subscription", result);
        ScenarioExpect.Contains("12 months", result);
    }

    [Scenario("Order DeepClone Creates Independent Copy")]
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

        ScenarioExpect.NotSame(original, clone);
        ScenarioExpect.NotEqual(original.Id, clone.Id);
        ScenarioExpect.NotSame(original.Items, clone.Items);
    }

    [Scenario("Run Executes Without Errors")]
    [Fact]
    public void Run_Executes_Without_Errors()
    {
        PatternKit.Examples.EnterpriseDemo.EnterpriseOrderDemo.Run();
    }
}
