#if DOCFX
using System;
using System.Threading.Tasks;

namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

// Minimal stub so docfx metadata build can compile when source generators are skipped.
public partial class CorporateApplicationBuilder
{
    public static CorporateApplicationBuilder New() => new();

    public CorporateApplicationBuilder With(Func<CorporateAppState, CorporateAppState> step) => this;

    public CorporateApplicationBuilder WithAsync(Func<CorporateAppState, ValueTask<CorporateAppState>> step) => this;

    public CorporateApplicationBuilder Require(Func<CorporateAppState, string?> requirement) => this;

    public ValueTask<CorporateApp> BuildAsync() =>
        new(new CorporateApp(default!, Array.Empty<Func<IServiceProvider, ValueTask>>(), Array.Empty<string>()));
}
#endif
