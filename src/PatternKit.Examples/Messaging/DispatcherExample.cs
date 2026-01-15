using PatternKit.Generators.Messaging;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: GenerateDispatcher(
    Namespace = "PatternKit.Examples.Messaging",
    Name = "ExampleDispatcher",
    IncludeStreaming = true,
    IncludeObjectOverloads = false,
    Visibility = GeneratedVisibility.Public)]

namespace PatternKit.Examples.Messaging;

// Command examples
public record CreateUser(string Username, string Email);
public record UserCreated(int UserId, string Username);

public record SendEmail(string To, string Subject, string Body);
public record EmailSent(bool Success, string MessageId);

// Notification examples
public record UserRegistered(int UserId, string Username, string Email);
public record OrderPlaced(int OrderId, decimal Total);

// Stream examples
public record SearchQuery(string Term, int MaxResults);
public record SearchResult(string Title, string Url, double Relevance);

public record PagedRequest(int PageNumber, int PageSize);
public record PagedItem(int Id, string Name);

/// <summary>
/// Examples demonstrating the Source-Generated Mediator pattern.
/// The Mediator pattern reduces coupling by centralizing communication between components.
/// This source-generated variant provides zero runtime dependencies on PatternKit.
/// </summary>
public static class DispatcherUsageExamples
{
    public static async Task BasicCommandExample()
    {
        var dispatcher = ExampleDispatcher.Create()
            .Command<CreateUser, UserCreated>((req, ct) =>
                new ValueTask<UserCreated>(new UserCreated(1, req.Username)))
            .Build();

        var result = await dispatcher.Send<CreateUser, UserCreated>(
            new CreateUser("alice", "alice@example.com"),
            default);

        System.Console.WriteLine($"User created: {result.UserId} - {result.Username}");
    }

    public static async Task NotificationExample()
    {
        var log = new List<string>();

        var dispatcher = ExampleDispatcher.Create()
            .Notification<UserRegistered>((n, ct) =>
            {
                log.Add($"Sending welcome email to {n.Email}");
                return ValueTask.CompletedTask;
            })
            .Notification<UserRegistered>((n, ct) =>
            {
                log.Add($"Adding user {n.Username} to mailing list");
                return ValueTask.CompletedTask;
            })
            .Notification<UserRegistered>((n, ct) =>
            {
                log.Add($"Logging registration for user {n.UserId}");
                return ValueTask.CompletedTask;
            })
            .Build();

        await dispatcher.Publish(
            new UserRegistered(1, "alice", "alice@example.com"),
            default);

        System.Console.WriteLine($"Handled {log.Count} notification handlers");
    }

    public static async Task StreamExample()
    {
        var dispatcher = ExampleDispatcher.Create()
            .Stream<SearchQuery, SearchResult>(SearchAsync)
            .Build();

        await foreach (var result in dispatcher.Stream<SearchQuery, SearchResult>(
            new SearchQuery("pattern", 10),
            default))
        {
            System.Console.WriteLine($"Result: {result.Title} ({result.Relevance:F2})");
        }
    }

    private static async IAsyncEnumerable<SearchResult> SearchAsync(
        SearchQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Simulate search results
        for (int i = 0; i < query.MaxResults && i < 5; i++)
        {
            await Task.Delay(10, ct); // Simulate async work
            yield return new SearchResult(
                $"Result {i + 1} for '{query.Term}'",
                $"https://example.com/result-{i + 1}",
                1.0 - (i * 0.1));
        }
    }

    public static async Task PipelineExample()
    {
        var log = new List<string>();

        var dispatcher = ExampleDispatcher.Create()
            .Pre<SendEmail>((req, ct) =>
            {
                log.Add($"Pre: Validating email to {req.To}");
                return ValueTask.CompletedTask;
            })
            .Command<SendEmail, EmailSent>((req, ct) =>
            {
                log.Add($"Handler: Sending email to {req.To}");
                return new ValueTask<EmailSent>(new EmailSent(true, "msg-123"));
            })
            .Post<SendEmail, EmailSent>((req, res, ct) =>
            {
                log.Add($"Post: Email sent successfully: {res.MessageId}");
                return ValueTask.CompletedTask;
            })
            .Build();

        await dispatcher.Send<SendEmail, EmailSent>(
            new SendEmail("user@example.com", "Welcome", "Hello!"),
            default);

        System.Console.WriteLine("Pipeline executed:");
        foreach (var entry in log)
        {
            System.Console.WriteLine($"  - {entry}");
        }
    }
}
