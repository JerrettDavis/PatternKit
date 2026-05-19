using PatternKit.Common;
using TinyBDD;

namespace PatternKit.Tests.Common;

public sealed class OptionTests
{
    [Scenario("Some HasValue ReturnsTrue")]
    [Fact]
    public void Some_HasValue_ReturnsTrue()
    {
        var option = Option<int>.Some(42);
        ScenarioExpect.True(option.HasValue);
    }

    [Scenario("Some ValueOrDefault ReturnsValue")]
    [Fact]
    public void Some_ValueOrDefault_ReturnsValue()
    {
        var option = Option<int>.Some(42);
        ScenarioExpect.Equal(42, option.ValueOrDefault);
    }

    [Scenario("None HasValue ReturnsFalse")]
    [Fact]
    public void None_HasValue_ReturnsFalse()
    {
        var option = Option<int>.None();
        ScenarioExpect.False(option.HasValue);
    }

    [Scenario("None ValueOrDefault ReturnsDefault")]
    [Fact]
    public void None_ValueOrDefault_ReturnsDefault()
    {
        var option = Option<int>.None();
        ScenarioExpect.Equal(0, option.ValueOrDefault);
    }

    [Scenario("OrDefault WithSome ReturnsValue")]
    [Fact]
    public void OrDefault_WithSome_ReturnsValue()
    {
        var option = Option<string>.Some("hello");
        ScenarioExpect.Equal("hello", option.OrDefault("fallback"));
    }

    [Scenario("OrDefault WithNone ReturnsFallback")]
    [Fact]
    public void OrDefault_WithNone_ReturnsFallback()
    {
        var option = Option<string>.None();
        ScenarioExpect.Equal("fallback", option.OrDefault("fallback"));
    }

    [Scenario("OrDefault WithNone NoFallback ReturnsNull")]
    [Fact]
    public void OrDefault_WithNone_NoFallback_ReturnsNull()
    {
        var option = Option<string>.None();
        ScenarioExpect.Null(option.OrDefault());
    }

    [Scenario("OrThrow WithSome ReturnsValue")]
    [Fact]
    public void OrThrow_WithSome_ReturnsValue()
    {
        var option = Option<int>.Some(42);
        ScenarioExpect.Equal(42, option.OrThrow());
    }

    [Scenario("OrThrow WithNone Throws")]
    [Fact]
    public void OrThrow_WithNone_Throws()
    {
        var option = Option<int>.None();
        var ex = ScenarioExpect.Throws<InvalidOperationException>(() => option.OrThrow());
        ScenarioExpect.Equal("No value.", ex.Message);
    }

    [Scenario("OrThrow WithNone CustomMessage Throws")]
    [Fact]
    public void OrThrow_WithNone_CustomMessage_Throws()
    {
        var option = Option<int>.None();
        var ex = ScenarioExpect.Throws<InvalidOperationException>(() => option.OrThrow("Custom error"));
        ScenarioExpect.Equal("Custom error", ex.Message);
    }

    [Scenario("Map WithSome TransformsValue")]
    [Fact]
    public void Map_WithSome_TransformsValue()
    {
        var option = Option<int>.Some(5);
        var mapped = option.Map(x => x * 2);

        ScenarioExpect.True(mapped.HasValue);
        ScenarioExpect.Equal(10, mapped.ValueOrDefault);
    }

    [Scenario("Map WithNone ReturnsNone")]
    [Fact]
    public void Map_WithNone_ReturnsNone()
    {
        var option = Option<int>.None();
        var mapped = option.Map(x => x * 2);

        ScenarioExpect.False(mapped.HasValue);
        ScenarioExpect.Equal(0, mapped.ValueOrDefault);
    }

    [Scenario("Map ChainedTransformations")]
    [Fact]
    public void Map_ChainedTransformations()
    {
        var option = Option<int>.Some(5);
        var result = option
            .Map(x => x * 2)
            .Map(x => x.ToString())
            .Map(s => $"Result: {s}")
            .OrDefault("nothing");

        ScenarioExpect.Equal("Result: 10", result);
    }

    [Scenario("Some WithNull StillHasValue")]
    [Fact]
    public void Some_WithNull_StillHasValue()
    {
        var option = Option<string>.Some(null);
        ScenarioExpect.True(option.HasValue);
        ScenarioExpect.Null(option.ValueOrDefault);
    }

    [Scenario("Map WithNullResult ReturnsSomeNull")]
    [Fact]
    public void Map_WithNullResult_ReturnsSomeNull()
    {
        var option = Option<string>.Some("test");
        var mapped = option.Map<string?>(_ => null);

        ScenarioExpect.True(mapped.HasValue);
        ScenarioExpect.Null(mapped.ValueOrDefault);
    }

    [Scenario("Some ValueType Works")]
    [Fact]
    public void Some_ValueType_Works()
    {
        var option = Option<DateTime>.Some(DateTime.UnixEpoch);
        ScenarioExpect.True(option.HasValue);
        ScenarioExpect.Equal(DateTime.UnixEpoch, option.ValueOrDefault);
    }

    [Scenario("None ValueType ReturnsDefaultValue")]
    [Fact]
    public void None_ValueType_ReturnsDefaultValue()
    {
        var option = Option<DateTime>.None();
        ScenarioExpect.False(option.HasValue);
        ScenarioExpect.Equal(default(DateTime), option.ValueOrDefault);
    }

    [Scenario("Map TypeChange Works")]
    [Fact]
    public void Map_TypeChange_Works()
    {
        var option = Option<int>.Some(42);
        var mapped = option.Map(x => new { Value = x });

        ScenarioExpect.True(mapped.HasValue);
        ScenarioExpect.Equal(42, mapped.ValueOrDefault!.Value);
    }

    [Scenario("OrDefault WithValue IgnoresFallback")]
    [Fact]
    public void OrDefault_WithValue_IgnoresFallback()
    {
        var option = Option<int>.Some(42);
        ScenarioExpect.Equal(42, option.OrDefault(999));
    }

    [Scenario("OrThrow Null Message Uses Default")]
    [Fact]
    public void OrThrow_Null_Message_Uses_Default()
    {
        var option = Option<int>.None();
        var ex = ScenarioExpect.Throws<InvalidOperationException>(() => option.OrThrow(null));
        ScenarioExpect.Equal("No value.", ex.Message);
    }
}
