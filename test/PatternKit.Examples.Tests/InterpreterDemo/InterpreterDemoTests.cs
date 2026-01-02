using PatternKit.Examples.InterpreterDemo;
using static PatternKit.Examples.InterpreterDemo.InterpreterDemo;

namespace PatternKit.Examples.Tests.InterpreterDemoTests;

public sealed class InterpreterDemoTests
{
    [Fact]
    public void PricingContext_Default_Values()
    {
        var ctx = new PricingContext();

        Assert.Equal(0m, ctx.CartTotal);
        Assert.Equal(0, ctx.ItemCount);
        Assert.Equal("Standard", ctx.CustomerTier);
        Assert.False(ctx.IsHoliday);
        Assert.Equal("", ctx.PromoCode);
        Assert.NotNull(ctx.Variables);
    }

    [Fact]
    public void CreatePricingInterpreter_Numeric_Literals()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext();

        var result = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("number", "42.5"),
            ctx);

        Assert.Equal(42.5m, result);
    }

    [Fact]
    public void CreatePricingInterpreter_Percent_Literals()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext();

        var result = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("percent", "15%"),
            ctx);

        Assert.Equal(0.15m, result);
    }

    [Fact]
    public void CreatePricingInterpreter_Var_CartTotal()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CartTotal = 100m };

        var result = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("var", "cart_total"),
            ctx);

        Assert.Equal(100m, result);
    }

    [Fact]
    public void CreatePricingInterpreter_Var_TierDiscount_Gold()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CustomerTier = "Gold" };

        var result = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("var", "tier_discount"),
            ctx);

        Assert.Equal(0.10m, result);
    }

    [Fact]
    public void CreatePricingInterpreter_Var_TierDiscount_Platinum()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CustomerTier = "Platinum" };

        var result = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("var", "tier_discount"),
            ctx);

        Assert.Equal(0.15m, result);
    }

    [Fact]
    public void CreatePricingInterpreter_Var_TierDiscount_Diamond()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CustomerTier = "Diamond" };

        var result = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("var", "tier_discount"),
            ctx);

        Assert.Equal(0.20m, result);
    }

    [Fact]
    public void CreatePricingInterpreter_Var_CustomVariable()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { Variables = { ["custom"] = 99m } };

        var result = interpreter.Interpret(
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("var", "custom"),
            ctx);

        Assert.Equal(99m, result);
    }

    [Fact]
    public void TierDiscountRule_Gold_Customer()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CartTotal = 150m, CustomerTier = "Gold" };

        var result = interpreter.Interpret(TierDiscountRule, ctx);

        Assert.Equal(15m, result); // 150 * 0.10 = 15
    }

    [Fact]
    public void ThresholdDiscountRule_Below_Threshold()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CartTotal = 35m };

        var result = interpreter.Interpret(ThresholdDiscountRule, ctx);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void ThresholdDiscountRule_Above_Threshold()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CartTotal = 75m };

        var result = interpreter.Interpret(ThresholdDiscountRule, ctx);

        Assert.Equal(5m, result);
    }

    [Fact]
    public void HolidayDiscountRule_Below_Cap()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CartTotal = 80m };

        var result = interpreter.Interpret(HolidayDiscountRule, ctx);

        Assert.Equal(12m, result); // 80 * 0.15 = 12
    }

    [Fact]
    public void HolidayDiscountRule_Capped_At_20()
    {
        var interpreter = CreatePricingInterpreter();
        var ctx = new PricingContext { CartTotal = 200m };

        var result = interpreter.Interpret(HolidayDiscountRule, ctx);

        Assert.Equal(20m, result); // 200 * 0.15 = 30, but capped at 20
    }

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

        Assert.True(goldResult);
        Assert.False(standardResult);
    }

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

        Assert.True(highResult);
        Assert.False(lowResult);
    }

    [Fact]
    public void VipEligibilityRule_Gold_High_Cart()
    {
        var interpreter = CreateEligibilityInterpreter();
        var ctx = new PricingContext { CustomerTier = "Gold", CartTotal = 150m };

        var result = interpreter.Interpret(VipEligibilityRule, ctx);

        Assert.True(result);
    }

    [Fact]
    public void VipEligibilityRule_Standard_High_Cart()
    {
        var interpreter = CreateEligibilityInterpreter();
        var ctx = new PricingContext { CustomerTier = "Standard", CartTotal = 150m };

        var result = interpreter.Interpret(VipEligibilityRule, ctx);

        Assert.False(result);
    }

    [Fact]
    public void VipEligibilityRule_Gold_Low_Cart()
    {
        var interpreter = CreateEligibilityInterpreter();
        var ctx = new PricingContext { CustomerTier = "Gold", CartTotal = 50m };

        var result = interpreter.Interpret(VipEligibilityRule, ctx);

        Assert.False(result);
    }

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

        Assert.Equal(25m, result); // 100 * 0.25 = 25
    }

    [Fact]
    public async Task CreateAsyncPricingInterpreter_Unknown_PromoCode()
    {
        var interpreter = CreateAsyncPricingInterpreter();
        var ctx = new PricingContext { CartTotal = 100m };

        var promoRule = PatternKit.Behavioral.Interpreter.ExpressionExtensions.NonTerminal("mul",
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("var", "cart_total"),
            PatternKit.Behavioral.Interpreter.ExpressionExtensions.Terminal("promo", "INVALID"));

        var result = await interpreter.InterpretAsync(promoRule, ctx);

        Assert.Equal(0m, result);
    }

    [Fact]
    public async Task RunAsync_Executes_Without_Errors()
    {
        await PatternKit.Examples.InterpreterDemo.InterpreterDemo.RunAsync();
    }

    [Fact]
    public void Run_Executes_Without_Errors()
    {
        PatternKit.Examples.InterpreterDemo.InterpreterDemo.Run();
    }
}
