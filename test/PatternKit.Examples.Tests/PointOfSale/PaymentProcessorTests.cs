using PatternKit.Examples.PointOfSale;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.PointOfSale;

[Feature("Point of Sale - Decorator Pattern Example")]
public sealed class PaymentProcessorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    #region Test Data Factories
    private static PurchaseOrder CreateBasicOrder(string orderId = "TEST-001", string? loyaltyTier = null)
    {
        return new PurchaseOrder
        {
            OrderId = orderId,
            Customer = new CustomerInfo
            {
                CustomerId = "CUST-001",
                LoyaltyTier = loyaltyTier,
                LoyaltyPoints = 0,
                IsEmployee = false
            },
            Store = new StoreLocation
            {
                StoreId = "STORE-001",
                State = "CA",
                Country = "USA",
                StateTaxRate = 0.0725m,
                LocalTaxRate = 0.0125m
            },
            Items =
            [
                new()
                {
                    Sku = "ITEM-001",
                    ProductName = "Test Item",
                    UnitPrice = 100m,
                    Quantity = 1,
                    Category = "Test"
                }
            ]
        };
    }

    private static PurchaseOrder CreateOrderWithCustomer(CustomerInfo customer, decimal itemPrice = 100m, string orderId = "TEST-001")
    {
        return new PurchaseOrder
        {
            OrderId = orderId,
            Customer = customer,
            Store = new StoreLocation
            {
                StoreId = "STORE-001",
                State = "CA",
                Country = "USA",
                StateTaxRate = 0.0725m,
                LocalTaxRate = 0.0125m
            },
            Items =
            [
                new()
                {
                    Sku = "ITEM-001",
                    ProductName = "Test Item",
                    UnitPrice = itemPrice,
                    Quantity = 1,
                    Category = "Test"
                }
            ]
        };
    }

    #endregion

    [Scenario("Simple processor calculates tax correctly")]
    [Fact]
    public Task SimpleProcessor_CalculatesTax()
        => Given("an order with $100 item", () => CreateBasicOrder())
            .When("processing with simple processor", order =>
            {
                var processor = PaymentProcessorDemo.CreateSimpleProcessor();
                return processor.Execute(order);
            })
            .Then("subtotal is $100", r => r.Subtotal == 100m)
            .And("tax is calculated", r => r.TaxAmount > 0m)
            .And("tax is 8.5% of subtotal", r => r.TaxAmount == 8.5m) // 0.0725 + 0.0125 = 0.085
            .And("final total includes tax", r => r.FinalTotal == 108.5m)
            .AssertPassed();

    [Scenario("Standard retail processor applies rounding")]
    [Fact]
    public Task StandardRetailProcessor_AppliesRounding()
        => Given("an order that results in fractional cents", () =>
            {
                var order = CreateBasicOrder();
                order.Items[0] = order.Items[0] with { UnitPrice = 10.01m }; // Will create fractional tax
                return order;
            })
            .When("processing with standard retail processor", order =>
            {
                var processor = PaymentProcessorDemo.CreateStandardRetailProcessor();
                return processor.Execute(order);
            })
            .Then("final total is properly rounded", r => r.FinalTotal == Math.Round(r.FinalTotal, 2))
            .And("processing log mentions rounding", r => r.ProcessingLog.Any(log => log.Contains("Rounding")))
            .AssertPassed();

    [Scenario("E-commerce processor applies promotional discount")]
    [Fact]
    public Task EcommerceProcessor_AppliesPromotions()
        => Given("an order and active promotions", () =>
            {
                var order = CreateBasicOrder("ORD-PROMO");
                order.Items[0] = order.Items[0] with 
                { 
                    UnitPrice = 100m,
                    Category = "Electronics"
                };

                var promotions = new List<PromotionConfig>
                {
                    new()
                    {
                        PromotionCode = "TEST10",
                        Description = "10% off electronics",
                        DiscountPercent = 0.10m,
                        ApplicableCategory = "Electronics",
                        MinimumPurchase = 0m,
                        ValidFrom = DateTime.UtcNow.AddDays(-1),
                        ValidUntil = DateTime.UtcNow.AddDays(1)
                    }
                };

                return (order, promotions);
            })
            .When("processing with e-commerce processor", ctx =>
            {
                var processor = PaymentProcessorDemo.CreateEcommerceProcessor(ctx.promotions);
                return processor.Execute(ctx.order);
            })
            .Then("discount is applied", r => r.DiscountAmount > 0m)
            .And("promotion appears in applied list", r => r.AppliedPromotions.Any(p => p.Contains("electronics")))
            .And("final total reflects discount", r => r.FinalTotal < r.Subtotal + r.TaxAmount)
            .AssertPassed();

    [Scenario("Loyalty discount applies correctly for Gold tier")]
    [Fact]
    public Task EcommerceProcessor_AppliesLoyaltyDiscount()
        => Given("a Gold tier customer order", () => CreateBasicOrder(loyaltyTier: "Gold"))
            .When("processing with e-commerce processor", order =>
            {
                var processor = PaymentProcessorDemo.CreateEcommerceProcessor([]);
                return processor.Execute(order);
            })
            .Then("loyalty discount is 10%", r => r.DiscountAmount == 10m) // 10% of $100
            .And("Gold Member Discount is applied", r => r.AppliedPromotions.Contains("Gold Member Discount"))
            .And("loyalty points are earned", r => r.LoyaltyPointsEarned > 0)
            .AssertPassed();

    [Scenario("Employee discount applies correctly")]
    [Fact]
    public Task CashRegisterProcessor_AppliesEmployeeDiscount()
        => Given("an employee purchase order", () =>
            {
                var customer = new CustomerInfo
                {
                    CustomerId = "CUST-001",
                    LoyaltyTier = null,
                    LoyaltyPoints = 0,
                    IsEmployee = true
                };
                return CreateOrderWithCustomer(customer);
            })
            .When("processing with cash register processor", order =>
            {
                var processor = PaymentProcessorDemo.CreateCashRegisterProcessor();
                return processor.Execute(order);
            })
            .Then("employee discount is 20%", r => r.DiscountAmount == 20m) // 20% of $100
            .And("Employee Discount is in promotions", r => r.AppliedPromotions.Contains("Employee Discount"))
            .And("discount applied before tax", r => r.TaxAmount < 8.5m) // Tax on $80 instead of $100
            .AssertPassed();

    [Scenario("Birthday discount applies in birthday month")]
    [Fact]
    public Task BirthdayProcessor_AppliesDiscountInBirthdayMonth()
        => Given("an order in customer's birthday month", () =>
            {
                var customer = new CustomerInfo
                {
                    CustomerId = "CUST-001",
                    LoyaltyTier = null,
                    LoyaltyPoints = 0,
                    IsEmployee = false,
                    BirthDate = new DateTime(1990, DateTime.UtcNow.Month, 15)
                };
                return CreateOrderWithCustomer(customer);
            })
            .When("processing with birthday special processor", order =>
            {
                var processor = PaymentProcessorDemo.CreateBirthdaySpecialProcessor(order);
                return processor.Execute(order);
            })
            .Then("birthday discount is applied", r => r.DiscountAmount > 0m)
            .And("Birthday Month Special in promotions", r => r.AppliedPromotions.Contains("Birthday Month Special"))
            .AssertPassed();

    [Scenario("Birthday discount does not apply outside birthday month")]
    [Fact]
    public Task BirthdayProcessor_NoDiscountOutsideBirthdayMonth()
        => Given("an order outside customer's birthday month", () =>
            {
                var notBirthdayMonth = DateTime.UtcNow.Month == 12 ? 1 : DateTime.UtcNow.Month + 1;
                var customer = new CustomerInfo
                {
                    CustomerId = "CUST-001",
                    LoyaltyTier = null,
                    LoyaltyPoints = 0,
                    IsEmployee = false,
                    BirthDate = new DateTime(1990, notBirthdayMonth, 15)
                };
                return CreateOrderWithCustomer(customer);
            })
            .When("processing with birthday special processor", order =>
            {
                var processor = PaymentProcessorDemo.CreateBirthdaySpecialProcessor(order);
                return processor.Execute(order);
            })
            .Then("no birthday discount applied", r => !r.AppliedPromotions.Contains("Birthday Month Special"))
            .AssertPassed();

    [Scenario("Nickel rounding rounds to nearest 5 cents")]
    [Fact]
    public Task CashRegisterProcessor_RoundsToNickel()
        => Given("an order resulting in non-nickel amount", () =>
            {
                var order = CreateBasicOrder();
                order.Items[0] = order.Items[0] with { UnitPrice = 10.03m }; // Will result in $10.88 total
                return order;
            })
            .When("processing with cash register processor", order =>
            {
                var processor = PaymentProcessorDemo.CreateCashRegisterProcessor();
                return processor.Execute(order);
            })
            .Then("final total ends in 0 or 5", r =>
            {
                var cents = (int)(r.FinalTotal * 100) % 10;
                return cents == 0 || cents == 5;
            })
            .AssertPassed();

    [Scenario("Multiple decorators apply in correct order")]
    [Fact]
    public Task EcommerceProcessor_AppliesDecoratorsInOrder()
        => Given("a Platinum customer with promotion eligible order", () =>
            {
                var order = CreateBasicOrder(loyaltyTier: "Platinum");
                order.Items[0] = order.Items[0] with
                {
                    UnitPrice = 100m,
                    Category = "Electronics"
                };

                var promotions = new List<PromotionConfig>
                {
                    new()
                    {
                        PromotionCode = "TEST20",
                        Description = "$20 off",
                        DiscountAmount = 20m,
                        MinimumPurchase = 50m,
                        ValidFrom = DateTime.UtcNow.AddDays(-1),
                        ValidUntil = DateTime.UtcNow.AddDays(1)
                    }
                };

                return (order, promotions);
            })
            .When("processing with e-commerce processor", ctx =>
            {
                var processor = PaymentProcessorDemo.CreateEcommerceProcessor(ctx.promotions);
                return processor.Execute(ctx.order);
            })
            .Then("both promotional and loyalty discounts applied", r => r.DiscountAmount > 20m)
            .And("tax calculated after discounts", r =>
            {
                var expectedTaxBase = r.Subtotal - r.DiscountAmount;
                var expectedTax = expectedTaxBase * 0.085m; // 8.5% combined tax rate
                return Math.Abs(r.TaxAmount - expectedTax) < 0.01m;
            })
            .And("loyalty points earned", r => r.LoyaltyPointsEarned > 0)
            .And("processing log shows all steps", r => r.ProcessingLog.Count > 3)
            .AssertPassed();

    [Scenario("Validation decorator catches empty orders")]
    [Fact]
    public Task EcommerceProcessor_ValidatesEmptyOrders()
        => Given("an order with no items", () =>
            {
                var order = CreateBasicOrder();
                order.Items.Clear();
                return order;
            })
            .When("processing with e-commerce processor", order =>
                Record.Exception(() =>
                {
                    var processor = PaymentProcessorDemo.CreateEcommerceProcessor([]);
                    processor.Execute(order);
                }))
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .And("mentions items requirement", ex => ex!.Message.Contains("at least one item"))
            .AssertPassed();

    [Scenario("Tax exempt items do not incur tax")]
    [Fact]
    public Task SimpleProcessor_RespectsTaxExemption()
        => Given("an order with tax-exempt item", () =>
            {
                var order = CreateBasicOrder();
                order.Items[0] = order.Items[0] with { IsTaxExempt = true };
                return order;
            })
            .When("processing with simple processor", order =>
            {
                var processor = PaymentProcessorDemo.CreateSimpleProcessor();
                return processor.Execute(order);
            })
            .Then("no tax applied", r => r.TaxAmount == 0m)
            .And("final total equals subtotal", r => r.FinalTotal == r.Subtotal)
            .AssertPassed();

    [Scenario("Audit logging captures processing details")]
    [Fact]
    public Task EcommerceProcessor_LogsAuditTrail()
        => Given("a standard order", () => CreateBasicOrder(loyaltyTier: "Silver"))
            .When("processing with e-commerce processor", order =>
            {
                var processor = PaymentProcessorDemo.CreateEcommerceProcessor([]);
                return processor.Execute(order);
            })
            .Then("processing log is not empty", r => r.ProcessingLog.Count > 0)
            .And("log contains start timestamp", r => r.ProcessingLog.Any(l => l.Contains("started")))
            .And("log contains completion time", r => r.ProcessingLog.Any(l => l.Contains("completed")))
            .AssertPassed();

    [Scenario("Promotions only apply within valid date range")]
    [Fact]
    public Task EcommerceProcessor_RespectsPromotionDates()
        => Given("an order with expired promotion", () =>
            {
                var order = CreateBasicOrder();
                var promotions = new List<PromotionConfig>
                {
                    new()
                    {
                        PromotionCode = "EXPIRED",
                        Description = "Expired promotion",
                        DiscountPercent = 0.50m,
                        MinimumPurchase = 0m,
                        ValidFrom = DateTime.UtcNow.AddDays(-30),
                        ValidUntil = DateTime.UtcNow.AddDays(-1) // Expired yesterday
                    }
                };

                return (order, promotions);
            })
            .When("processing with e-commerce processor", ctx =>
            {
                var processor = PaymentProcessorDemo.CreateEcommerceProcessor(ctx.promotions);
                return processor.Execute(ctx.order);
            })
            .Then("no discount applied", r => r.DiscountAmount == 0m)
            .And("promotion not in applied list", r => !r.AppliedPromotions.Any())
            .AssertPassed();

    [Scenario("Loyalty points calculated with tier multiplier")]
    [Fact]
    public Task EcommerceProcessor_CalculatesLoyaltyPointsWithMultiplier()
        => Given("Platinum tier customer order", () => CreateBasicOrder(loyaltyTier: "Platinum"))
            .When("processing with e-commerce processor", order =>
            {
                var processor = PaymentProcessorDemo.CreateEcommerceProcessor([]);
                return processor.Execute(order);
            })
            .Then("points earned with 2x multiplier", r =>
            {
                // $100 - 15% discount = $85 * 2x multiplier = 170 points
                var expectedPoints = Math.Floor((100m - 15m) * 2.0m);
                return r.LoyaltyPointsEarned == expectedPoints;
            })
            .And("processing log mentions multiplier", r =>
                r.ProcessingLog.Any(l => l.Contains("2") && l.Contains("multiplier")))
            .AssertPassed();

    [Scenario("Processor can be reused for multiple orders")]
    [Fact]
    public Task Processor_IsReusable()
        => Given("a single processor instance", PaymentProcessorDemo.CreateSimpleProcessor)
            .When("processing multiple orders", processor =>
            {
                var order1 = CreateBasicOrder("ORD-001");
                var order2 = CreateOrderWithCustomer(new CustomerInfo
                {
                    CustomerId = "CUST-001",
                    LoyaltyTier = null,
                    LoyaltyPoints = 0,
                    IsEmployee = false
                }, itemPrice: 200m, orderId: "ORD-002");

                var receipt1 = processor.Execute(order1);
                var receipt2 = processor.Execute(order2);

                return (receipt1, receipt2);
            })
            .Then("both orders processed correctly", r => r.receipt1.FinalTotal == 108.5m && r.receipt2.FinalTotal == 217m)
            .And("order IDs are distinct", r => r.receipt1.OrderId != r.receipt2.OrderId)
            .AssertPassed();
}
