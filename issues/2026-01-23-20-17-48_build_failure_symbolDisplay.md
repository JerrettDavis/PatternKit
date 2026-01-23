---
Title: Build failure: SymbolDisplay does not exist in DecoratorGenerator.cs
Body: ## Description
The CI build is failing with a compilation error in `DecoratorGenerator.cs` at line 663.

## Error Details
```
error CS0103: The name 'SymbolDisplay' does not exist in the current context
[/home/runner/work/PatternKit/PatternKit/src/PatternKit.Generators/PatternKit.Generators.csproj]
```

**Location**: `src/PatternKit.Generators/DecoratorGenerator.cs(663,16)`

**Failed Job**: https://github.com/JerrettDavis/PatternKit/actions/runs/21299756617/job/61314298472

**Commit**: 0ec123a9a7f8d661acb920bbe8b89e49e1c9d399

## Root Cause
The `SymbolDisplay` class is being used on line 663, but it appears the project may be missing the appropriate reference to the Roslyn package that provides this class, or there's an issue with the package reference configuration.

## Code Reference
```csharp
// Line 663 in DecoratorGenerator.cs
return SymbolDisplay.FormatPrimitive(param.ExplicitDefaultValue, quoteStrings: true, useHexadecimalNumbers: false);
```

While `using Microsoft.CodeAnalysis;` is present at the top of the file, the `SymbolDisplay` class may not be available in the current compilation context.

## Suggested Solution
Verify that the `PatternKit.Generators.csproj` file includes the appropriate package reference:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" PrivateAssets="all" />
</ItemGroup>
```

Ensure the version is compatible with the target framework and other dependencies in the project.

## Additional Context
- This is preventing the build from completing successfully
- The error appeared during the pack step of the CI workflow
- Three warnings also present but not blocking (CS0436, RS1032)