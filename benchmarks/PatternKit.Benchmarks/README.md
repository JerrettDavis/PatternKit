# PatternKit Benchmarks

This project contains reportable BenchmarkDotNet comparisons for PatternKit fluent builders and source-generated factories.

Run the full benchmark suite from the repository root:

```powershell
dotnet run -c Release --project benchmarks/PatternKit.Benchmarks -- --artifacts artifacts/benchmarks
```

Run one pattern family:

```powershell
dotnet run -c Release --project benchmarks/PatternKit.Benchmarks -- --filter *LeaderElection* --artifacts artifacts/benchmarks
```

Every benchmark class must:

- Compare fluent and source-generated routes in the same benchmark class.
- Split construction/setup overhead from execution overhead where the pattern has meaningful runtime work.
- Keep realistic domain names and payloads so benchmark results map back to production examples.
- Emit memory diagnostics and markdown, CSV, and JSON reports through the shared benchmark config.
- Use BenchmarkDotNet categories for the pattern family, pattern name, route, and benchmark phase.
