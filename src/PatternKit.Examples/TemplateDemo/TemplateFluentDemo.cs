using PatternKit.Behavioral.Template;

namespace PatternKit.Examples.TemplateDemo;

// Fluent Template demo: word count with logging and error handling
public static class TemplateFluentDemo
{
    public static void Run()
    {
        var tpl = Template<string, int>
            .Create(ctx => ctx.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)
            .Before(ctx => Console.WriteLine($"[Before] Input: '{ctx}'"))
            .After((ctx, res) => Console.WriteLine($"[After] '{ctx}' -> words: {res}"))
            .OnError((ctx, err) => Console.WriteLine($"[Error] Input '{ctx}', error: {err}"))
            .Synchronized() // demonstrate optional synchronization
            .Build();

        var ok = tpl.TryExecute("The quick brown fox", out var result, out var error);
        Console.WriteLine(ok ? $"Word count: {result}" : $"Failed: {error}");
    }
}