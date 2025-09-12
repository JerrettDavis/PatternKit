using PatternKit.Behavioral.Strategy;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Core.Behavioral.Strategy;

[Feature("Strategy (Selector)")]
public class SelectorStrategyTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static bool IsNull<T>(in T? x) => x is null;
    private static bool IsInt(in object? x) => x is int;
    private static bool IsString(in object? x) => x is string;

    private static string HandleNull(in object? _) => "NULL";
    private static string HandleInt(in object? _) => "INT";
    private static string HandleString(in object? _) => "STRING";
    private static string DefaultHandler(in object? _) => "DEFAULT";

    [Scenario("First true predicate selects the handler; default used when none match")]
    [Fact]
    public async Task FirstPredicateWins_WithDefault()
    {
        await Given("a selector that tests null, int, string; default to 'DEFAULT'", BuildSelector)
            .When("executing with 42", s => s.Execute(42))
            .Then("should be 'INT'", v => v == "INT")
            .AssertPassed();

        await Given("same selector", BuildSelector)
            .When("executing with \"hi\"", s => s.Execute("hi"))
            .Then("should be 'STRING'", v => v == "STRING")
            .AssertPassed();

        await Given("same selector", BuildSelector)
            .When("executing with null", s => s.Execute(null!))
            .Then("should be 'NULL'", v => v == "NULL")
            .AssertPassed();

        await Given("same selector", BuildSelector)
            .When("executing with 3.14 (no explicit match)", s => s.Execute(3.14))
            .Then("should be 'DEFAULT'", v => v == "DEFAULT")
            .AssertPassed();

        return;

        static Strategy<object?, string> BuildSelector()
            => Strategy<object?, string>.Create()
                .When(IsNull).Then(HandleNull)
                .When(IsInt).Then(HandleInt)
                .When(IsString).Then(HandleString)
                .Default(DefaultHandler)
                .Build();
    }
}