using PatternKit.Creational.Builder;

namespace PatternKit.Examples.Pricing;

public interface IPricingSource
{
    ValueTask<decimal?> TryGetUnitPriceAsync(Sku sku, Location loc, CancellationToken ct);
    string Name { get; }
}

public sealed class DbPricingSource : IPricingSource
{
    private readonly Dictionary<(string sku, string region), decimal> _prices;
    public string Name => "db";
    public DbPricingSource(Dictionary<(string sku, string region), decimal> prices) => _prices = prices;
    public ValueTask<decimal?> TryGetUnitPriceAsync(Sku sku, Location loc, CancellationToken ct)
    {
        _ = ct; // demo
        return new(_prices.TryGetValue((sku.Id, loc.Region), out var p) ? p : (decimal?)null);
    }
}

public sealed class ApiPricingSource(Dictionary<string, decimal> prices) : IPricingSource
{
    // sku-only for demo
    public string Name => "api";

    public async ValueTask<decimal?> TryGetUnitPriceAsync(Sku sku, Location loc, CancellationToken ct)
    {
        _ = loc; await Task.Delay(5, ct); // simulate latency
        return prices.TryGetValue(sku.Id, out var p) ? p : (decimal?)null;
    }
}

public sealed class FilePricingSource(Dictionary<string, decimal> prices) : IPricingSource
{
    // sku-only for demo
    public string Name => "file";

    public async ValueTask<decimal?> TryGetUnitPriceAsync(Sku sku, Location loc, CancellationToken ct)
    {
        _ = loc; await Task.Delay(1, ct);
        return prices.TryGetValue(sku.Id, out var p) ? p : (decimal?)null;
    }
}

public delegate bool SourcePred(in Sku sku);
public delegate IPricingSource SourceProvider();

public sealed class SourceRouter
{
    private readonly SourcePred[] _preds;
    private readonly SourceProvider[] _providers;
    private readonly SourceProvider _default;

    private SourceRouter(SourcePred[] preds, SourceProvider[] providers, SourceProvider def)
        => (_preds, _providers, _default) = (preds, providers, def);

    public static Builder Create() => new();

    public IPricingSource Resolve(in Sku sku)
    {
        for (var i = 0; i < _preds.Length; i++) if (_preds[i](in sku)) return _providers[i]();
        return _default();
    }

    public sealed class Builder
    {
        private readonly ChainBuilder<(SourcePred pred, SourceProvider prov)> _b = ChainBuilder<(SourcePred, SourceProvider)>.Create();
        private SourceProvider? _default;

        public Builder When(SourcePred pred, SourceProvider provider) { _b.Add((pred, provider)); return this; }
        public Builder Default(SourceProvider provider) { _default = provider; return this; }
        public SourceRouter Build()
        {
            var def = _default ?? throw new InvalidOperationException("Default provider is required.");
            return _b.Build(items => new SourceRouter(
                items.Select(t => t.pred).ToArray(),
                items.Select(t => t.prov).ToArray(),
                def));
        }
    }
}

public static class DefaultSourceRouting
{
    public static SourceRouter Build(DbPricingSource db, ApiPricingSource api, FilePricingSource file)
        => SourceRouter.Create()
            .When(static (in Sku s) => s.HasTag("price:db"), () => db)
            .When(static (in Sku s) => s.HasTag("price:api"), () => api)
            .When(static (in Sku s) => s.HasTag("price:file"), () => file)
            .Default(() => db)
            .Build();
}

