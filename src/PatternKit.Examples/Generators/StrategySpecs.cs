using PatternKit.Generators;

namespace PatternKit.Examples.Generators;

[GenerateStrategy(nameof(OrderRouter), typeof(char), StrategyKind.Action)]
public partial class OrderRouter
{
}

[GenerateStrategy(nameof(ScoreLabeler), typeof(int), typeof(string), StrategyKind.Result)]
public partial class ScoreLabeler
{
}

[GenerateStrategy(nameof(IntParser), typeof(string), typeof(int), StrategyKind.Try)]
public partial class IntParser
{
}