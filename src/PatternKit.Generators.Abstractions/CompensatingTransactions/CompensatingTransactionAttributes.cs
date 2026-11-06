namespace PatternKit.Generators.CompensatingTransactions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateCompensatingTransactionAttribute : Attribute
{
    public string FactoryMethodName { get; set; } = "Create";

    public string TransactionName { get; set; } = "compensating-transaction";
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class CompensatingTransactionStepAttribute(string name, int order) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Compensating transaction step name is required.", nameof(name))
        : name;

    public int Order { get; } = order;

    public string Compensation { get; set; } = string.Empty;

    public string? Condition { get; set; }
}
