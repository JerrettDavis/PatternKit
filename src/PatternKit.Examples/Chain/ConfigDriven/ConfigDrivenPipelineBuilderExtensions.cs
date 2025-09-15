using Microsoft.Extensions.Options;
using PatternKit.Behavioral.Chain;

namespace PatternKit.Examples.Chain.ConfigDriven;

public static class ConfigDrivenPipelineBuilderExtensions
{
    /// <summary>
    /// Recompute subtotal, apply configured discount rules in order, then compute tax.
    /// </summary>
    public static TransactionPipelineBuilder AddConfigDrivenDiscountsAndTax(
        this TransactionPipelineBuilder b,
        IOptions<PipelineOptions> opts,
        IEnumerable<IDiscountRule> discountRules)
    {
        // Build a stable map once (case-insensitive)
        var map = discountRules.ToDictionary(r => r.Key, r => r, StringComparer.OrdinalIgnoreCase);
        var chain = ActionChain<TransactionContext>.Create()
            .Use(static (in c, next) =>
            {
                c.RecomputeSubtotal();
                c.Log.Add($"subtotal: {c.Subtotal:C2}");
                next(in c);
            })
            .Finally((in c, next) =>
            {
                foreach (var key in opts.Value.DiscountRules)
                    if (map.TryGetValue(key, out var rule))
                        rule.Apply(c);

                var taxable = Math.Max(0m, c.Subtotal - c.DiscountTotal);
                var tax = Math.Round(taxable * 0.0875m, 2);
                c.SetTax(tax);
                c.Log.Add($"pre-round total: {c.GrandTotal:C2}");
                next(in c);
            })
            .Build();

        return b.AddStage(chain);
    }

    /// <summary>
    /// Apply configured rounding strategies in order (first-match-wins semantics live inside each strategy).
    /// </summary>
    public static TransactionPipelineBuilder AddConfigDrivenRounding(
        this TransactionPipelineBuilder b,
        IOptions<PipelineOptions> opts,
        IEnumerable<IRoundingStrategy> rounding)
    {
        var map = rounding.ToDictionary(r => r.Key, r => r, StringComparer.OrdinalIgnoreCase);
        var chain = ActionChain<TransactionContext>.Create()
            .Finally((in c, next) =>
            {
                foreach (var key in opts.Value.Rounding)
                    if (map.TryGetValue(key, out var strat))
                        strat.Apply(c);

                c.Log.Add($"total: {c.GrandTotal:C2}");
                next(in c);
            })
            .Build();

        return b.AddStage(chain);
    }
}
