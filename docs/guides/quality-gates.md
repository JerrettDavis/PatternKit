# Quality Gates

PatternKit changes should be validated as production library changes, not sample-only changes. Use these gates before opening or merging work.

## Required local validation

Restore and build the full solution:

```bash
dotnet restore PatternKit.slnx --use-lock-file
dotnet format PatternKit.slnx --verify-no-changes --verbosity minimal
dotnet build PatternKit.slnx --configuration Release --no-restore -m:1
```

Run the focused test suite for the area being changed. For broad changes, run the same project-by-project coverage shape used by CI:

```bash
for project in \
  test/PatternKit.Tests/PatternKit.Tests.csproj \
  test/PatternKit.Generators.Tests/PatternKit.Generators.Tests.csproj \
  test/PatternKit.Examples.Tests/PatternKit.Examples.Tests.csproj \
  test/PatternKit.Hosting.Extensions.Tests/PatternKit.Hosting.Extensions.Tests.csproj
do
  dotnet test "$project" \
    --configuration Release \
    --no-build \
    -p:TestTfmsInParallel=false \
    --collect:"XPlat Code Coverage" \
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura \
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include="[PatternKit*]*" \
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude="[*Tests]*" \
    -- RunConfiguration.TestSessionTimeout=1800000
done
```

Build documentation with warnings treated as failures:

```bash
docfx docs/docfx.json --warningsAsErrors
```

Check package currency:

```bash
dotnet list PatternKit.slnx package --outdated
```

The expected exception is `PatternKit.Generators`, which intentionally pins `Microsoft.CodeAnalysis.CSharp` with a project-level `VersionOverride` so the analyzer assembly remains compatible with the SDK compiler that loads it.

## Test expectations

Tests should be executable specifications. Use TinyBDD scenarios and `ScenarioExpect` assertion helpers instead of direct xUnit assertions.

Prefer this structure:

```csharp
[Scenario("A policy rejects invalid registration input")]
[Fact]
public Task Policy_Rejects_Invalid_Registration_Input()
    => Given("invalid registration input", () => new ServiceCollection())
        .When("registering the policy", services =>
            ScenarioExpect.Throws<ArgumentNullException>(
                () => services.AddPatternKitPriorityQueue<WorkItem, int>(null!)))
        .Then("the invalid dependency is named", exception =>
            ScenarioExpect.Equal("prioritySelector", exception.ParamName))
        .AssertPassed();
```

Every production pattern should have:

- Core fluent API coverage.
- Source-generator coverage when a generator route exists.
- Real-world example coverage in `PatternKit.Examples.Tests`.
- Documentation in the API docs, README table, or guide pages.
- Benchmark coverage that separates fluent and generated paths when both exist.
- `IServiceCollection` or host integration coverage when the pattern is naturally used through dependency injection.

## Formatting and static analysis

The repository includes a root `.editorconfig` so editors, `dotnet format`, and CI agree on basic C# layout and style. Run `dotnet format PatternKit.slnx --verify-no-changes --verbosity minimal` before opening a PR; CI enforces the same gate for pull requests and `main` releases.

Built-in .NET analyzers are enabled in `Recommended` mode for the production package projects:

- `PatternKit.Core`
- `PatternKit.Generators`
- `PatternKit.Generators.Abstractions`
- `PatternKit.Hosting.Extensions`

The current rule-family baseline is path-scoped in `.editorconfig`. Tighten it by removing one rule family at a time, fixing or documenting the production API impact, and validating the affected package with `/t:Rebuild`.

Do not enable `EnforceCodeStyleInBuild` at the solution level. `dotnet format` remains the repository style gate, and forcing code-style analysis through MSBuild currently prevents design-time workspace loading for examples and benchmarks that depend on PatternKit source generators. Keep source-generated consumers validated through the normal restore, format, build, test, docs, and benchmark gates instead of routing style diagnostics through generated-symbol design-time builds.
