using PatternKit.Examples.InterpreterDemo;
using TinyBDD;
using static PatternKit.Examples.InterpreterDemo.InterpreterDemo;
using Expr = PatternKit.Behavioral.Interpreter.ExpressionExtensions;

namespace PatternKit.Examples.Tests.InterpreterDemoTests;

public sealed class InterpreterDemoTests
{
    [Scenario("PricingContext Default Values")]
    [Fact]
    public void PricingContext_Default_Values()
    {
        var ctx = new PricingContext();

        ScenarioExpect.Equal(0m, ctx.CartTotal);
        ScenarioExpect.Equal(0, ctx.ItemCount);
        ScenarioExpect.Equal("Standard", ctx.CustomerTier);
        ScenarioExpect.False(ctx.IsHoliday);
        ScenarioExpect.Equal("", ctx.PromoCode);
        ScenarioExpect.NotNull(ctx.Variables);
    }

    [Scenario("CreatePricingInterpreter Numeric Literals")]
    [Fact]
    public void CreatePricingInterpreter_Numeric_Literals()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext();

        var result = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("number", "42.5"),
            ctx);

        ScenarioExpect.Equal(42.5m, result);
    }

    [Scenario("CreatePricingInterpreter Percent Literals")]
    [Fact]
    public void CreatePricingInterpreter_Percent_Literals()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext();

        var result = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("percent", "15%"),
            ctx);

        ScenarioExpect.Equal(0.15m, result);
    }

    [Scenario("CreatePricingInterpreter Var CartTotal")]
    [Fact]
    public void CreatePricingInterpreter_Var_CartTotal()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CartTotal = 100m };

        var result = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("var", "cart_total"),
            ctx);

        ScenarioExpect.Equal(100m, result);
    }

    [Scenario("CreatePricingInterpreter Var TierDiscount Gold")]
    [Fact]
    public void CreatePricingInterpreter_Var_TierDiscount_Gold()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CustomerTier = "Gold" };

        var result = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("var", "tier_discount"),
            ctx);

        ScenarioExpect.Equal(0.10m, result);
    }

    [Scenario("CreatePricingInterpreter Var TierDiscount Platinum")]
    [Fact]
    public void CreatePricingInterpreter_Var_TierDiscount_Platinum()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CustomerTier = "Platinum" };

        var result = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("var", "tier_discount"),
            ctx);

        ScenarioExpect.Equal(0.15m, result);
    }

    [Scenario("CreatePricingInterpreter Var TierDiscount Diamond")]
    [Fact]
    public void CreatePricingInterpreter_Var_TierDiscount_Diamond()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CustomerTier = "Diamond" };

        var result = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("var", "tier_discount"),
            ctx);

        ScenarioExpect.Equal(0.20m, result);
    }

    [Scenario("CreatePricingInterpreter Var CustomVariable")]
    [Fact]
    public void CreatePricingInterpreter_Var_CustomVariable()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { Variables = { ["custom"] = 99m } };

        var result = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("var", "custom"),
            ctx);

        ScenarioExpect.Equal(99m, result);
    }

    [Scenario("TierDiscountRule Gold Customer")]
    [Fact]
    public void TierDiscountRule_Gold_Customer()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CartTotal = 150m, CustomerTier = "Gold" };

        var result = interpreter.Interpret(TierDiscountRule, ctx);

        ScenarioExpect.Equal(15m, result); // 150 * 0.10 = 15
    }

    [Scenario("CreateGeneratedPricingInterpreter Matches Fluent Pricing Rules")]
    [Fact]
    public void CreateGeneratedPricingInterpreter_Matches_Fluent_Pricing_Rules()
    {
        var fluent = CreatePricingInterpreter();
        var generated = CreateGeneratedPricingInterpreter();
        var ctx = new PricingContext { CartTotal = 150m, CustomerTier = "Platinum" };

        var fluentResult = fluent.Interpret(TierDiscountRule, ctx);
        var generatedResult = generated.Interpret(TierDiscountRule, ctx);

        ScenarioExpect.Equal(fluentResult, generatedResult);
        ScenarioExpect.True(generated.HasTerminal("percent"));
        ScenarioExpect.True(generated.HasNonTerminal("round"));
    }

    [Scenario("CreateGeneratedPricingInterpreter Covers Production Pricing Rules")]
    [Fact]
    public void CreateGeneratedPricingInterpreter_Covers_Production_Pricing_Rules()
    {
        var interpreter = CreateGeneratedPricingInterpreter();
        var ctx = new PricingContext
        {
            CartTotal = 120m,
            ItemCount = 4,
            CustomerTier = "Diamond",
            Variables = { ["manual_credit"] = 8m }
        };
        var arithmeticRule = Expr.NonTerminal("sub",
            Expr.NonTerminal("add", Expr.Terminal("number", "10"), Expr.Terminal("var", "manual_credit")),
            Expr.NonTerminal("div", Expr.Terminal("number", "9"), Expr.Terminal("number", "3")));
        var fallbackRule = Expr.NonTerminal("add",
            Expr.Terminal("var", "missing"),
            Expr.Terminal("var", "item_count"));
        var comparisonRule = Expr.NonTerminal("add",
            Expr.NonTerminal("eq", Expr.Terminal("number", "2"), Expr.Terminal("number", "2")),
            Expr.NonTerminal("lt", Expr.Terminal("number", "1"), Expr.Terminal("number", "2")));

        ScenarioExpect.Equal(24m, interpreter.Interpret(TierDiscountRule, ctx));
        ScenarioExpect.Equal(15m, interpreter.Interpret(arithmeticRule, ctx));
        ScenarioExpect.Equal(4m, interpreter.Interpret(fallbackRule, ctx));
        ScenarioExpect.Equal(2m, interpreter.Interpret(comparisonRule, ctx));
        ScenarioExpect.Equal(0m, interpreter.Interpret(
            Expr.NonTerminal("div", Expr.Terminal("number", "9"), Expr.Terminal("number", "0")),
            ctx));
        ScenarioExpect.Equal(3m, interpreter.Interpret(
            Expr.NonTerminal("if", Expr.Terminal("number", "0"), Expr.Terminal("number", "7"), Expr.Terminal("number", "3")),
            ctx));
        ScenarioExpect.Throws<InvalidOperationException>(() => interpreter.Interpret(
            Expr.NonTerminal("if", Expr.Terminal("number", "1")),
            ctx));
    }

    [Scenario("CreateGeneratedPricingInterpreter Covers Remaining Generated Operators")]
    [Fact]
    public void CreateGeneratedPricingInterpreter_Covers_Remaining_Generated_Operators()
    {
        var interpreter = CreateGeneratedPricingInterpreter();
        var ctx = new PricingContext
        {
            CartTotal = 80m,
            CustomerTier = "Standard"
        };
        var percentRule = Expr.NonTerminal("mul",
            Expr.Terminal("var", "cart_total"),
            Expr.Terminal("percent", "12.5%"));
        var comparisonRule = Expr.NonTerminal("add",
            Expr.NonTerminal("gt", Expr.Terminal("number", "5"), Expr.Terminal("number", "2")),
            Expr.NonTerminal("min", Expr.Terminal("number", "10"), Expr.Terminal("number", "3")));
        var maxRule = Expr.NonTerminal("max",
            Expr.Terminal("var", "tier_discount"),
            Expr.Terminal("number", "2"));

        ScenarioExpect.Equal(10m, interpreter.Interpret(percentRule, ctx));
        ScenarioExpect.Equal(4m, interpreter.Interpret(comparisonRule, ctx));
        ScenarioExpect.Equal(2m, interpreter.Interpret(maxRule, ctx));
        ScenarioExpect.Equal(0m, interpreter.Interpret(Expr.Terminal("var", "tier_discount"), ctx));
    }

    [Scenario("CreatePricingInterpreter Covers Production Arithmetic And Fallback Rules")]
    [Fact]
    public void CreatePricingInterpreter_Covers_Production_Arithmetic_And_Fallback_Rules()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext
        {
            CartTotal = 120m,
            ItemCount = 4,
            CustomerTier = "Standard"
        };
        var arithmetic = Expr.NonTerminal("max",
            Expr.NonTerminal("sub", Expr.Terminal("number", "20"), Expr.Terminal("number", "5")),
            Expr.NonTerminal("div", Expr.Terminal("number", "10"), Expr.Terminal("number", "2")));
        var comparisons = Expr.NonTerminal("add",
            Expr.NonTerminal("lt", Expr.Terminal("number", "2"), Expr.Terminal("number", "3")),
            Expr.NonTerminal("eq", Expr.Terminal("number", "4"), Expr.Terminal("number", "4")));

        ScenarioExpect.Equal(0m, interpreter.Interpret(Expr.Terminal("var", "tier_discount"), ctx));
        ScenarioExpect.Equal(15m, interpreter.Interpret(arithmetic, ctx));
        ScenarioExpect.Equal(2m, interpreter.Interpret(comparisons, ctx));
        ScenarioExpect.Equal(0m, interpreter.Interpret(
            Expr.NonTerminal("div", Expr.Terminal("number", "9"), Expr.Terminal("number", "0")),
            ctx));
        ScenarioExpect.Throws<InvalidOperationException>(() => interpreter.Interpret(
            Expr.NonTerminal("if", Expr.Terminal("number", "1")),
            ctx));
    }

    [Scenario("ThresholdDiscountRule Below Threshold")]
    [Fact]
    public void ThresholdDiscountRule_Below_Threshold()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CartTotal = 35m };

        var result = interpreter.Interpret(ThresholdDiscountRule, ctx);

        ScenarioExpect.Equal(0m, result);
    }

    [Scenario("ThresholdDiscountRule Above Threshold")]
    [Fact]
    public void ThresholdDiscountRule_Above_Threshold()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CartTotal = 75m };

        var result = interpreter.Interpret(ThresholdDiscountRule, ctx);

        ScenarioExpect.Equal(5m, result);
    }

    [Scenario("HolidayDiscountRule Below Cap")]
    [Fact]
    public void HolidayDiscountRule_Below_Cap()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CartTotal = 80m };

        var result = interpreter.Interpret(HolidayDiscountRule, ctx);

        ScenarioExpect.Equal(12m, result); // 80 * 0.15 = 12
    }

    [Scenario("HolidayDiscountRule Capped At 20")]
    [Fact]
    public void HolidayDiscountRule_Capped_At_20()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CartTotal = 200m };

        var result = interpreter.Interpret(HolidayDiscountRule, ctx);

        ScenarioExpect.Equal(20m, result); // 200 * 0.15 = 30, but capped at 20
    }

    [Scenario("CreateEligibilityInterpreter Tier Check")]
    [Fact]
    public void CreateEligibilityInterpreter_Tier_Check()
    {
        var interpreter = CreateEligibilityInterpreter();
        var goldCtx = new PricingContext { CustomerTier = "Gold" };
        var standardCtx = new PricingContext { CustomerTier = "Standard" };

        var goldResult = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("tier", "Gold"),
            goldCtx);

        var standardResult = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("tier", "Gold"),
            standardCtx);

        ScenarioExpect.True(goldResult);
        ScenarioExpect.False(standardResult);
    }

    [Scenario("CreateEligibilityInterpreter CartOver Check")]
    [Fact]
    public void CreateEligibilityInterpreter_CartOver_Check()
    {
        var interpreter = CreateEligibilityInterpreter();
        var highCart = new PricingContext { CartTotal = 150m };
        var lowCart = new PricingContext { CartTotal = 50m };

        var highResult = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("cartOver", "100"),
            highCart);

        var lowResult = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("cartOver", "100"),
            lowCart);

        ScenarioExpect.True(highResult);
        ScenarioExpect.False(lowResult);
    }

    [Scenario("VipEligibilityRule Gold High Cart")]
    [Fact]
    public void VipEligibilityRule_Gold_High_Cart()
    {
        var interpreter = CreateEligibilityInterpreter();
        var ctx = new PricingContext { CustomerTier = "Gold", CartTotal = 150m };

        var result = interpreter.Interpret(VipEligibilityRule, ctx);

        ScenarioExpect.True(result);
    }

    [Scenario("CreateGeneratedEligibilityInterpreter Matches Fluent Eligibility Rules")]
    [Fact]
    public void CreateGeneratedEligibilityInterpreter_Matches_Fluent_Eligibility_Rules()
    {
        var fluent = CreateEligibilityInterpreter();
        var generated = CreateGeneratedEligibilityInterpreter();
        var ctx = new PricingContext { CustomerTier = "Gold", CartTotal = 150m };

        var fluentResult = fluent.Interpret(VipEligibilityRule, ctx);
        var generatedResult = generated.Interpret(VipEligibilityRule, ctx);

        ScenarioExpect.Equal(fluentResult, generatedResult);
        ScenarioExpect.True(generated.HasTerminal("cartOver"));
        ScenarioExpect.True(generated.HasNonTerminal("and"));
    }

    [Scenario("CreateGeneratedEligibilityInterpreter Covers Production Eligibility Rules")]
    [Fact]
    public void CreateGeneratedEligibilityInterpreter_Covers_Production_Eligibility_Rules()
    {
        var interpreter = CreateGeneratedEligibilityInterpreter();
        var ctx = new PricingContext
        {
            CartTotal = 120m,
            ItemCount = 6,
            CustomerTier = "Gold",
            PromoCode = "SPRING",
            IsHoliday = true
        };
        var eligibilityRule = Expr.NonTerminal("and",
            Expr.NonTerminal("or", Expr.Terminal("promo", "SPRING"), Expr.Terminal("bool", "false")),
            Expr.NonTerminal("and", Expr.Terminal("isHoliday", ""), Expr.Terminal("itemsOver", "5")));

        ScenarioExpect.True(interpreter.Interpret(eligibilityRule, ctx));
        ScenarioExpect.False(interpreter.Interpret(Expr.NonTerminal("not", Expr.Terminal("tier", "Gold")), ctx));
        ScenarioExpect.False(interpreter.Interpret(Expr.Terminal("itemsOver", "10"), ctx));
        ScenarioExpect.False(interpreter.Interpret(Expr.Terminal("promo", "WINTER"), ctx));
    }

    [Scenario("CreateEligibilityInterpreter Covers Production Eligibility Terminals")]
    [Fact]
    public void CreateEligibilityInterpreter_Covers_Production_Eligibility_Terminals()
    {
        var interpreter = CreateEligibilityInterpreter();
        var ctx = new PricingContext
        {
            CartTotal = 120m,
            ItemCount = 6,
            CustomerTier = "Gold",
            PromoCode = "SPRING",
            IsHoliday = true
        };
        var rule = Expr.NonTerminal("and",
            Expr.NonTerminal("or", Expr.Terminal("promo", "SPRING"), Expr.Terminal("bool", "false")),
            Expr.NonTerminal("and", Expr.Terminal("isHoliday", ""), Expr.Terminal("itemsOver", "5")));

        ScenarioExpect.True(interpreter.Interpret(rule, ctx));
        ScenarioExpect.False(interpreter.Interpret(Expr.NonTerminal("not", Expr.Terminal("tier", "Gold")), ctx));
        ScenarioExpect.False(interpreter.Interpret(Expr.Terminal("itemsOver", "10"), ctx));
        ScenarioExpect.False(interpreter.Interpret(Expr.Terminal("promo", "WINTER"), ctx));
    }

    [Scenario("VipEligibilityRule Standard High Cart")]
    [Fact]
    public void VipEligibilityRule_Standard_High_Cart()
    {
        var interpreter = CreateEligibilityInterpreter();
        var ctx = new PricingContext { CustomerTier = "Standard", CartTotal = 150m };

        var result = interpreter.Interpret(VipEligibilityRule, ctx);

        ScenarioExpect.False(result);
    }

    [Scenario("VipEligibilityRule Gold Low Cart")]
    [Fact]
    public void VipEligibilityRule_Gold_Low_Cart()
    {
        var interpreter = CreateEligibilityInterpreter();
        var ctx = new PricingContext { CustomerTier = "Gold", CartTotal = 50m };

        var result = interpreter.Interpret(VipEligibilityRule, ctx);

        ScenarioExpect.False(result);
    }

    [Scenario("CreateAsyncPricingInterpreter PromoCode Lookup")]
    [Fact]
    public async Task CreateAsyncPricingInterpreter_PromoCode_Lookup()
    {
        var interpreter = CreateAsyncPricingInterpreter();
        var ctx = new PricingContext { CartTotal = 100m };

        var promoRule = PatternKit.Behavioral.Interpreter.ExpressionExtensions.NonTerminal("round",
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.NonTerminal("mul",
                PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("var", "cart_total"),
                PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("promo", "HOLIDAY25")));

        var result = await interpreter.InterpretAsync(promoRule, ctx);

        ScenarioExpect.Equal(25m, result); // 100 * 0.25 = 25
    }

    [Scenario("CreateAsyncPricingInterpreter Unknown PromoCode")]
    [Fact]
    public async Task CreateAsyncPricingInterpreter_Unknown_PromoCode()
    {
        var interpreter = CreateAsyncPricingInterpreter();
        var ctx = new PricingContext { CartTotal = 100m };

        var promoRule = PatternKit.Behavioral.Interpreter.ExpressionExtensions.NonTerminal("mul",
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("var", "cart_total"),
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("promo", "INVALID"));

        var result = await interpreter.InterpretAsync(promoRule, ctx);

        ScenarioExpect.Equal(0m, result);
    }

    [Scenario("CreateAsyncPricingInterpreter Covers Production Terminal Variants")]
    [Fact]
    public async Task CreateAsyncPricingInterpreter_Covers_Production_Terminal_Variants()
    {
        var interpreter = CreateAsyncPricingInterpreter();
        var ctx = new PricingContext { CartTotal = 200m, ItemCount = 3 };
        var saveRule = Expr.NonTerminal("round",
            Expr.NonTerminal("mul", Expr.Terminal("var", "cart_total"), Expr.Terminal("promo", "SAVE10")));
        var vipRule = Expr.NonTerminal("max",
            Expr.Terminal("percent", "15%"),
            Expr.Terminal("promo", "VIP50"));
        var itemRule = Expr.NonTerminal("mul",
            Expr.Terminal("var", "item_count"),
            Expr.Terminal("number", "2"));

        ScenarioExpect.Equal(20m, await interpreter.InterpretAsync(saveRule, ctx));
        ScenarioExpect.Equal(0.50m, await interpreter.InterpretAsync(vipRule, ctx));
        ScenarioExpect.Equal(6m, await interpreter.InterpretAsync(itemRule, ctx));
    }

    [Scenario("RunAsync Executes Without Errors")]
    [Fact]
    public async Task RunAsync_Executes_Without_Errors()
    {
        await PatternKit.Examples.InterpreterDemo.InterpreterDemo.RunAsync();
    }

    [Scenario("Run Executes Without Errors")]
    [Fact]
    public void Run_Executes_Without_Errors()
    {
        PatternKit.Examples.InterpreterDemo.InterpreterDemo.Run();
    }
}
