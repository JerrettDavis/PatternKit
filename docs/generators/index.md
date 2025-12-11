# PatternKit Generators

PatternKit includes a Roslyn incremental generator package (`PatternKit.Generators`) that emits factory code at compile time. Use it when you want predictable, allocation-light factories without writing the boilerplate by hand.

## When to use

- You already express intent with attributes and want the compiler to write the factory.
- You need both synchronous and `ValueTask`-first async entry points.
- You want deterministic codegen with no runtime dependency on PatternKit.

## Package & setup

1. Add the analyzer package to your project:

   ```bash
   dotnet add package PatternKit.Generators
   ```

2. Ensure the project targets `netstandard2.0+` (for libraries) or any modern .NET target (for apps). No runtime references are required.

3. Mark your types with the attributes below; the generator produces partial classes at compile time.

## Available generators

- **Factory Method** — Turn a `static partial` class into a keyed dispatcher with optional default behavior.
- **Factory Class** — GoF-style factory for an abstract base or interface, mapping keys to concrete products (with optional enum keys and async creation).

Use the pages in this section for usage, generated API shape, async rules, and diagnostics.
