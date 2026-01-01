using PatternKit.Common;

namespace PatternKit.Tests.Common;

public sealed class OptionTests
{
    [Fact]
    public void Some_HasValue_ReturnsTrue()
    {
        var option = Option<int>.Some(42);
        Assert.True(option.HasValue);
    }

    [Fact]
    public void Some_ValueOrDefault_ReturnsValue()
    {
        var option = Option<int>.Some(42);
        Assert.Equal(42, option.ValueOrDefault);
    }

    [Fact]
    public void None_HasValue_ReturnsFalse()
    {
        var option = Option<int>.None();
        Assert.False(option.HasValue);
    }

    [Fact]
    public void None_ValueOrDefault_ReturnsDefault()
    {
        var option = Option<int>.None();
        Assert.Equal(0, option.ValueOrDefault);
    }

    [Fact]
    public void OrDefault_WithSome_ReturnsValue()
    {
        var option = Option<string>.Some("hello");
        Assert.Equal("hello", option.OrDefault("fallback"));
    }

    [Fact]
    public void OrDefault_WithNone_ReturnsFallback()
    {
        var option = Option<string>.None();
        Assert.Equal("fallback", option.OrDefault("fallback"));
    }

    [Fact]
    public void OrDefault_WithNone_NoFallback_ReturnsNull()
    {
        var option = Option<string>.None();
        Assert.Null(option.OrDefault());
    }

    [Fact]
    public void OrThrow_WithSome_ReturnsValue()
    {
        var option = Option<int>.Some(42);
        Assert.Equal(42, option.OrThrow());
    }

    [Fact]
    public void OrThrow_WithNone_Throws()
    {
        var option = Option<int>.None();
        var ex = Assert.Throws<InvalidOperationException>(() => option.OrThrow());
        Assert.Equal("No value.", ex.Message);
    }

    [Fact]
    public void OrThrow_WithNone_CustomMessage_Throws()
    {
        var option = Option<int>.None();
        var ex = Assert.Throws<InvalidOperationException>(() => option.OrThrow("Custom error"));
        Assert.Equal("Custom error", ex.Message);
    }

    [Fact]
    public void Map_WithSome_TransformsValue()
    {
        var option = Option<int>.Some(5);
        var mapped = option.Map(x => x * 2);

        Assert.True(mapped.HasValue);
        Assert.Equal(10, mapped.ValueOrDefault);
    }

    [Fact]
    public void Map_WithNone_ReturnsNone()
    {
        var option = Option<int>.None();
        var mapped = option.Map(x => x * 2);

        Assert.False(mapped.HasValue);
        Assert.Equal(0, mapped.ValueOrDefault);
    }

    [Fact]
    public void Map_ChainedTransformations()
    {
        var option = Option<int>.Some(5);
        var result = option
            .Map(x => x * 2)
            .Map(x => x.ToString())
            .Map(s => $"Result: {s}")
            .OrDefault("nothing");

        Assert.Equal("Result: 10", result);
    }

    [Fact]
    public void Some_WithNull_StillHasValue()
    {
        var option = Option<string>.Some(null);
        Assert.True(option.HasValue);
        Assert.Null(option.ValueOrDefault);
    }

    [Fact]
    public void Map_WithNullResult_ReturnsSomeNull()
    {
        var option = Option<string>.Some("test");
        var mapped = option.Map<string?>(_ => null);

        Assert.True(mapped.HasValue);
        Assert.Null(mapped.ValueOrDefault);
    }

    [Fact]
    public void Some_ValueType_Works()
    {
        var option = Option<DateTime>.Some(DateTime.UnixEpoch);
        Assert.True(option.HasValue);
        Assert.Equal(DateTime.UnixEpoch, option.ValueOrDefault);
    }

    [Fact]
    public void None_ValueType_ReturnsDefaultValue()
    {
        var option = Option<DateTime>.None();
        Assert.False(option.HasValue);
        Assert.Equal(default(DateTime), option.ValueOrDefault);
    }

    [Fact]
    public void Map_TypeChange_Works()
    {
        var option = Option<int>.Some(42);
        var mapped = option.Map(x => new { Value = x });

        Assert.True(mapped.HasValue);
        Assert.Equal(42, mapped.ValueOrDefault!.Value);
    }

    [Fact]
    public void OrDefault_WithValue_IgnoresFallback()
    {
        var option = Option<int>.Some(42);
        Assert.Equal(42, option.OrDefault(999));
    }

    [Fact]
    public void OrThrow_Null_Message_Uses_Default()
    {
        var option = Option<int>.None();
        var ex = Assert.Throws<InvalidOperationException>(() => option.OrThrow(null));
        Assert.Equal("No value.", ex.Message);
    }
}
