; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
PKCF001 | PatternKit.FactoryClass | Error | Diagnostics
PKCF002 | PatternKit.FactoryClass | Error | Diagnostics
PKCF003 | PatternKit.FactoryClass | Error | Diagnostics
PKCF004 | PatternKit.FactoryClass | Error | Diagnostics
PKCF005 | PatternKit.FactoryClass | Error | Diagnostics
PKCF006 | PatternKit.FactoryClass | Error | Diagnostics
PKKF001 | PatternKit.FactoryMethod | Error | Diagnostics
PKKF002 | PatternKit.FactoryMethod | Error | Diagnostics
PKKF003 | PatternKit.FactoryMethod | Error | Diagnostics
PKKF004 | PatternKit.FactoryMethod | Error | Diagnostics
PKKF005 | PatternKit.FactoryMethod | Error | Diagnostics
PKKF006 | PatternKit.FactoryMethod | Error | Diagnostics
B001 | PatternKit.Builders | Error | Diagnostics
B002 | PatternKit.Builders | Error | Diagnostics
B003 | PatternKit.Builders | Error | Diagnostics
B004 | PatternKit.Builders | Error | Diagnostics
B005 | PatternKit.Builders | Warning | Diagnostics
B006 | PatternKit.Builders | Warning | Diagnostics
BR001 | PatternKit.Builders | Warning | Diagnostics
BR002 | PatternKit.Builders | Warning | Diagnostics
BR003 | PatternKit.Builders | Warning | Diagnostics
BP001 | PatternKit.Builders | Error | Diagnostics
BP002 | PatternKit.Builders | Error | Diagnostics
BP003 | PatternKit.Builders | Error | Diagnostics
BA001 | PatternKit.Builders | Warning | Diagnostics
BA002 | PatternKit.Builders | Warning | Diagnostics
PKMEM001 | PatternKit.Generators.Memento | Error | Type marked with [Memento] must be partial
PKMEM002 | PatternKit.Generators.Memento | Warning | Member is inaccessible for memento capture or restore
PKMEM003 | PatternKit.Generators.Memento | Warning | Unsafe reference capture
PKMEM004 | PatternKit.Generators.Memento | Error | Clone strategy requested but mechanism missing
PKMEM005 | PatternKit.Generators.Memento | Error | Record restore generation failed
PKMEM006 | PatternKit.Generators.Memento | Info | Init-only or readonly restrictions prevent in-place restore
PKVIS001 | PatternKit.Generators.Visitor | Warning | No concrete types found for visitor generation
PKVIS002 | PatternKit.Generators.Visitor | Error | Type must be partial for Accept method generation
PKVIS004 | PatternKit.Generators.Visitor | Error | Derived type must be partial for Accept method generation
PKFCD001 | PatternKit.Generators.Facade | Error | Type must be partial for facade generation
PKFCD002 | PatternKit.Generators.Facade | Error | No mapped method found for contract member
PKFCD003 | PatternKit.Generators.Facade | Error | Multiple mappings found for contract member
PKFCD004 | PatternKit.Generators.Facade | Error | Signature mismatch between map method and contract member
PKFCD005 | PatternKit.Generators.Facade | Error | Facade type name conflicts with existing type
PKFCD006 | PatternKit.Generators.Facade | Warning | Async mapping detected but generation disabled
