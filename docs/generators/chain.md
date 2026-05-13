# Chain Generator

The Chain generator emits deterministic responsibility-chain dispatch for partial chain hosts.

## Usage

```csharp
using PatternKit.Generators.Chain;

[Chain]
public partial class Router
{
    [ChainHandler(Order = 0)]
    private bool TryHealth(in Request request, out Response response)
    {
        response = new Response(200);
        return request.Path == "/health";
    }

    [ChainDefault]
    private Response NotFound(in Request request) => new(404);
}
```

The generated `TryHandle` method invokes handlers by ascending `Order` and stops at the first handler that returns `true`. The generated `Handle` method returns that value or calls the `[ChainDefault]` fallback.

## Diagnostics

- `PKCH001`: chain host must be partial.
- `PKCH002`: no handlers found.
- `PKCH003`: duplicate handler order.
- `PKCH004`: handler signature invalid.
- `PKCH005`: pipeline terminal missing.
- `PKCH006`: multiple pipeline terminals.
- `PKCH007`: default fallback missing.
