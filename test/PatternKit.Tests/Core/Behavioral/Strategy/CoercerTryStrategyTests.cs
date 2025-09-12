using System.Text.Json;
using PatternKit.Behavioral.Strategy;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Core.Behavioral.Strategy;

[Feature("Coercer (JsonElement -> primitives)")]
public class CoercerTryStrategyTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // Handlers for a tiny demo coercer: JsonElement -> int | bool | string
    private static bool FromJsonInt(in JsonElement je, out object? r)
    {
        if (je.ValueKind == JsonValueKind.Number)
        {
            r = je.TryGetInt32(out var i) ? i : null;
            return r is not null;
        }

        r = null;
        return false;
    }

    private static bool FromJsonBool(in JsonElement je, out object? r)
    {
        if (je.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            r = je.GetBoolean();
            return true;
        }

        r = null;
        return false;
    }

    private static bool FromJsonString(in JsonElement je, out object? r)
    {
        // allow numbers/booleans/strings as text fallback
        r = je.ToString();
        return true;
    }

    [Scenario("Coerce number -> int; bool -> bool; string fallback")]
    [Fact]
    public async Task CoerceJsonElement()
    {
        await Given("a TryStrategy<JsonElement, object> coercer", BuildCoercer)
            .When("coercing 123", s => Execute(s, JsonDocument.Parse("123").RootElement))
            .Then("should return 123 (int)", v => v is int i && i == 123)
            .AssertPassed();

        await Given("same coercer", BuildCoercer)
            .When("coercing true", s => Execute(s, JsonDocument.Parse("true").RootElement))
            .Then("should return true (bool)", v => v is bool b && b)
            .AssertPassed();

        await Given("same coercer", BuildCoercer)
            .When("coercing \"hello\"", s => Execute(s, JsonDocument.Parse("\"hello\"").RootElement))
            .Then("should return \"hello\" (string)", v => v is string s2 && s2 == "hello")
            .AssertPassed();

        static TryStrategy<JsonElement, object> BuildCoercer()
            => TryStrategy<JsonElement, object>.Create()
                .Always(FromJsonInt)
                .Or.Always(FromJsonBool)
                .Finally(FromJsonString) // fallback to string
                .Build();

        static object? Execute(TryStrategy<JsonElement, object> s, JsonElement je)
            => s.Execute(in je, out var r) ? r : null;
    }
}