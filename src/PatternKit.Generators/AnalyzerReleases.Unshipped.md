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
PKTS001 | PatternKit.Generators.TransactionScript | Error | Transaction Script host must be partial.
PKTS002 | PatternKit.Generators.TransactionScript | Error | Transaction Script must declare exactly one handler.
PKTS003 | PatternKit.Generators.TransactionScript | Error | Transaction Script handler signature is invalid.
PKTS004 | PatternKit.Generators.TransactionScript | Error | Transaction Script validator signature is invalid.
PKSL001 | PatternKit.Generators.ServiceLayer | Error | Service Layer host must be partial.
PKSL002 | PatternKit.Generators.ServiceLayer | Error | Service Layer operation must declare exactly one handler.
PKSL003 | PatternKit.Generators.ServiceLayer | Error | Service Layer handler signature is invalid.
PKSL004 | PatternKit.Generators.ServiceLayer | Error | Service Layer rule signature is invalid.
PKSL005 | PatternKit.Generators.ServiceLayer | Error | Service Layer rule order is duplicated.
PKUOW001 | PatternKit.Generators.UnitOfWork | Error | Unit of work host must be partial.
PKUOW002 | PatternKit.Generators.UnitOfWork | Error | Unit of work must declare at least one step.
PKUOW003 | PatternKit.Generators.UnitOfWork | Error | Unit of work step signature is invalid.
PKUOW004 | PatternKit.Generators.UnitOfWork | Error | Unit of work step name or order is duplicated.
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
PKMAP001 | PatternKit.Generators.DataMapping | Error | Data Mapper host must be partial.
PKMAP002 | PatternKit.Generators.DataMapping | Error | Data Mapper must declare exactly one projection in each direction.
PKMAP003 | PatternKit.Generators.DataMapping | Error | Data Mapper projection signature is invalid.
PKDE001 | PatternKit.Generators.DomainEvents | Error | Domain Event dispatcher host must be partial.
PKDE002 | PatternKit.Generators.DomainEvents | Error | Domain Event dispatcher must declare at least one handler.
PKDE003 | PatternKit.Generators.DomainEvents | Error | Domain Event handler signature is invalid.
PKDE004 | PatternKit.Generators.DomainEvents | Error | Domain Event handler order is duplicated.
PKAUD001 | PatternKit.Generators.AuditLog | Error | Audit Log host must be partial.
PKAUD002 | PatternKit.Generators.AuditLog | Error | Audit Log must declare exactly one key selector.
PKAUD003 | PatternKit.Generators.AuditLog | Error | Audit Log key selector signature is invalid.
PKMV001 | PatternKit.Generators.MaterializedViews | Error | Materialized View host must be partial.
PKMV002 | PatternKit.Generators.MaterializedViews | Error | Materialized View requires handlers.
PKMV003 | PatternKit.Generators.MaterializedViews | Error | Materialized View handler signature is invalid.
PKES001 | PatternKit.Generators.EventSourcing | Error | Event Store host must be partial.
PKFT001 | PatternKit.Generators.FeatureToggles | Error | Feature Toggle host must be partial.
PKFT002 | PatternKit.Generators.FeatureToggles | Error | Feature Toggle set must declare at least one rule.
PKFT003 | PatternKit.Generators.FeatureToggles | Error | Feature Toggle rule signature is invalid.
PKQL001 | PatternKit.Generators.QueueLoadLeveling | Error | Queue Load Leveling policy host must be partial.
PKQL002 | PatternKit.Generators.QueueLoadLeveling | Error | Queue Load Leveling policy configuration is invalid.
PKPQ001 | PatternKit.Generators.PriorityQueue | Error | Priority Queue host must be partial.
PKPQ002 | PatternKit.Generators.PriorityQueue | Error | Priority Queue must declare exactly one priority selector.
PKPQ003 | PatternKit.Generators.PriorityQueue | Error | Priority Queue priority selector signature is invalid.
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
PKACL001 | PatternKit.Generators.AntiCorruption | Error | Anti-corruption layer host must be partial.
PKACL002 | PatternKit.Generators.AntiCorruption | Error | Anti-corruption layer must declare exactly one translator.
PKACL003 | PatternKit.Generators.AntiCorruption | Error | Anti-corruption layer translator signature is invalid.
PKACL004 | PatternKit.Generators.AntiCorruption | Error | Anti-corruption layer validation rule signature is invalid.
PKBRG001 | PatternKit.Generators.Bridge | Error | Bridge abstraction must be partial
PKBRG002 | PatternKit.Generators.Bridge | Error | Bridge implementor must be an interface or abstract class
PKBRG003 | PatternKit.Generators.Bridge | Error | Implementor member is unsupported
PKBRG004 | PatternKit.Generators.Bridge | Error | Generated default abstraction name conflicts
PKCH001 | PatternKit.Generators.Chain | Error | Chain type must be partial
PKCH002 | PatternKit.Generators.Chain | Error | No chain handlers found
PKCH003 | PatternKit.Generators.Chain | Error | Duplicate chain handler order
PKCH004 | PatternKit.Generators.Chain | Error | Chain handler signature invalid
PKCH005 | PatternKit.Generators.Chain | Error | Pipeline terminal missing
PKCH006 | PatternKit.Generators.Chain | Error | Multiple pipeline terminals
PKCH007 | PatternKit.Generators.Chain | Error | Chain default missing
PKCMD001 | PatternKit.Generators.Command | Error | Command type must be partial
PKCMD002 | PatternKit.Generators.Command | Error | Command handler missing
PKCMD003 | PatternKit.Generators.Command | Error | Multiple command handlers
PKCMD004 | PatternKit.Generators.Command | Error | Command handler signature invalid
PKCMP001 | PatternKit.Generators.Composite | Error | Composite component must be partial
PKCMP002 | PatternKit.Generators.Composite | Error | Composite component target is invalid
PKCMP003 | PatternKit.Generators.Composite | Error | Generated Composite type name conflicts
PKCMP004 | PatternKit.Generators.Composite | Error | Composite contract member is unsupported
PKFLY001 | PatternKit.Generators.Flyweight | Error | Flyweight type must be partial
PKFLY002 | PatternKit.Generators.Flyweight | Error | Flyweight factory method missing
PKFLY003 | PatternKit.Generators.Flyweight | Error | Multiple flyweight factories
PKFLY004 | PatternKit.Generators.Flyweight | Error | Flyweight factory signature is invalid
PKFLY005 | PatternKit.Generators.Flyweight | Error | Flyweight cache type name conflicts
PKFLY006 | PatternKit.Generators.Flyweight | Error | Invalid flyweight eviction configuration
PKIT001 | PatternKit.Generators.Iterator | Error | Iterator type must be partial
PKIT002 | PatternKit.Generators.Iterator | Error | Iterator step missing
PKIT003 | PatternKit.Generators.Iterator | Error | Multiple iterator steps
PKIT004 | PatternKit.Generators.Iterator | Error | Iterator step signature invalid
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
PKOBS001 | PatternKit.Generators.Observer | Error | Type marked with [Observer] must be partial
PKOBS002 | PatternKit.Generators.Observer | Error | Unable to extract payload type from [Observer] attribute
PKOBS003 | PatternKit.Generators.Observer | Warning | Unsupported observer type or configuration
PKRS001 | PatternKit.Generators.Messaging | Error | Routing slip type must be partial
PKRS002 | PatternKit.Generators.Messaging | Error | Routing slip has no steps
PKRS003 | PatternKit.Generators.Messaging | Error | Routing slip step signature is invalid
PKSG001 | PatternKit.Generators.Messaging | Error | Saga type must be partial
PKSG002 | PatternKit.Generators.Messaging | Error | Saga has no steps
PKSG003 | PatternKit.Generators.Messaging | Error | Saga step signature is invalid
PKSG004 | PatternKit.Generators.Messaging | Error | Saga completion signature is invalid
PKCR001 | PatternKit.Generators.Messaging | Error | Content router type must be partial.
PKCR002 | PatternKit.Generators.Messaging | Error | Content router must declare at least one route.
PKCR003 | PatternKit.Generators.Messaging | Error | Content route handler or predicate signature is invalid.
PKCR004 | PatternKit.Generators.Messaging | Error | Content router default handler signature is invalid.
PKCR005 | PatternKit.Generators.Messaging | Error | Content router route name or order is duplicated.
PKCC001 | PatternKit.Generators.Messaging | Error | Claim check host must be partial.
PKCC002 | PatternKit.Generators.Messaging | Error | Claim check must declare exactly one store factory.
PKCC003 | PatternKit.Generators.Messaging | Error | Claim check store factory signature is invalid.
PKDL001 | PatternKit.Generators.Messaging | Error | Dead-letter channel host must be partial.
PKDL002 | PatternKit.Generators.Messaging | Error | Dead-letter channel must declare exactly one store factory.
PKDL003 | PatternKit.Generators.Messaging | Error | Dead-letter store factory signature is invalid.
PKME001 | PatternKit.Generators.Messaging | Error | Message envelope type must be partial.
PKME002 | PatternKit.Generators.Messaging | Error | Message envelope must declare at least one required header.
PKME003 | PatternKit.Generators.Messaging | Error | Message envelope header configuration is invalid.
PKME004 | PatternKit.Generators.Messaging | Error | Message envelope header name or generated parameter name is duplicated.
PKMT001 | PatternKit.Generators.Messaging | Error | Message translator host must be partial.
PKMT002 | PatternKit.Generators.Messaging | Error | Message translator must declare exactly one handler.
PKMT003 | PatternKit.Generators.Messaging | Error | Message translator handler signature is invalid.
PKMB001 | PatternKit.Generators.Messaging | Error | Mailbox type must be partial.
PKMB002 | PatternKit.Generators.Messaging | Error | Mailbox must declare exactly one handler.
PKMB003 | PatternKit.Generators.Messaging | Error | Mailbox handler signature is invalid.
PKMB004 | PatternKit.Generators.Messaging | Error | Mailbox optional handler signature is invalid.
PKMB005 | PatternKit.Generators.Messaging | Error | Mailbox generator configuration is invalid.
PKRP001 | PatternKit.Generators.Messaging | Error | Reliability pipeline type must be partial.
PKRP002 | PatternKit.Generators.Messaging | Error | Reliability pipeline must declare exactly one handler.
PKRP003 | PatternKit.Generators.Messaging | Error | Reliability pipeline handler signature is invalid.
PKRP004 | PatternKit.Generators.Messaging | Error | Reliability key selector signature is invalid.
PKRP005 | PatternKit.Generators.Messaging | Error | Reliability pipeline configuration is invalid.
PKBT001 | PatternKit.Generators.Messaging | Error | Backplane topology type must be partial.
PKBT002 | PatternKit.Generators.Messaging | Error | Backplane topology must declare at least one request/reply route or subscription.
PKBT003 | PatternKit.Generators.Messaging | Error | Backplane request/reply declaration is invalid.
PKBT004 | PatternKit.Generators.Messaging | Error | Backplane subscription declaration is invalid.
PKBT005 | PatternKit.Generators.Messaging | Error | Backplane request default route is duplicated.
PKBH001 | PatternKit.Generators.Bulkhead | Error | Bulkhead policy host must be partial.
PKBH002 | PatternKit.Generators.Bulkhead | Error | Bulkhead policy configuration is invalid.
PKCA001 | PatternKit.Generators.CacheAside | Error | Cache-aside policy host must be partial.
PKCA002 | PatternKit.Generators.CacheAside | Error | Cache-aside policy configuration is invalid.
PKCA003 | PatternKit.Generators.CacheAside | Error | Cache-aside predicate signature is invalid.
PKCA004 | PatternKit.Generators.CacheAside | Error | Cache-aside predicate declaration is duplicated.
PKCB001 | PatternKit.Generators.CircuitBreaker | Error | Circuit breaker policy host must be partial.
PKCB002 | PatternKit.Generators.CircuitBreaker | Error | Circuit breaker policy configuration is invalid.
PKCB003 | PatternKit.Generators.CircuitBreaker | Error | Circuit breaker predicate signature is invalid.
PKCB004 | PatternKit.Generators.CircuitBreaker | Error | Circuit breaker predicate declaration is duplicated.
PKRLT001 | PatternKit.Generators.RateLimiting | Error | Rate-limit policy host must be partial.
PKRLT002 | PatternKit.Generators.RateLimiting | Error | Rate-limit policy configuration is invalid.
PKREP001 | PatternKit.Generators.Repository | Error | Repository host must be partial.
PKREP002 | PatternKit.Generators.Repository | Error | Repository must declare exactly one key selector.
PKREP003 | PatternKit.Generators.Repository | Error | Repository key selector signature is invalid.
PKAF001 | PatternKit.Generators.Factories | Error | Abstract factory host must be partial.
PKAF002 | PatternKit.Generators.Factories | Error | Abstract factory must declare at least one product.
PKAF003 | PatternKit.Generators.Factories | Error | Abstract factory product declaration is invalid.
PKAF004 | PatternKit.Generators.Factories | Error | Abstract factory product declaration is duplicated.
PKINT001 | PatternKit.Generators.Interpreter | Error | Interpreter host must be partial.
PKINT002 | PatternKit.Generators.Interpreter | Error | Interpreter must declare at least one rule.
PKINT003 | PatternKit.Generators.Interpreter | Error | Interpreter rule signature is invalid.
PKINT004 | PatternKit.Generators.Interpreter | Error | Interpreter rule declaration is duplicated.
PKIM001 | PatternKit.Generators.IdentityMap | Error | Identity Map host must be partial.
PKIM002 | PatternKit.Generators.IdentityMap | Error | Identity Map must declare exactly one key selector.
PKIM003 | PatternKit.Generators.IdentityMap | Error | Identity Map key selector signature is invalid.
PKSPEC001 | PatternKit.Generators.Specification | Error | Specification registry host must be partial.
PKSPEC002 | PatternKit.Generators.Specification | Error | Specification registry must declare at least one rule.
PKSPEC003 | PatternKit.Generators.Specification | Error | Specification rule signature is invalid.
PKSPEC004 | PatternKit.Generators.Specification | Error | Specification rule declaration is duplicated.
PKTDG001 | PatternKit.Generators.TableDataGateway | Error | Table Data Gateway host must be partial.
PKTDG002 | PatternKit.Generators.TableDataGateway | Error | Table Data Gateway must declare exactly one key selector.
PKTDG003 | PatternKit.Generators.TableDataGateway | Error | Table Data Gateway key selector signature is invalid.
PKRET001 | PatternKit.Generators.Retry | Error | Retry policy host must be partial.
PKRET002 | PatternKit.Generators.Retry | Error | Retry policy configuration is invalid.
PKRET003 | PatternKit.Generators.Retry | Error | Retry predicate signature is invalid.
PKRET004 | PatternKit.Generators.Retry | Error | Retry predicate declaration is duplicated.
PKRL001 | PatternKit.Generators.Messaging | Error | Recipient list type must be partial.
PKRL002 | PatternKit.Generators.Messaging | Error | Recipient list must declare at least one recipient.
PKRL003 | PatternKit.Generators.Messaging | Error | Recipient handler or predicate signature is invalid.
PKRL004 | PatternKit.Generators.Messaging | Error | Recipient name or order is duplicated.
PKSA001 | PatternKit.Generators.Messaging | Error | Splitter or aggregator host must be partial.
PKSA002 | PatternKit.Generators.Messaging | Error | Generated splitter host must declare exactly one projection.
PKSA003 | PatternKit.Generators.Messaging | Error | Generated splitter projection signature is invalid.
PKSA004 | PatternKit.Generators.Messaging | Error | Generated aggregator host must declare correlation, completion, and projection methods.
PKSA005 | PatternKit.Generators.Messaging | Error | Generated aggregator method signature is invalid.
PKSA006 | PatternKit.Generators.Messaging | Error | Generated aggregator duplicate policy is invalid.
PKCNS001 | PatternKit.Generators.Messaging | Error | Competing Consumers host must be partial.
PKCNS002 | PatternKit.Generators.Messaging | Error | Competing Consumers configuration is invalid.
PKPF001 | PatternKit.Generators.Messaging | Error | Pipes and Filters host must be partial.
PKMF001 | PatternKit.Generators.Messaging | Error | Message filter host type must be partial.
PKMF002 | PatternKit.Generators.Messaging | Error | Message filter must declare at least one rule.
PKMF003 | PatternKit.Generators.Messaging | Error | Message filter rule method signature is invalid.
PKMF004 | PatternKit.Generators.Messaging | Error | Message filter rule name or order is duplicated.
PKWT001 | PatternKit.Generators.Messaging | Error | Wire Tap host type must be partial.
PKWT002 | PatternKit.Generators.Messaging | Error | Wire Tap must declare at least one handler.
PKWT003 | PatternKit.Generators.Messaging | Error | Wire Tap handler signature is invalid.
PKWT004 | PatternKit.Generators.Messaging | Error | Wire Tap handler name or order is duplicated.
PKECS001 | PatternKit.Generators.Cloud | Error | External Configuration Store host type must be partial.
PKECS002 | PatternKit.Generators.Cloud | Error | External Configuration Store loader is invalid.
PKECS003 | PatternKit.Generators.Cloud | Error | External Configuration Store validator signature is invalid.
PKECS004 | PatternKit.Generators.Cloud | Error | External Configuration Store validator order is duplicated.
PKHEM001 | PatternKit.Generators.HealthEndpointMonitoring | Error | Health Endpoint host must be partial.
PKHEM002 | PatternKit.Generators.HealthEndpointMonitoring | Error | Health Endpoint checks are missing.
PKHEM003 | PatternKit.Generators.HealthEndpointMonitoring | Error | Health Endpoint check signature is invalid.
PKGA001 | PatternKit.Generators.GatewayAggregation | Error | Gateway Aggregation host must be partial.
PKGA002 | PatternKit.Generators.GatewayAggregation | Error | Gateway Aggregation members are missing.
PKGA003 | PatternKit.Generators.GatewayAggregation | Error | Gateway Aggregation method signature is invalid.
PKGA004 | PatternKit.Generators.GatewayAggregation | Error | Gateway Aggregation fetch is duplicated.
PKGR001 | PatternKit.Generators.GatewayRouting | Error | Gateway Routing host must be partial.
PKGR002 | PatternKit.Generators.GatewayRouting | Error | Gateway Routing members are missing.
PKGR003 | PatternKit.Generators.GatewayRouting | Error | Gateway Routing method signature is invalid.
PKGR004 | PatternKit.Generators.GatewayRouting | Error | Gateway Routing route is duplicated.
PKGR005 | PatternKit.Generators.GatewayRouting | Error | Gateway Routing handler is missing.
PKSF001 | PatternKit.Generators.StranglerFig | Error | Strangler Fig host must be partial.
PKSF002 | PatternKit.Generators.StranglerFig | Error | Strangler Fig members are missing.
PKSF003 | PatternKit.Generators.StranglerFig | Error | Strangler Fig method signature is invalid.
PKSF004 | PatternKit.Generators.StranglerFig | Error | Strangler Fig route is duplicated.
PKMS001 | PatternKit.Generators.Messaging | Error | Message store type must be partial.
PKMS002 | PatternKit.Generators.Messaging | Error | Message store identity signature is invalid.
PKMS003 | PatternKit.Generators.Messaging | Error | Message store retention signature is invalid.
PKMS004 | PatternKit.Generators.Messaging | Error | Message store hook is duplicated.
PKCTL001 | PatternKit.Generators.Messaging | Error | Control Bus host type must be partial.
PKCTL002 | PatternKit.Generators.Messaging | Error | Control Bus must declare at least one handler.
PKCTL003 | PatternKit.Generators.Messaging | Error | Control Bus handler signature is invalid.
PKCTL004 | PatternKit.Generators.Messaging | Error | Control Bus command name or order is duplicated.
PKSCG001 | PatternKit.Generators.Messaging | Error | Scatter-Gather host type must be partial.
PKSCG002 | PatternKit.Generators.Messaging | Error | Scatter-Gather must declare at least one recipient.
PKSCG003 | PatternKit.Generators.Messaging | Error | Scatter-Gather recipient signature is invalid.
PKSCG004 | PatternKit.Generators.Messaging | Error | Scatter-Gather aggregator signature is invalid.
PKSCG005 | PatternKit.Generators.Messaging | Error | Scatter-Gather recipient name or order is duplicated.
PKRSEQ001 | PatternKit.Generators.Messaging | Error | Resequencer host type must be partial.
PKRSEQ002 | PatternKit.Generators.Messaging | Error | Resequencer must declare exactly one sequence selector.
PKRSEQ003 | PatternKit.Generators.Messaging | Error | Resequencer sequence selector signature is invalid.
PKCHN001 | PatternKit.Generators.Messaging | Error | Message Channel host type must be partial.
PKCHN002 | PatternKit.Generators.Messaging | Error | Message Channel capacity is invalid.
PKPOLL001 | PatternKit.Generators.Messaging | Error | Polling Consumer host type must be partial.
PKPOLL002 | PatternKit.Generators.Messaging | Error | Polling Consumer must declare exactly one source.
PKPOLL003 | PatternKit.Generators.Messaging | Error | Polling Consumer source signature is invalid.
PKEVT001 | PatternKit.Generators.Messaging | Error | Event-Driven Consumer host type must be partial.
PKEVT002 | PatternKit.Generators.Messaging | Error | Event-Driven Consumer must declare at least one handler.
PKEVT003 | PatternKit.Generators.Messaging | Error | Event-Driven Consumer handler signature is invalid.
PKCAD001 | PatternKit.Generators.Messaging | Error | Channel Adapter host type must be partial.
PKCAD002 | PatternKit.Generators.Messaging | Error | Channel Adapter must declare exactly one inbound translator.
PKCAD003 | PatternKit.Generators.Messaging | Error | Channel Adapter must declare exactly one outbound translator.
PKCAD004 | PatternKit.Generators.Messaging | Error | Channel Adapter inbound translator signature is invalid.
PKCAD005 | PatternKit.Generators.Messaging | Error | Channel Adapter outbound translator signature is invalid.
PKCDM001 | PatternKit.Generators.CanonicalDataModel | Error | Canonical Data Model host must be partial.
PKCDM002 | PatternKit.Generators.CanonicalDataModel | Error | Canonical Data Model mapper is missing.
PKCDM003 | PatternKit.Generators.CanonicalDataModel | Error | Canonical Data Model mapper signature is invalid.
PKECST001 | PatternKit.Generators.EventCarriedStateTransfer | Error | Event-Carried State Transfer host must be partial.
PKECST002 | PatternKit.Generators.EventCarriedStateTransfer | Error | Event-Carried State Transfer methods are missing.
PKECST003 | PatternKit.Generators.EventCarriedStateTransfer | Error | Event-Carried State Transfer method signature is invalid.
PKEN001 | PatternKit.Generators.EventNotification | Error | Event Notification host must be partial.
PKEN002 | PatternKit.Generators.EventNotification | Error | Event Notification key selector is missing.
PKEN003 | PatternKit.Generators.EventNotification | Error | Event Notification method signature is invalid.
PKEN004 | PatternKit.Generators.EventNotification | Error | Event Notification metadata is duplicated.
PKGWY001 | PatternKit.Generators.Messaging | Error | Messaging Gateway host type must be partial.
PKGWY002 | PatternKit.Generators.Messaging | Error | Messaging Gateway must declare exactly one handler.
PKGWY003 | PatternKit.Generators.Messaging | Error | Messaging Gateway handler signature is invalid.
PKSVA001 | PatternKit.Generators.Messaging | Error | Service Activator host type must be partial.
PKSVA002 | PatternKit.Generators.Messaging | Error | Service Activator must declare exactly one handler.
PKSVA003 | PatternKit.Generators.Messaging | Error | Service Activator handler signature is invalid.
