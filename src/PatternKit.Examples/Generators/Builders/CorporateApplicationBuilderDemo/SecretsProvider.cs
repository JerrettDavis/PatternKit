namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public static class SecretsProvider
{
    public static ValueTask<string> GetConnectionStringAsync(string environment)
    {
        var value = environment.Equals("Production", StringComparison.OrdinalIgnoreCase)
            ? "Server=prod;Database=Corporate;"
            : "Server=dev;Database=Corporate;";
        return new ValueTask<string>(value);
    }
}