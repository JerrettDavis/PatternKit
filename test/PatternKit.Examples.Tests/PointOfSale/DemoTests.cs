using PatternKit.Examples.PointOfSale;

namespace PatternKit.Examples.Tests.PointOfSaleDemoTests;

public sealed class DemoTests
{
    [Fact]
    public void Run_Executes_Without_Errors()
    {
        PatternKit.Examples.PointOfSale.Demo.Run();
    }
}

public sealed class DemoScenarioTests
{
    [Fact]
    public void SimpleBusiness_Scenario_Works()
    {
        var order = CreateBasicOrder("ORD-001", "CUST-001", null);
        var processor = PaymentProcessorDemo.CreateSimpleProcessor();

        var receipt = processor.Execute(order);

        Assert.NotNull(receipt);
        Assert.True(receipt.TaxAmount > 0);
        Assert.Equal("ORD-001", receipt.OrderId);
    }

    [Fact]
    public void RetailStore_Scenario_Works()
    {
        var order = CreateBasicOrder("ORD-002", "CUST-002", "Silver");
        var processor = PaymentProcessorDemo.CreateStandardRetailProcessor();

        var receipt = processor.Execute(order);

        Assert.NotNull(receipt);
        Assert.True(receipt.TaxAmount > 0);
    }

    [Fact]
    public void Ecommerce_Scenario_With_Promotions()
    {
        var order = CreateBasicOrderWithElectronics("ORD-003", "CUST-003", "Gold");
        var promotions = new List<PromotionConfig>
        {
            new()
            {
                PromotionCode = "FALL2025",
                Description = "Fall Sale - 10% off electronics",
                DiscountPercent = 0.10m,
                ApplicableCategory = "Electronics",
                MinimumPurchase = 0m,
                ValidFrom = new DateTime(2025, 9, 1),
                ValidUntil = new DateTime(2025, 11, 30)
            }
        };

        var processor = PaymentProcessorDemo.CreateEcommerceProcessor(promotions);
        var receipt = processor.Execute(order);

        Assert.NotNull(receipt);
    }

    [Fact]
    public void EmployeePurchase_Gets_Discount()
    {
        var order = new PurchaseOrder
        {
            OrderId = "ORD-004",
            Customer = new CustomerInfo
            {
                CustomerId = "EMP-001",
                IsEmployee = true,
                LoyaltyTier = null,
                LoyaltyPoints = 0
            },
            Store = CreateStore(),
            Items = new List<OrderLineItem>
            {
                new()
                {
                    Sku = "ITEM-001",
                    ProductName = "Office Chair",
                    UnitPrice = 100m,
                    Quantity = 1,
                    Category = "Furniture"
                }
            }
        };

        var processor = PaymentProcessorDemo.CreateCashRegisterProcessor();
        var receipt = processor.Execute(order);

        Assert.NotNull(receipt);
        Assert.True(receipt.DiscountAmount > 0); // Employee discount should apply
    }

    [Fact]
    public void BirthdaySpecial_Scenario()
    {
        var currentMonth = DateTime.UtcNow.Month;
        var order = new PurchaseOrder
        {
            OrderId = "ORD-005",
            Customer = new CustomerInfo
            {
                CustomerId = "CUST-005",
                LoyaltyTier = "Platinum",
                LoyaltyPoints = 5000,
                BirthDate = new DateTime(1990, currentMonth, 15),
                IsEmployee = false
            },
            Store = CreateStore(),
            Items = new List<OrderLineItem>
            {
                new()
                {
                    Sku = "ITEM-003",
                    ProductName = "Headphones",
                    UnitPrice = 199.99m,
                    Quantity = 1,
                    Category = "Electronics"
                }
            }
        };

        var processor = PaymentProcessorDemo.CreateBirthdaySpecialProcessor(order);
        var receipt = processor.Execute(order);

        Assert.NotNull(receipt);
    }

    [Fact]
    public void InternationalStore_With_NickelRounding()
    {
        var order = new PurchaseOrder
        {
            OrderId = "ORD-006",
            Customer = new CustomerInfo
            {
                CustomerId = "CUST-006",
                LoyaltyTier = null,
                LoyaltyPoints = 0,
                IsEmployee = false
            },
            Store = new StoreLocation
            {
                StoreId = "STORE-CA-001",
                State = "ON",
                Country = "Canada",
                StateTaxRate = 0.13m,
                LocalTaxRate = 0m
            },
            Items = new List<OrderLineItem>
            {
                new()
                {
                    Sku = "ITEM-005",
                    ProductName = "Coffee",
                    UnitPrice = 4.99m,
                    Quantity = 1,
                    Category = "Beverages"
                }
            }
        };

        var processor = PaymentProcessorDemo.CreateCashRegisterProcessor();
        var receipt = processor.Execute(order);

        Assert.NotNull(receipt);
    }

    private static PurchaseOrder CreateBasicOrder(string orderId, string customerId, string? loyaltyTier)
    {
        return new PurchaseOrder
        {
            OrderId = orderId,
            Customer = new CustomerInfo
            {
                CustomerId = customerId,
                LoyaltyTier = loyaltyTier,
                LoyaltyPoints = loyaltyTier == "Gold" ? 1000 : loyaltyTier == "Platinum" ? 5000 : 0,
                IsEmployee = false
            },
            Store = CreateStore(),
            Items = new List<OrderLineItem>
            {
                new()
                {
                    Sku = "BOOK-001",
                    ProductName = "Programming Patterns",
                    UnitPrice = 49.99m,
                    Quantity = 1,
                    Category = "Books"
                },
                new()
                {
                    Sku = "SHIRT-001",
                    ProductName = "T-Shirt",
                    UnitPrice = 19.99m,
                    Quantity = 2,
                    Category = "Clothing"
                }
            }
        };
    }

    private static PurchaseOrder CreateBasicOrderWithElectronics(string orderId, string customerId, string? loyaltyTier)
    {
        var order = CreateBasicOrder(orderId, customerId, loyaltyTier);
        order.Items.Add(new OrderLineItem
        {
            Sku = "LAPTOP-001",
            ProductName = "Laptop Computer",
            UnitPrice = 899.99m,
            Quantity = 1,
            Category = "Electronics"
        });
        return order;
    }

    private static StoreLocation CreateStore()
    {
        return new StoreLocation
        {
            StoreId = "STORE-001",
            State = "CA",
            Country = "USA",
            StateTaxRate = 0.0725m,
            LocalTaxRate = 0.0125m
        };
    }
}
