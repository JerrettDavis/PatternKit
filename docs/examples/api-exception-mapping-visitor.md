# Visitor — API Exception Mapping (ASP.NET Core)

Map exceptions to HTTP responses with a result visitor. Centralize error policy without scattering `catch` blocks across controllers.

---

## Goal

- Convert exceptions to `ProblemDetails` or `IResult`.
- Keep mappings in one place; add specialized cases without touching controllers.
- Share a single, immutable visitor via DI.

---

## Exception Types

```csharp
public sealed class NotFoundException(string resource, string id) : Exception($"{resource} '{id}' not found");
public sealed class ValidationException(IDictionary<string, string[]> errors) : Exception("Validation failed");
public sealed class ForbiddenException(string? reason = null) : Exception(reason ?? "Forbidden");
```

---

## Visitor Registration

```csharp
// Program.cs or composition root
builder.Services.AddSingleton<Visitor<Exception, IResult>>(_ =>
    Visitor<Exception, IResult>
        .Create()
        .On<NotFoundException>(ex => Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            detail: ex.Message))
        .On<ValidationException>(ex => Results.ValidationProblem(
            errors: ex.Errors.ToDictionary(kv => kv.Key, kv => kv.Value)))
        .On<ForbiddenException>(ex => Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden",
            detail: ex.Message))
        .Default(ex => Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Server Error",
            detail: builder.Environment.IsDevelopment() ? ex.ToString() : ex.Message))
        .Build());
```

---

## Middleware

```csharp
public sealed class ExceptionMappingMiddleware(
    RequestDelegate next,
    Visitor<Exception, IResult> mapper,
    ILogger<ExceptionMappingMiddleware> log)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unhandled exception");
            var result = mapper.Visit(ex);
            await result.ExecuteAsync(ctx);
        }
    }
}

// Program.cs
app.UseMiddleware<ExceptionMappingMiddleware>();
```

---

## Why This Works Well

- The mapping is explicit and ordered; specific exceptions come first.
- A default keeps APIs resilient to unknown errors.
- The visitor instance is immutable and safe to reuse across requests.

---

## Tests (sketch)

```csharp
[Fact]
public void Maps_NotFound_To_404()
{
    var v = BuildMapper();
    var res = v.Visit(new NotFoundException("Order", "123"));
    // Assert res is Problem with 404...
}
```

