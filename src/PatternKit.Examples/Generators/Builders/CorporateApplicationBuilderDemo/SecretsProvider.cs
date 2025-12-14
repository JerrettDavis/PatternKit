namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public static class SecretsProvider
{
    public static ValueTask<string> GetConnectionStringAsync(CorporateEnvironment environment)
    {
        var value = environment == CorporateEnvironment.Production
            ? "Server=prod;Database=Corporate;"
            : "Server=dev;Database=Corporate;";
        return new ValueTask<string>(value);
    }
}
