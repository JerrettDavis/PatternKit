using System.Globalization;

namespace PatternKit.Examples.Tests.Fixtures;

public sealed class CultureFixture : IDisposable
{
    private readonly CultureInfo _orig = CultureInfo.CurrentCulture;
    private readonly CultureInfo _origUi = CultureInfo.CurrentUICulture;

    public CultureFixture()
    {
        var enUS = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = enUS;
        CultureInfo.CurrentUICulture = enUS;
        CultureInfo.DefaultThreadCurrentCulture = enUS;
        CultureInfo.DefaultThreadCurrentUICulture = enUS;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _orig;
        CultureInfo.CurrentUICulture = _origUi;
    }
}

[CollectionDefinition("Culture")]
public class CultureCollection : ICollectionFixture<CultureFixture>
{
}
