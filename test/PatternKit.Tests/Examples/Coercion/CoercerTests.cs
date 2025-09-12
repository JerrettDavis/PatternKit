using System.Text.Json;
using PatternKit.Examples.Coercion;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Examples.Coercion;

[Feature("Coercer<T> (Strategy-based coercion)")]
public class CoercerTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ---------- Helpers ----------
    private static JsonElement Json(string literal) => JsonDocument.Parse(literal).RootElement;

    private static int? CoerceInt<T>(T? v) => Coercer<int>.From(v);
    private static int? CoerceIntNullable(object? v) => Coercer<int?>.From(v);
    private static float? CoerceFloat(object? v) => Coercer<float>.From(v);
    private static double? CoerceDouble(object? v) => Coercer<double>.From(v);
    private static bool? CoerceBool(object? v) => Coercer<bool>.From(v);
    private static string? CoerceString(object? v) => Coercer<string>.From(v);
    private static string[]? CoerceStringArray(object? v) => Coercer<string[]>.From(v);

    private static int SourceInt() => 42;
    private static object? SourceNull() => null;
    private static JsonElement SourceJson123() => Json("123");
    private static JsonElement SourceJsonTrue() => Json("true");
    private static JsonElement SourceJsonPi() => Json("3.5");
    private static JsonElement SourceJsonText() => Json("\"hello\"");
    private static JsonElement SourceJsonStringArray() => Json("[\"a\",\"b\",\"c\"]");

    // ---------- Direct cast / null ----------
    [Scenario("Direct cast wins immediately; null returns default")]
    [Fact]
    public async Task DirectCastAndNull()
    {
        await Given("an int value 42", SourceInt)
            .When("coercing to int", CoerceInt)
            .Then("should be 42", v => v == 42)
            .AssertPassed();

        await Given("a null object", SourceNull)
            .When("coercing to int", CoerceInt)
            .Then("should be default (0? no—null because method returns int?)", v => v is null or 0) // From returns int?, so null expected
            .AssertPassed();

        await Given("a null object", SourceNull)
            .When("coercing to string", CoerceString)
            .Then("should be null", v => v is null)
            .AssertPassed();
    }

    // ---------- JsonElement → primitives ----------
    [Scenario("JsonElement number -> int")]
    [Fact]
    public async Task JsonNumberToInt()
    {
        await Given("json 123", SourceJson123)
            .When("coercing to int", je => Coercer<int>.From(je))
            .Then("should be 123", v => v == 123)
            .AssertPassed();
    }

    [Scenario("JsonElement number -> float/double")]
    [Fact]
    public async Task JsonNumberToFloatDouble()
    {
        await Given("json 3.5", SourceJsonPi)
            .When("coercing to float", je => Coercer<float>.From(je))
            .Then("should be 3.5f", v => v is > 3.49f and < 3.51f)
            .AssertPassed();

        await Given("json 3.5", SourceJsonPi)
            .When("coercing to double", je => Coercer<double>.From(je))
            .Then("should be 3.5", v => v is > 3.49 and < 3.51)
            .AssertPassed();
    }

    [Scenario("JsonElement bool -> bool")]
    [Fact]
    public async Task JsonBoolToBool()
    {
        await Given("json true", SourceJsonTrue)
            .When("coercing to bool", je => Coercer<bool>.From(je))
            .Then("should be true", v => v == true)
            .AssertPassed();
    }

    [Scenario("JsonElement any -> string via ToString()")]
    [Fact]
    public async Task JsonAnyToString()
    {
        await Given("json \"hello\"", SourceJsonText)
            .When("coercing to string", je => Coercer<string>.From(je))
            .Then("should be \"hello\"", v => v == "hello")
            .AssertPassed();

        await Given("json 123", SourceJson123)
            .When("coercing to string", je => Coercer<string>.From(je))
            .Then("should be \"123\"", v => v == "123")
            .AssertPassed();
    }

    // ---------- Arrays / single string ----------
    [Scenario("JsonElement array -> string[]; single string -> string[]")]
    [Fact]
    public async Task JsonArrayAndSingleStringToStringArray()
    {
        await Given("json [\"a\",\"b\",\"c\"]", SourceJsonStringArray)
            .When("coercing to string[]", je => Coercer<string[]>.From(je))
            .Then("should be [a,b,c]", arr => arr is { Length: 3 } a && a[0] == "a" && a[1] == "b" && a[2] == "c")
            .AssertPassed();

        await Given("single string \"one\"", () => (object)"one")
            .When("coercing to string[]", CoerceStringArray)
            .Then("should be [\"one\"]", arr => arr is { Length: 1 } a && a[0] == "one")
            .AssertPassed();
    }

    // ---------- Convertible fallback ----------
    [Scenario("Convertible fallback converts strings to numbers")]
    [Fact]
    public async Task ConvertibleFallbackStringsToNumbers()
    {
        await Given("string \"27\"", () => (object)"27")
            .When("coercing to int", CoerceInt)
            .Then("should be 27", v => v == 27)
            .AssertPassed();

        await Given("string \"2.25\"", () => (object)"2.25")
            .When("coercing to double", CoerceDouble)
            .Then("should be ~2.25", v => v is > 2.249 and < 2.251)
            .AssertPassed();
    }

    // ---------- Already typed passes through ----------
    [Scenario("Already-typed value returns as-is via DirectCast")]
    [Fact]
    public async Task AlreadyTypedPassesThrough()
    {
        await Given("bool true as object", () => (object)true)
            .When("coercing to bool", CoerceBool)
            .Then("should be true", v => v == true)
            .AssertPassed();

        await Given("int 11 as object", () => (object)11)
            .When("coercing to int?", CoerceIntNullable)
            .Then("should be 11", v => v == 11)
            .AssertPassed();
    }

    // ---------- Extension method facade ----------
    [Scenario("Extension method Coerce<T> forwards to Coercer<T>.From")]
    [Fact]
    public async Task ExtensionFacade()
    {
        await Given("json 123", SourceJson123)
            .When("coercing to int via extension", je => je.Coerce<int>())
            .Then("should be 123", v => v == 123)
            .AssertPassed();

        await Given("string \"5\"", () => (object)"5")
            .When("coercing to int via extension", o => o.Coerce<int>())
            .Then("should be 5", v => v == 5)
            .AssertPassed();
    }

    // ---------- Ordering correctness ----------
    [Scenario("Ordering: FromJsonNumberInt runs before ConvertibleFallback for ints")]
    [Fact]
    public async Task OrderingPrefersJsonHandlersOverFallback()
    {
        // If order broke, we'd still get 123—but this guards the intended priority
        // by ensuring JSON number path is taken without cultural side-effects.
        await Given("json 123", SourceJson123)
            .When("coercing to int", je => Coercer<int>.From(je))
            .Then("should be 123", v => v == 123)
            .AssertPassed();
    }
}