# Repository Guidelines

## Project Structure & Module Organization
- `src/PatternKit.Core`: core fluent design‑patterns library (root namespace `PatternKit`).
- `src/PatternKit.Generators`: Roslyn source generators packaged with the solution.
- `src/PatternKit.Examples`: small example usages referenced by tests.
- `test/*`: xUnit test projects (`*.Tests`) covering core and generators.
- `docs/`: documentation and images. Solution: `PatternKit.slnx`; shared settings in `Directory.Build.props`.

## Build, Test, and Development Commands
- Restore: `dotnet restore --use-lock-file`
- Build: `dotnet build PatternKit.slnx -c Debug` (use `Release` for CI)
- Test with coverage: `dotnet test PatternKit.slnx -c Release --collect:"XPlat Code Coverage"`
- Pack NuGet: `dotnet pack PatternKit.slnx -c Release -o ./artifacts`

## Coding Style & Naming Conventions
- C# with 4‑space indentation, `Nullable` enabled, implicit usings.
- Target frameworks: `netstandard2.0`, `netstandard2.1`, `net8.0`, `net9.0`; ensure code compiles for all TFMs.
- Namespaces start with `PatternKit.*`; projects and tests follow `PatternKit.*` and `*.Tests`.
- Use PascalCase for types/members; camelCase for locals/parameters. Fluent APIs should read naturally in chains.
- XML docs aren’t required for CI (`NoWarn 1591`) but are preferred for public APIs.

## Testing Guidelines
- Frameworks: xUnit (+ TinyBDD where useful).
- Place tests under `test/<Project>.Tests`; name files to mirror the target type.
- Use clear, behavior‑focused test names (e.g., `StrategyBuilder_ShouldSelectCorrectImplementation`).
- Coverage is collected in CI and uploaded to Codecov; no fixed threshold enforced.

## Commit & Pull Request Guidelines
- Follow Conventional Commits: `feat(scope): summary`, `fix(generator): issue`, etc.
- Link issues/PRs in the description; include rationale and examples.
- Ensure `dotnet build` and `dotnet test` pass locally before opening a PR.
- Releases use GitVersion in CI; don’t manually bump package versions.

## Security & Configuration Tips
- Avoid APIs unavailable on `netstandard2.0` unless conditioned with `#if`.
- Don’t commit secrets; CI uses repository secrets for tokens.

## Agent-Specific Instructions
- Do not run `git commit`, `git push`, tag, or release commands. Draft commit messages and PR bodies; provide exact commands for maintainers to run.
- Never bump versions or publish packages locally; CI + GitVersion manage releases.
- If a change could be destructive (e.g., deleting files or history), propose the plan and commands instead of executing them.
