namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public sealed class NotificationOptions
{
    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = "queue";
}