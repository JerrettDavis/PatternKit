using PatternKit.Behavioral.Interpreter;
using static PatternKit.Behavioral.Interpreter.ExpressionExtensions;

namespace PatternKit.Examples.InterpreterDemo;

/// <summary>
/// Demonstrates the Interpreter pattern for evaluating domain-specific languages.
/// This example shows a business rules engine that interprets pricing and discount rules.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real-world scenario:</b> An e-commerce pricing engine that evaluates complex
/// discount rules, tax calculations, and promotional offers defined in a DSL.
/// </para>
/// <para>
/// <b>Key GoF concepts demonstrated:</b>
/// <list type="bullet">
/// <item>Terminal expressions (literals: numbers, booleans, identifiers)</item>
/// <item>Non-terminal expressions (operations: add, multiply, if-then-else)</item>
/// <item>Context-based interpretation (cart context, customer context)</item>
/// <item>Extensible grammar (easy to add new operations)</item>
/// </list>
/// </para>
/// </remarks>
public static class InterpreterDemo
{
    // ─────────────────────────────────────────────────────────────────────────
    // Context - The environment for rule evaluation
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class PricingContext
    {
        public decimal CartTotal { get; set; }
        public int ItemCount { get; set; }
        public string CustomerTier { get; set; } = "Standard";
        public bool IsHoliday { get; set; }
        public string PromoCode { get; set; } = "";
        public Dictionary<string, decimal> Variables { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pricing Rules Interpreter
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an interpreter for pricing rule expressions.
    /// Returns a decimal result (discount amount, price, or percentage).
    /// </summary>
    public static Interpreter<PricingContext, decimal> CreatePricingInterpreter()
    {
        return Interpreter.Create<PricingContext, decimal>()

            // Terminal: Parse numeric literals
            .Terminal("number", (token, _) => decimal.Parse(token))

            // Terminal: Parse percentage (e.g., "15%")
            .Terminal("percent", (token, _) =>
            {
                var value = token.TrimEnd('%');
                return decimal.Parse(value) / 100m;
            })

            // Terminal: Read variable from context
            .Terminal("var", (token, ctx) =>
            {
                return token switch
                {
                    "cart_total" => ctx.CartTotal,
                    "item_count" => ctx.ItemCount,
                    "tier_discount" => ctx.CustomerTier switch
                    {
                        "Gold" => 0.10m,
                        "Platinum" => 0.15m,
                        "Diamond" => 0.20m,
                        _ => 0.0m
                    },
                    _ => ctx.Variables.TryGetValue(token, out var v) ? v : 0m
                };
            })

            // Non-terminal: Addition
            .Binary("add", (left, right) => left + right)

            // Non-terminal: Subtraction
            .Binary("sub", (left, right) => left - right)

            // Non-terminal: Multiplication
            .Binary("mul", (left, right) => left * right)

            // Non-terminal: Division
            .Binary("div", (left, right) => right != 0 ? left / right : 0)

            // Non-terminal: Minimum
            .Binary("min", Math.Min)

            // Non-terminal: Maximum
            .Binary("max", Math.Max)

            // Non-terminal: Conditional (if-then-else)
            // args[0] = condition (>0 means true), args[1] = then, args[2] = else
            .NonTerminal("if", (args, _) =>
            {
                if (args.Length != 3)
                    throw new InvalidOperationException("if requires 3 arguments: condition, then, else");
                return args[0] > 0 ? args[1] : args[2];
            })

            // Non-terminal: Greater than (returns 1 if true, 0 if false)
            .Binary("gt", (left, right) => left > right ? 1m : 0m)

            // Non-terminal: Less than
            .Binary("lt", (left, right) => left < right ? 1m : 0m)

            // Non-terminal: Equals
            .Binary("eq", (left, right) => left == right ? 1m : 0m)

            // Non-terminal: Round to nearest cent
            .Unary("round", value => Math.Round(value, 2, MidpointRounding.AwayFromZero))

            .Build();
    }

    /// <summary>
    /// Creates an async interpreter for rules that need external lookups.
    /// </summary>
    public static AsyncInterpreter<PricingContext, decimal> CreateAsyncPricingInterpreter()
    {
        return AsyncInterpreter.Create<PricingContext, decimal>()
            .Terminal("number", token => decimal.Parse(token))
            .Terminal("percent", token => decimal.Parse(token.TrimEnd('%')) / 100m)
            .Terminal("var", (token, ctx) => token switch
            {
                "cart_total" => ctx.CartTotal,
                "item_count" => ctx.ItemCount,
                _ => 0m
            })

            // Async terminal: Lookup promo code discount from "database"
            .Terminal("promo", async (code, ctx, ct) =>
            {
                await Task.Delay(10, ct); // Simulate DB lookup
                return code.ToUpperInvariant() switch
                {
                    "SAVE10" => 0.10m,
                    "HOLIDAY25" => 0.25m,
                    "VIP50" => 0.50m,
                    _ => 0m
                };
            })

            .Binary("mul", (l, r) => l * r)
            .Binary("max", Math.Max)
            .Unary("round", v => Math.Round(v, 2))
            .Build();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Boolean Rules Interpreter (for eligibility checks)
    // ─────────────────────────────────────────────────────────────────────────

    public static Interpreter<PricingContext, bool> CreateEligibilityInterpreter()
    {
        return Interpreter.Create<PricingContext, bool>()

            // Terminal: Boolean literals
            .Terminal("bool", (token, _) => bool.Parse(token))

            // Terminal: Check customer tier
            .Terminal("tier", (token, ctx) =>
                ctx.CustomerTier.Equals(token, StringComparison.OrdinalIgnoreCase))

            // Terminal: Check promo code
            .Terminal("promo", (token, ctx) =>
                ctx.PromoCode.Equals(token, StringComparison.OrdinalIgnoreCase))

            // Terminal: Check holiday
            .Terminal("isHoliday", (_, ctx) => ctx.IsHoliday)

            // Terminal: Numeric comparison (cart > threshold)
            .Terminal("cartOver", (token, ctx) => ctx.CartTotal > decimal.Parse(token))

            // Terminal: Item count check
            .Terminal("itemsOver", (token, ctx) => ctx.ItemCount > int.Parse(token))

            // Non-terminal: AND
            .Binary("and", (left, right) => left && right)

            // Non-terminal: OR
            .Binary("or", (left, right) => left || right)

            // Non-terminal: NOT
            .Unary("not", value => !value)

            .Build();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sample Rule Expressions
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rule: 10% off cart total for Gold members
    /// Expression: mul(var(cart_total), percent(10%))
    /// </summary>
    public static IExpression TierDiscountRule =>
        NonTerminal("round",
            NonTerminal("mul",
                Terminal("var", "cart_total"),
                Terminal("var", "tier_discount")));

    /// <summary>
    /// Rule: $5 off if cart > $50, else $0
    /// Expression: if(gt(var(cart_total), 50), 5, 0)
    /// </summary>
    public static IExpression ThresholdDiscountRule =>
        NonTerminal("if",
            NonTerminal("gt", Terminal("var", "cart_total"), Terminal("number", "50")),
            Terminal("number", "5"),
            Terminal("number", "0"));

    /// <summary>
    /// Rule: 15% holiday discount capped at $20
    /// Expression: min(mul(var(cart_total), percent(15%)), 20)
    /// </summary>
    public static IExpression HolidayDiscountRule =>
        NonTerminal("round",
            NonTerminal("min",
                NonTerminal("mul",
                    Terminal("var", "cart_total"),
                    Terminal("percent", "15%")),
                Terminal("number", "20")));

    /// <summary>
    /// Eligibility: Gold/Platinum tier AND cart over $100
    /// Expression: and(or(tier(Gold), tier(Platinum)), cartOver(100))
    /// </summary>
    public static IExpression VipEligibilityRule =>
        NonTerminal("and",
            NonTerminal("or",
                Terminal("tier", "Gold"),
                Terminal("tier", "Platinum")),
            Terminal("cartOver", "100"));

    /// <summary>
    /// Runs the complete Interpreter pattern demonstration.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          INTERPRETER PATTERN DEMONSTRATION                    ║");
        Console.WriteLine("║   E-Commerce Pricing Rules Engine with DSL Evaluation        ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

        var pricingInterpreter = CreatePricingInterpreter();
        var asyncInterpreter = CreateAsyncPricingInterpreter();
        var eligibilityInterpreter = CreateEligibilityInterpreter();

        // ── Scenario 1: Tier-based discount ──
        Console.WriteLine("▶ Scenario 1: Tier-Based Discount Calculation");
        Console.WriteLine(new string('─', 50));

        var goldCtx = new PricingContext { CartTotal = 150m, CustomerTier = "Gold" };
        var tierDiscount = pricingInterpreter.Interpret(TierDiscountRule, goldCtx);
        Console.WriteLine($"  Customer: {goldCtx.CustomerTier} | Cart: ${goldCtx.CartTotal}");
        Console.WriteLine($"  Rule: 'Tier discount % of cart total'");
        Console.WriteLine($"  Discount: ${tierDiscount:F2}");

        var platinumCtx = new PricingContext { CartTotal = 150m, CustomerTier = "Platinum" };
        var platDiscount = pricingInterpreter.Interpret(TierDiscountRule, platinumCtx);
        Console.WriteLine($"\n  Customer: {platinumCtx.CustomerTier} | Cart: ${platinumCtx.CartTotal}");
        Console.WriteLine($"  Discount: ${platDiscount:F2}");

        // ── Scenario 2: Threshold discount ──
        Console.WriteLine("\n▶ Scenario 2: Threshold-Based Discount");
        Console.WriteLine(new string('─', 50));

        var smallCart = new PricingContext { CartTotal = 35m };
        var largeCart = new PricingContext { CartTotal = 75m };

        var smallDiscount = pricingInterpreter.Interpret(ThresholdDiscountRule, smallCart);
        var largeDiscount = pricingInterpreter.Interpret(ThresholdDiscountRule, largeCart);

        Console.WriteLine($"  Rule: '$5 off if cart > $50'");
        Console.WriteLine($"  Cart $35: Discount = ${smallDiscount}");
        Console.WriteLine($"  Cart $75: Discount = ${largeDiscount}");

        // ── Scenario 3: Capped holiday discount ──
        Console.WriteLine("\n▶ Scenario 3: Holiday Discount (15% capped at $20)");
        Console.WriteLine(new string('─', 50));

        var holidayContexts = new[]
        {
            new PricingContext { CartTotal = 80m },
            new PricingContext { CartTotal = 150m },
            new PricingContext { CartTotal = 200m },
        };

        Console.WriteLine("  Rule: 'min(15% of cart, $20)'");
        foreach (var ctx in holidayContexts)
        {
            var discount = pricingInterpreter.Interpret(HolidayDiscountRule, ctx);
            Console.WriteLine($"  Cart ${ctx.CartTotal}: Discount = ${discount:F2}");
        }

        // ── Scenario 4: Async promo code lookup ──
        Console.WriteLine("\n▶ Scenario 4: Async Promo Code Lookup");
        Console.WriteLine(new string('─', 50));

        var promoRule = NonTerminal("round",
            NonTerminal("mul",
                Terminal("var", "cart_total"),
                Terminal("promo", "HOLIDAY25")));

        var promoCtx = new PricingContext { CartTotal = 100m };
        var promoDiscount = await asyncInterpreter.InterpretAsync(promoRule, promoCtx);
        Console.WriteLine($"  Promo: HOLIDAY25 | Cart: ${promoCtx.CartTotal}");
        Console.WriteLine($"  Discount (from async lookup): ${promoDiscount:F2}");

        // ── Scenario 5: Eligibility check ──
        Console.WriteLine("\n▶ Scenario 5: VIP Eligibility Check");
        Console.WriteLine(new string('─', 50));

        var eligibilityTests = new[]
        {
            new PricingContext { CustomerTier = "Standard", CartTotal = 150m },
            new PricingContext { CustomerTier = "Gold", CartTotal = 50m },
            new PricingContext { CustomerTier = "Gold", CartTotal = 150m },
            new PricingContext { CustomerTier = "Platinum", CartTotal = 200m },
        };

        Console.WriteLine("  Rule: '(Gold OR Platinum) AND cart > $100'");
        foreach (var ctx in eligibilityTests)
        {
            var eligible = eligibilityInterpreter.Interpret(VipEligibilityRule, ctx);
            Console.WriteLine($"  {ctx.CustomerTier} + ${ctx.CartTotal} cart: {(eligible ? "✓ Eligible" : "✗ Not eligible")}");
        }

        // ── Scenario 6: Dynamic rule building ──
        Console.WriteLine("\n▶ Scenario 6: Dynamic Rule Composition");
        Console.WriteLine(new string('─', 50));

        // Build rule dynamically: total discount = tier + threshold + (holiday ? 15% : 0)
        var combinedRule = NonTerminal("round",
            NonTerminal("add",
                TierDiscountRule,
                NonTerminal("add",
                    ThresholdDiscountRule,
                    NonTerminal("if",
                        Terminal("var", "item_count"), // Using itemCount > 0 as proxy for holiday
                        HolidayDiscountRule,
                        Terminal("number", "0")))));

        var fullContext = new PricingContext
        {
            CartTotal = 120m,
            CustomerTier = "Gold",
            ItemCount = 5 // Non-zero triggers "holiday" discount
        };

        var totalDiscount = pricingInterpreter.Interpret(combinedRule, fullContext);
        Console.WriteLine($"  Combined rule: Tier + Threshold + Holiday discounts");
        Console.WriteLine($"  Context: ${fullContext.CartTotal} cart, {fullContext.CustomerTier} tier");
        Console.WriteLine($"  Total discount: ${totalDiscount:F2}");

        Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
        Console.WriteLine("Pattern Benefits Demonstrated:");
        Console.WriteLine("  • Extensible grammar - add new operations without changing core");
        Console.WriteLine("  • Context-aware evaluation - rules adapt to customer/cart state");
        Console.WriteLine("  • Composable expressions - build complex rules from simple parts");
        Console.WriteLine("  • Async support - integrate with external data sources");
        Console.WriteLine("  • Type-safe DSL - compile-time verification of expression structure");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
    }

    public static void Run() => RunAsync().GetAwaiter().GetResult();
}
