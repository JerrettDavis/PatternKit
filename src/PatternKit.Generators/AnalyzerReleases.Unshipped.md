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
PKCOM001 | PatternKit.Generators.Composer | Error | ComposerGenerator
PKCOM002 | PatternKit.Generators.Composer | Error | ComposerGenerator
PKCOM003 | PatternKit.Generators.Composer | Error | ComposerGenerator
PKCOM004 | PatternKit.Generators.Composer | Error | ComposerGenerator
PKCOM005 | PatternKit.Generators.Composer | Error | ComposerGenerator
PKCOM006 | PatternKit.Generators.Composer | Error | ComposerGenerator
PKCOM007 | PatternKit.Generators.Composer | Error | ComposerGenerator
PKCOM008 | PatternKit.Generators.Composer | Error | ComposerGenerator
PKCOM009 | PatternKit.Generators.Composer | Warning | ComposerGenerator
PKFAC001 | PatternKit.Generators.Facade | Error | Diagnostics
PKFAC002 | PatternKit.Generators.Facade | Error | Diagnostics
PKFAC003 | PatternKit.Generators.Facade | Warning | Diagnostics
PKFAC004 | PatternKit.Generators.Facade | Warning | Diagnostics
PKFAC005 | PatternKit.Generators.Facade | Error | Diagnostics
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
PKPRX001 | PatternKit.Generators.Proxy | Error | ProxyGenerator
PKPRX002 | PatternKit.Generators.Proxy | Error | ProxyGenerator
PKPRX003 | PatternKit.Generators.Proxy | Warning | ProxyGenerator
PKPRX004 | PatternKit.Generators.Proxy | Error | ProxyGenerator
PKPRX005 | PatternKit.Generators.Proxy | Warning | ProxyGenerator
PKTMP001 | PatternKit.Generators.Template | Error | TemplateGenerator
PKTMP002 | PatternKit.Generators.Template | Error | TemplateGenerator
PKTMP003 | PatternKit.Generators.Template | Error | TemplateGenerator
PKTMP004 | PatternKit.Generators.Template | Error | TemplateGenerator
PKTMP005 | PatternKit.Generators.Template | Error | TemplateGenerator
PKTMP007 | PatternKit.Generators.Template | Warning | TemplateGenerator
PKTMP008 | PatternKit.Generators.Template | Error | TemplateGenerator
PKVIS001 | PatternKit.Generators.Visitor | Warning | No concrete types found for visitor generation
PKVIS002 | PatternKit.Generators.Visitor | Error | Type must be partial for Accept method generation
PKVIS004 | PatternKit.Generators.Visitor | Error | Derived type must be partial for Accept method generation
PKFCD001 | PatternKit.Generators.Facade | Error | Type must be partial for facade generation
PKFCD002 | PatternKit.Generators.Facade | Error | No mapped method found for contract member
PKFCD003 | PatternKit.Generators.Facade | Error | Multiple mappings found for contract member
PKFCD004 | PatternKit.Generators.Facade | Error | Signature mismatch between map method and contract member
PKFCD005 | PatternKit.Generators.Facade | Error | Facade type name conflicts with existing type
PKFCD006 | PatternKit.Generators.Facade | Warning | Async mapping detected but generation disabled
PKDEC001 | PatternKit.Generators.Decorator | Error | Unsupported target type for decorator generation
PKDEC002 | PatternKit.Generators.Decorator | Error | Unsupported member kind for decorator generation
PKDEC003 | PatternKit.Generators.Decorator | Error | Name conflict for generated decorator types
PKDEC004 | PatternKit.Generators.Decorator | Warning | Member is not accessible for decorator generation
PKDEC005 | PatternKit.Generators.Decorator | Error | Generic contracts are not supported for decorator generation
PKDEC006 | PatternKit.Generators.Decorator | Error | Nested types are not supported for decorator generation
PKPRO001 | PatternKit.Generators.Prototype | Error | Type marked with [Prototype] must be partial
PKPRO002 | PatternKit.Generators.Prototype | Error | Cannot construct clone target (no supported clone construction path)
PKPRO003 | PatternKit.Generators.Prototype | Warning | Unsafe reference capture (mutable reference types)
PKPRO004 | PatternKit.Generators.Prototype | Error | Requested Clone strategy but no clone mechanism found
PKPRO005 | PatternKit.Generators.Prototype | Error | Custom strategy requires partial clone hook, but none found
PKPRO006 | PatternKit.Generators.Prototype | Warning | Include/Ignore attribute misuse
PKPRO007 | PatternKit.Generators.Prototype | Error | DeepCopy strategy not yet implemented
PKPRO008 | PatternKit.Generators.Prototype | Error | Generic types not supported for Prototype pattern
PKPRO009 | PatternKit.Generators.Prototype | Error | Nested types not supported for Prototype pattern
PKPRO010 | PatternKit.Generators.Prototype | Error | Abstract types not supported for Prototype pattern
PKSNG001 | PatternKit.Generators.Singleton | Error | Type marked with [Singleton] must be partial
PKSNG002 | PatternKit.Generators.Singleton | Error | Singleton type must be a class
PKSNG003 | PatternKit.Generators.Singleton | Error | No usable constructor or factory method found
PKSNG004 | PatternKit.Generators.Singleton | Error | Multiple [SingletonFactory] methods found
PKSNG005 | PatternKit.Generators.Singleton | Warning | Public constructor detected
PKSNG006 | PatternKit.Generators.Singleton | Error | Instance property name conflicts with existing member
PKSNG007 | PatternKit.Generators.Singleton | Error | Generic types are not supported
PKSNG008 | PatternKit.Generators.Singleton | Error | Nested types are not supported
PKSNG009 | PatternKit.Generators.Singleton | Error | Invalid instance property name
PKSNG010 | PatternKit.Generators.Singleton | Error | Abstract types not supported for Singleton pattern
PKADP001 | PatternKit.Generators.Adapter | Error | Adapter host must be static partial
PKADP002 | PatternKit.Generators.Adapter | Error | Target must be interface or abstract class
PKADP003 | PatternKit.Generators.Adapter | Error | Missing mapping for target member
PKADP004 | PatternKit.Generators.Adapter | Error | Multiple mappings found for target member
PKADP005 | PatternKit.Generators.Adapter | Error | Mapping method signature mismatch
PKADP006 | PatternKit.Generators.Adapter | Error | Adapter type name conflicts with existing type
PKADP007 | PatternKit.Generators.Adapter | Error | Invalid adaptee type (must be concrete)
PKADP008 | PatternKit.Generators.Adapter | Error | Mapping method must be static
PKADP009 | PatternKit.Generators.Adapter | Error | Events are not supported
PKADP010 | PatternKit.Generators.Adapter | Error | Generic methods are not supported
PKADP011 | PatternKit.Generators.Adapter | Error | Overloaded methods are not supported
PKADP012 | PatternKit.Generators.Adapter | Error | Abstract class target requires accessible parameterless constructor
PKADP013 | PatternKit.Generators.Adapter | Error | Settable properties are not supported
PKADP014 | PatternKit.Generators.Adapter | Error | Nested or generic host not supported
PKADP015 | PatternKit.Generators.Adapter | Error | Mapping method must be accessible
PKADP016 | PatternKit.Generators.Adapter | Error | Static members are not supported
PKADP017 | PatternKit.Generators.Adapter | Error | Ref-return members are not supported
PKADP018 | PatternKit.Generators.Adapter | Error | Indexers are not supported
PKST001 | PatternKit.Generators.State | Error | Type marked with [StateMachine] must be partial
PKST002 | PatternKit.Generators.State | Error | State type must be an enum
PKST003 | PatternKit.Generators.State | Error | Trigger type must be an enum
PKST004 | PatternKit.Generators.State | Error | Duplicate transition detected
PKST005 | PatternKit.Generators.State | Error | Transition method signature invalid
PKST006 | PatternKit.Generators.State | Error | Guard method signature invalid
PKST007 | PatternKit.Generators.State | Error | Entry/Exit hook signature invalid
PKST008 | PatternKit.Generators.State | Warning | Async method detected but async generation disabled
PKST009 | PatternKit.Generators.State | Error | Generic types not supported for State pattern
PKST010 | PatternKit.Generators.State | Error | Nested types not supported for State pattern
