namespace PatternKit.Examples.PointOfSale;

/// <summary>
/// Comprehensive demonstration of the Point of Sale decorator pattern.
/// Run this to see how decorators can be composed to build complex payment processing pipelines.
/// </summary>
public static class Demo
{
    public static void Run()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  PatternKit Decorator Pattern - Point of Sale Example");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        // Scenario 1: Simple small business
        Console.WriteLine("SCENARIO 1: Small Business - Basic Tax Calculation");
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        RunSimpleBusiness();

        Console.WriteLine("\n\nSCENARIO 2: Retail Store - Tax + Rounding");
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        RunRetailStore();

        Console.WriteLine("\n\nSCENARIO 3: E-commerce - Full Featured with Promotions");
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        RunEcommerce();

        Console.WriteLine("\n\nSCENARIO 4: Employee Purchase - Special Discount");
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        RunEmployeePurchase();

        Console.WriteLine("\n\nSCENARIO 5: Birthday Special - Conditional Decorators");
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        RunBirthdaySpecial();

        Console.WriteLine("\n\nSCENARIO 6: International - Nickel Rounding");
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        RunInternationalStore();

        Console.WriteLine("\n\n═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  Demo Complete - All Scenarios Executed Successfully!");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
    }

    #region Scenario 1: Simple Business

    private static void RunSimpleBusiness()
    {
        var order = CreateBasicOrder(
            orderId: "ORD-001",
            customerId: "CUST-001",
            loyaltyTier: null
        );

        var processor = PaymentProcessorDemo.CreateSimpleProcessor();
        var receipt = processor.Execute(order);

        PrintReceipt(receipt, "Simple Tax Only");
    }

    #endregion

    #region Scenario 2: Retail Store

    private static void RunRetailStore()
    {
        var order = CreateBasicOrder(
            orderId: "ORD-002",
            customerId: "CUST-002",
            loyaltyTier: "Silver"
        );

        var processor = PaymentProcessorDemo.CreateStandardRetailProcessor();
        var receipt = processor.Execute(order);

        PrintReceipt(receipt, "Standard Retail (Tax + Rounding)");
    }

    #endregion

    #region Scenario 3: E-commerce

    private static void RunEcommerce()
    {
        var order = CreateBasicOrder(
            orderId: "ORD-003",
            customerId: "CUST-003",
            loyaltyTier: "Gold",
            includeElectronics: true
        );

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
            },
            new()
            {
                PromotionCode = "SAVE20",
                Description = "$20 off orders over $100",
                DiscountAmount = 20m,
                MinimumPurchase = 100m,
                ValidFrom = new DateTime(2025, 1, 1),
                ValidUntil = new DateTime(2025, 12, 31)
            }
        };

        var processor = PaymentProcessorDemo.CreateEcommerceProcessor(promotions);
        var receipt = processor.Execute(order);

        PrintReceipt(receipt, "E-commerce (Full Featured)");
    }

    #endregion

    #region Scenario 4: Employee Purchase

    private static void RunEmployeePurchase()
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
                    UnitPrice = 299.99m,
                    Quantity = 1,
                    Category = "Furniture"
                },
                new()
                {
                    Sku = "ITEM-002",
                    ProductName = "Desk Lamp",
                    UnitPrice = 49.99m,
                    Quantity = 2,
                    Category = "Furniture"
                }
            }
        };

        var processor = PaymentProcessorDemo.CreateCashRegisterProcessor();
        var receipt = processor.Execute(order);

        PrintReceipt(receipt, "Employee Discount (20% off)");
    }

    #endregion

    #region Scenario 5: Birthday Special

    private static void RunBirthdaySpecial()
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
                BirthDate = new DateTime(1990, currentMonth, 15), // Birthday this month!
                IsEmployee = false
            },
            Store = CreateStore(),
            Items = new List<OrderLineItem>
            {
                new()
                {
                    Sku = "ITEM-003",
                    ProductName = "Premium Headphones",
                    UnitPrice = 199.99m,
                    Quantity = 1,
                    Category = "Electronics"
                },
                new()
                {
                    Sku = "ITEM-004",
                    ProductName = "Wireless Mouse",
                    UnitPrice = 79.99m,
                    Quantity = 1,
                    Category = "Electronics"
                }
            }
        };

        var processor = PaymentProcessorDemo.CreateBirthdaySpecialProcessor(order);
        var receipt = processor.Execute(order);

        PrintReceipt(receipt, "Birthday Special (Conditional Decorators)");
    }

    #endregion

    #region Scenario 6: International Store

    private static void RunInternationalStore()
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
                StateTaxRate = 0.13m,  // HST in Ontario
                LocalTaxRate = 0m
            },
            Items = new List<OrderLineItem>
            {
                new()
                {
                    Sku = "ITEM-005",
                    ProductName = "Coffee",
                    UnitPrice = 4.99m,
                    Quantity = 2,
                    Category = "Beverages"
                },
                new()
                {
                    Sku = "ITEM-006",
                    ProductName = "Muffin",
                    UnitPrice = 3.49m,
                    Quantity = 1,
                    Category = "Bakery"
                }
            }
        };

        var processor = PaymentProcessorDemo.CreateCashRegisterProcessor();
        var receipt = processor.Execute(order);

        PrintReceipt(receipt, "International - Nickel Rounding (Canada)");
    }

    #endregion

    #region Helper Methods

    private static PurchaseOrder CreateBasicOrder(
        string orderId,
        string customerId,
        string? loyaltyTier,
        bool includeElectronics = false)
    {
        var items = new List<OrderLineItem>
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
        };

        if (includeElectronics)
        {
            items.Add(new OrderLineItem
            {
                Sku = "LAPTOP-001",
                ProductName = "Laptop Computer",
                UnitPrice = 899.99m,
                Quantity = 1,
                Category = "Electronics"
            });
        }

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
            Items = items
        };
    }

    private static StoreLocation CreateStore()
    {
        return new StoreLocation
        {
            StoreId = "STORE-001",
            State = "CA",
            Country = "USA",
            StateTaxRate = 0.0725m,  // California state tax
            LocalTaxRate = 0.0125m   // Local tax
        };
    }

    private static void PrintReceipt(PaymentReceipt receipt, string scenario)
    {
        Console.WriteLine($"Scenario: {scenario}");
        Console.WriteLine($"Order ID: {receipt.OrderId}\n");

        Console.WriteLine("Items:");
        foreach (var item in receipt.LineItems)
        {
            Console.WriteLine($"  {item.Quantity}x {item.ProductName,-30} ${item.UnitPrice,7:F2} = ${item.LineTotal,8:F2}");
            if (item.Discount > 0)
                Console.WriteLine($"     Discount: -${item.Discount,8:F2}");
            if (item.Tax > 0)
                Console.WriteLine($"     Tax:      +${item.Tax,8:F2}");
        }

        Console.WriteLine(new string('─', 65));
        Console.WriteLine($"{"Subtotal:",-50} ${receipt.Subtotal,10:F2}");
        
        if (receipt.DiscountAmount > 0)
        {
            Console.WriteLine($"{"Total Discounts:",-50} -${receipt.DiscountAmount,9:F2}");
            if (receipt.AppliedPromotions.Any())
            {
                foreach (var promo in receipt.AppliedPromotions)
                {
                    Console.WriteLine($"  • {promo}");
                }
            }
        }
        
        if (receipt.TaxAmount > 0)
            Console.WriteLine($"{"Tax:",-50} ${receipt.TaxAmount,10:F2}");
        
        Console.WriteLine(new string('═', 65));
        Console.WriteLine($"{"TOTAL:",-50} ${receipt.FinalTotal,10:F2}");
        Console.WriteLine(new string('═', 65));

        if (receipt.LoyaltyPointsEarned > 0)
        {
            Console.WriteLine($"\n💰 Loyalty Points Earned: {receipt.LoyaltyPointsEarned:F0} points");
        }

        if (receipt.ProcessingLog.Any())
        {
            Console.WriteLine("\nProcessing Log:");
            foreach (var log in receipt.ProcessingLog)
            {
                Console.WriteLine($"  ℹ {log}");
            }
        }
    }

    #endregion
}

