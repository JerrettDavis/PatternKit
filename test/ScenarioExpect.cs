using System.Collections;
using System.Text.RegularExpressions;
using TinyBDD.Assertions;

public static class ScenarioExpect
{
    public static void True(bool condition, string? message = null)
    {
        if (!condition)
            Fail(message ?? "expected condition to be true");
    }

    public static void True(bool? condition, string? message = null)
    {
        if (condition != true)
            Fail(message ?? $"expected condition to be true, but was {Format(condition)}");
    }

    public static void False(bool condition, string? message = null)
    {
        if (condition)
            Fail(message ?? "expected condition to be false");
    }

    public static void False(bool? condition, string? message = null)
    {
        if (condition != false)
            Fail(message ?? $"expected condition to be false, but was {Format(condition)}");
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (BothEnumerable(expected, actual, out var expectedItems, out var actualItems))
        {
            EqualSequences(expectedItems, actualItems);
            return;
        }

        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            Throw("value", expected, actual);
    }

    public static void Equal<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        => EqualSequences(expected.Cast<object?>(), actual.Cast<object?>());

    public static void NotEqual<T>(T notExpected, T actual)
    {
        if (EqualityComparer<T>.Default.Equals(notExpected, actual))
            Fail($"expected value to not equal {Format(notExpected)}, but it did");
    }

    public static void Null(object? value)
        => Expect.That(value, "value").ToBeNull().EvaluateAsync().GetAwaiter().GetResult();

    public static T NotNull<T>(T? value)
    {
        Expect.That(value, "value").ToNotBeNull().EvaluateAsync().GetAwaiter().GetResult();
        return value!;
    }

    public static void Empty(IEnumerable collection)
    {
        if (collection.Cast<object?>().Any())
            Fail("expected collection to be empty");
    }

    public static void NotEmpty(IEnumerable collection)
    {
        if (!collection.Cast<object?>().Any())
            Fail("expected collection to not be empty");
    }

    public static T Single<T>(IEnumerable<T> collection)
    {
        var items = collection.Take(2).ToArray();
        if (items.Length != 1)
            Fail($"expected collection to contain exactly one item, but it contained {collection.Count()}");
        return items[0];
    }

    public static T Single<T>(IEnumerable<T> collection, Func<T, bool> predicate)
    {
        var matches = collection.Where(predicate).Take(2).ToArray();
        if (matches.Length != 1)
            Fail($"expected collection to contain exactly one matching item, but it contained {collection.Count(predicate)}");
        return matches[0];
    }

    public static void All<T>(IEnumerable<T> collection, Action<T> assertion)
    {
        var index = 0;
        foreach (var item in collection)
        {
            try
            {
                assertion(item);
            }
            catch (Exception ex) when (ex is not TinyBddAssertionException)
            {
                throw new TinyBddAssertionException($"expected all items to pass assertion, but item {index} threw {ex.GetType().Name}: {ex.Message}", ex);
            }

            index++;
        }
    }

    public static void Contains(string expectedSubstring, string? actualString)
        => Contains(expectedSubstring, actualString, StringComparison.Ordinal);

    public static void Contains(string expectedSubstring, string? actualString, StringComparison comparison)
    {
        if (actualString is null || actualString.IndexOf(expectedSubstring, comparison) < 0)
            Fail($"expected string to contain {Format(expectedSubstring)}, but was {Format(actualString)}");
    }

    public static void Contains<T>(T expected, IEnumerable<T> collection)
    {
        if (!collection.Contains(expected))
            Fail($"expected collection to contain {Format(expected)}");
    }

    public static T Contains<T>(IEnumerable<T> collection, Func<T, bool> predicate)
    {
        foreach (var item in collection)
        {
            if (predicate(item))
                return item;
        }

        Fail("expected collection to contain a matching item");
        return default!;
    }

    public static void DoesNotContain(string expectedSubstring, string? actualString)
    {
        if (actualString?.Contains(expectedSubstring, StringComparison.Ordinal) == true)
            Fail($"expected string to not contain {Format(expectedSubstring)}, but was {Format(actualString)}");
    }

    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection)
    {
        if (collection.Contains(expected))
            Fail($"expected collection to not contain {Format(expected)}");
    }

    public static void DoesNotContain<T>(IEnumerable<T> collection, Func<T, bool> predicate)
    {
        if (collection.Any(predicate))
            Fail("expected collection to not contain a matching item");
    }

    public static void StartsWith(string expectedStart, string? actualString)
        => StartsWith(expectedStart, actualString, StringComparison.Ordinal);

    public static void StartsWith(string expectedStart, string? actualString, StringComparison comparison)
    {
        if (actualString is null || !actualString.StartsWith(expectedStart, comparison))
            Fail($"expected string to start with {Format(expectedStart)}, but was {Format(actualString)}");
    }

    public static void Matches(string expectedRegexPattern, string? actualString)
    {
        if (actualString is null || !Regex.IsMatch(actualString, expectedRegexPattern))
            Fail($"expected string to match /{expectedRegexPattern}/, but was {Format(actualString)}");
    }

    public static void Same(object? expected, object? actual)
    {
        if (!ReferenceEquals(expected, actual))
            Fail("expected references to be the same instance");
    }

    public static void NotSame(object? notExpected, object? actual)
    {
        if (ReferenceEquals(notExpected, actual))
            Fail("expected references to be different instances");
    }

    public static void InRange<T>(T actual, T low, T high) where T : IComparable<T>
    {
        if (actual.CompareTo(low) < 0 || actual.CompareTo(high) > 0)
            Fail($"expected {Format(actual)} to be in range {Format(low)}..{Format(high)}");
    }

    public static TExpected IsType<TExpected>(object? value)
    {
        if (value is not null && value.GetType() == typeof(TExpected))
            return (TExpected)value;

        if (value is null)
            Fail($"expected value to be of type {typeof(TExpected).Name}, but was null");
        else
            Fail($"expected value to be of type {typeof(TExpected).Name}, but was {value?.GetType().Name ?? "null"}");

        return default!;
    }

    public static TExpected IsAssignableFrom<TExpected>(object? value)
    {
        if (value is TExpected typed)
            return typed;

        if (value is null)
            Fail($"expected value to be assignable to {typeof(TExpected).Name}, but was null");
        else
            Fail($"expected value to be assignable to {typeof(TExpected).Name}, but was {value?.GetType().Name ?? "null"}");

        return default!;
    }

    public static TException Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            Fail($"expected {typeof(TException).Name}, but threw {ex.GetType().Name}: {ex.Message}");
        }

        Fail($"expected {typeof(TException).Name}, but no exception was thrown");
        return null!;
    }

    public static TException Throws<TException, TResult>(Func<TResult> action) where TException : Exception
        => Throws<TException>(() => _ = action());

    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            Fail($"expected {typeof(TException).Name}, but threw {ex.GetType().Name}: {ex.Message}");
        }

        Fail($"expected {typeof(TException).Name}, but no exception was thrown");
        return null!;
    }

    public static async Task<TException> ThrowsAnyAsync<TException>(Func<Task> action) where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            Fail($"expected {typeof(TException).Name} or derived type, but threw {ex.GetType().Name}: {ex.Message}");
        }

        Fail($"expected {typeof(TException).Name} or derived type, but no exception was thrown");
        return null!;
    }

    public static void Fail(string? message = null)
        => throw new TinyBddAssertionException(message ?? "assertion failed");

    private static void EqualSequences(IEnumerable<object?> expected, IEnumerable<object?> actual)
    {
        var expectedArray = expected.ToArray();
        var actualArray = actual.ToArray();
        if (expectedArray.Length != actualArray.Length)
            Fail($"expected collection count {expectedArray.Length}, but was {actualArray.Length}");

        for (var i = 0; i < expectedArray.Length; i++)
        {
            if (!Equals(expectedArray[i], actualArray[i]))
                Fail($"expected collection item {i} to be {Format(expectedArray[i])}, but was {Format(actualArray[i])}");
        }
    }

    private static bool BothEnumerable<T>(T expected, T actual, out IEnumerable<object?> expectedItems, out IEnumerable<object?> actualItems)
    {
        if (expected is IEnumerable expectedEnumerable && actual is IEnumerable actualEnumerable && expected is not string && actual is not string)
        {
            expectedItems = expectedEnumerable.Cast<object?>();
            actualItems = actualEnumerable.Cast<object?>();
            return true;
        }

        expectedItems = [];
        actualItems = [];
        return false;
    }

    private static void Throw(string subject, object? expected, object? actual)
        => throw new TinyBddAssertionException($"expected {subject} to be {Format(expected)}, but was {Format(actual)}")
        {
            Expected = expected,
            Actual = actual,
            Subject = subject
        };

    private static string Format(object? value)
        => value switch
        {
            null => "null",
            string text => $"\"{text}\"",
            _ => value.ToString() ?? string.Empty
        };
}
