using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the State pattern.
/// Generates deterministic state machine implementations with explicit states, triggers,
/// guards, entry/exit hooks, and sync/async support using ValueTask.
/// </summary>
[Generator]
public sealed class StateMachineGenerator : IIncrementalGenerator
{
    // Diagnostic IDs
    private const string DiagIdTypeNotPartial = "PKST001";
    private const string DiagIdStateNotEnum = "PKST002";
    private const string DiagIdTriggerNotEnum = "PKST003";
    private const string DiagIdDuplicateTransition = "PKST004";
    private const string DiagIdInvalidTransitionSignature = "PKST005";
    private const string DiagIdInvalidGuardSignature = "PKST006";
    private const string DiagIdInvalidHookSignature = "PKST007";
    private const string DiagIdAsyncMethodDetected = "PKST008";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type marked with [StateMachine] must be partial",
        messageFormat: "Type '{0}' is marked with [StateMachine] but is not declared as partial. Add the 'partial' keyword to the type declaration.",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor StateNotEnumDescriptor = new(
        id: DiagIdStateNotEnum,
        title: "State type must be an enum",
        messageFormat: "State type '{0}' must be an enum type. Non-enum state types are not supported in v1.",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor TriggerNotEnumDescriptor = new(
        id: DiagIdTriggerNotEnum,
        title: "Trigger type must be an enum",
        messageFormat: "Trigger type '{0}' must be an enum type. Non-enum trigger types are not supported in v1.",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateTransitionDescriptor = new(
        id: DiagIdDuplicateTransition,
        title: "Duplicate transition detected",
        messageFormat: "Duplicate transition detected for (From={0}, Trigger={1}). Each (From, Trigger) pair must be unique. Conflicting methods: {2}.",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidTransitionSignatureDescriptor = new(
        id: DiagIdInvalidTransitionSignature,
        title: "Transition method signature invalid",
        messageFormat: "Transition method '{0}' has an invalid signature. Transitions must return void or ValueTask, optionally accepting CancellationToken for async methods.",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidGuardSignatureDescriptor = new(
        id: DiagIdInvalidGuardSignature,
        title: "Guard method signature invalid",
        messageFormat: "Guard method '{0}' has an invalid signature. Guards must return bool or ValueTask<bool>, optionally accepting CancellationToken for async methods.",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidHookSignatureDescriptor = new(
        id: DiagIdInvalidHookSignature,
        title: "Entry/Exit hook signature invalid",
        messageFormat: "Entry/Exit hook method '{0}' has an invalid signature. Hooks must return void or ValueTask, optionally accepting CancellationToken for async methods.",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AsyncMethodDetectedDescriptor = new(
        id: DiagIdAsyncMethodDetected,
        title: "Async method detected but async generation disabled",
        messageFormat: "Async method '{0}' detected but async generation is disabled. Enable GenerateAsync or ForceAsync on the [StateMachine] attribute.",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all type declarations with [StateMachine] attribute
        var stateMachineTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.State.StateMachineAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        // Generate for each type
        context.RegisterSourceOutput(stateMachineTypes, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.State.StateMachineAttribute");
            if (attr is null)
                return;

            GenerateStateMachineForType(spc, typeSymbol, attr, typeContext.TargetNode);
        });
    }

    private void GenerateStateMachineForType(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attribute,
        SyntaxNode node)
    {
        // Check if type is partial
        if (!IsPartialType(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNotPartialDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Parse attribute configuration
        var config = ParseStateMachineConfig(attribute, context, out var stateType, out var triggerType);
        if (config is null || stateType is null || triggerType is null)
            return;

        // Validate state and trigger types are enums
        if (stateType.TypeKind != TypeKind.Enum)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                StateNotEnumDescriptor,
                node.GetLocation(),
                stateType.ToDisplayString()));
            return;
        }

        if (triggerType.TypeKind != TypeKind.Enum)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TriggerNotEnumDescriptor,
                node.GetLocation(),
                triggerType.ToDisplayString()));
            return;
        }

        // Collect transitions, guards, and hooks
        var transitions = CollectTransitions(typeSymbol, stateType, triggerType, context);
        var guards = CollectGuards(typeSymbol, stateType, triggerType, context);
        var entryHooks = CollectEntryHooks(typeSymbol, stateType, context);
        var exitHooks = CollectExitHooks(typeSymbol, stateType, context);

        // Validate for duplicate transitions
        if (!ValidateTransitions(transitions, typeSymbol, context))
            return;

        // Validate signatures
        if (!ValidateSignatures(transitions, guards, entryHooks, exitHooks, context))
            return;

        // Determine if async generation is needed
        var needsAsync = config.ForceAsync ||
                        (config.GenerateAsync ?? false) ||
                        DetermineIfAsync(transitions, guards, entryHooks, exitHooks);

        // Generate the state machine implementation
        var source = GenerateStateMachine(typeSymbol, config, stateType, triggerType, 
                                         transitions, guards, entryHooks, exitHooks, needsAsync);
        var fileName = $"{typeSymbol.Name}.StateMachine.g.cs";
        context.AddSource(fileName, source);
    }

    private static bool IsPartialType(SyntaxNode node)
    {
        return node switch
        {
            ClassDeclarationSyntax classDecl => classDecl.Modifiers.Any(SyntaxKind.PartialKeyword),
            StructDeclarationSyntax structDecl => structDecl.Modifiers.Any(SyntaxKind.PartialKeyword),
            RecordDeclarationSyntax recordDecl => recordDecl.Modifiers.Any(SyntaxKind.PartialKeyword),
            _ => false
        };
    }

    private StateMachineConfig? ParseStateMachineConfig(
        AttributeData attribute,
        SourceProductionContext context,
        out ITypeSymbol? stateType,
        out ITypeSymbol? triggerType)
    {
        stateType = null;
        triggerType = null;

        // Constructor arguments: stateType, triggerType
        if (attribute.ConstructorArguments.Length < 2)
            return null;

        stateType = attribute.ConstructorArguments[0].Value as ITypeSymbol;
        triggerType = attribute.ConstructorArguments[1].Value as ITypeSymbol;

        if (stateType is null || triggerType is null)
            return null;

        var config = new StateMachineConfig
        {
            StateTypeName = stateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            TriggerTypeName = triggerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        };

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "FireMethodName":
                    config.FireMethodName = namedArg.Value.Value?.ToString() ?? "Fire";
                    break;
                case "FireAsyncMethodName":
                    config.FireAsyncMethodName = namedArg.Value.Value?.ToString() ?? "FireAsync";
                    break;
                case "CanFireMethodName":
                    config.CanFireMethodName = namedArg.Value.Value?.ToString() ?? "CanFire";
                    break;
                case "GenerateAsync":
                    if (namedArg.Value.Value is bool ga)
                        config.GenerateAsync = ga;
                    break;
                case "ForceAsync":
                    config.ForceAsync = namedArg.Value.Value is bool f && f;
                    break;
                case "InvalidTrigger":
                    config.InvalidTriggerPolicy = namedArg.Value.Value is int itp ? itp : 0;
                    break;
                case "GuardFailure":
                    config.GuardFailurePolicy = namedArg.Value.Value is int gfp ? gfp : 0;
                    break;
            }
        }

        return config;
    }

    private ImmutableArray<TransitionModel> CollectTransitions(
        INamedTypeSymbol typeSymbol,
        ITypeSymbol stateType,
        ITypeSymbol triggerType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<TransitionModel>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var transitionAttrs = method.GetAttributes().Where(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.State.StateTransitionAttribute");

            foreach (var transitionAttr in transitionAttrs)
            {
                string? fromState = null;
                string? trigger = null;
                string? toState = null;

                foreach (var namedArg in transitionAttr.NamedArguments)
                {
                    if (namedArg.Key == "From")
                        fromState = GetEnumValueName(namedArg.Value, stateType);
                    else if (namedArg.Key == "Trigger")
                        trigger = GetEnumValueName(namedArg.Value, triggerType);
                    else if (namedArg.Key == "To")
                        toState = GetEnumValueName(namedArg.Value, stateType);
                }

                if (fromState is not null && trigger is not null && toState is not null)
                {
                    builder.Add(new TransitionModel
                    {
                        Method = method,
                        FromState = fromState,
                        Trigger = trigger,
                        ToState = toState
                    });
                }
            }
        }

        return builder.ToImmutable();
    }

    private ImmutableArray<GuardModel> CollectGuards(
        INamedTypeSymbol typeSymbol,
        ITypeSymbol stateType,
        ITypeSymbol triggerType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<GuardModel>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var guardAttrs = method.GetAttributes().Where(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.State.StateGuardAttribute");

            foreach (var guardAttr in guardAttrs)
            {
                string? fromState = null;
                string? trigger = null;

                foreach (var namedArg in guardAttr.NamedArguments)
                {
                    if (namedArg.Key == "From")
                        fromState = GetEnumValueName(namedArg.Value, stateType);
                    else if (namedArg.Key == "Trigger")
                        trigger = GetEnumValueName(namedArg.Value, triggerType);
                }

                if (fromState is not null && trigger is not null)
                {
                    builder.Add(new GuardModel
                    {
                        Method = method,
                        FromState = fromState,
                        Trigger = trigger
                    });
                }
            }
        }

        return builder.ToImmutable();
    }

    private ImmutableArray<HookModel> CollectEntryHooks(
        INamedTypeSymbol typeSymbol,
        ITypeSymbol stateType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<HookModel>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var entryAttrs = method.GetAttributes().Where(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.State.StateEntryAttribute");

            foreach (var entryAttr in entryAttrs)
            {
                string? state = null;

                // Check constructor argument first
                if (entryAttr.ConstructorArguments.Length > 0)
                {
                    state = GetEnumValueName(entryAttr.ConstructorArguments[0], stateType);
                }
                else
                {
                    // Check named argument
                    var stateArg = entryAttr.NamedArguments.FirstOrDefault(na => na.Key == "State");
                    if (stateArg.Key is not null)
                        state = GetEnumValueName(stateArg.Value, stateType);
                }

                if (state is not null)
                {
                    builder.Add(new HookModel
                    {
                        Method = method,
                        State = state
                    });
                }
            }
        }

        return builder.ToImmutable();
    }

    private ImmutableArray<HookModel> CollectExitHooks(
        INamedTypeSymbol typeSymbol,
        ITypeSymbol stateType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<HookModel>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var exitAttrs = method.GetAttributes().Where(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.State.StateExitAttribute");

            foreach (var exitAttr in exitAttrs)
            {
                string? state = null;

                // Check constructor argument first
                if (exitAttr.ConstructorArguments.Length > 0)
                {
                    state = GetEnumValueName(exitAttr.ConstructorArguments[0], stateType);
                }
                else
                {
                    // Check named argument
                    var stateArg = exitAttr.NamedArguments.FirstOrDefault(na => na.Key == "State");
                    if (stateArg.Key is not null)
                        state = GetEnumValueName(stateArg.Value, stateType);
                }

                if (state is not null)
                {
                    builder.Add(new HookModel
                    {
                        Method = method,
                        State = state
                    });
                }
            }
        }

        return builder.ToImmutable();
    }

    private string? GetEnumValueName(TypedConstant constant, ITypeSymbol enumType)
    {
        if (constant.Value is int intValue)
        {
            // Get the enum member name from the value
            var members = enumType.GetMembers().OfType<IFieldSymbol>()
                .Where(f => f.IsConst && f.HasConstantValue && Equals(f.ConstantValue, intValue));
            return members.FirstOrDefault()?.Name;
        }
        return null;
    }

    private bool ValidateTransitions(
        ImmutableArray<TransitionModel> transitions,
        INamedTypeSymbol typeSymbol,
        SourceProductionContext context)
    {
        var transitionKeys = new Dictionary<string, List<string>>();

        foreach (var transition in transitions)
        {
            var key = $"{transition.FromState},{transition.Trigger}";
            if (!transitionKeys.ContainsKey(key))
                transitionKeys[key] = new List<string>();
            transitionKeys[key].Add(transition.Method.Name);
        }

        foreach (var kvp in transitionKeys.Where(kvp => kvp.Value.Count > 1))
        {
            var parts = kvp.Key.Split(',');
            var methodNames = string.Join(", ", kvp.Value);
            context.ReportDiagnostic(Diagnostic.Create(
                DuplicateTransitionDescriptor,
                Location.None,
                parts[0],
                parts[1],
                methodNames));
            return false;
        }

        return true;
    }

    private bool ValidateSignatures(
        ImmutableArray<TransitionModel> transitions,
        ImmutableArray<GuardModel> guards,
        ImmutableArray<HookModel> entryHooks,
        ImmutableArray<HookModel> exitHooks,
        SourceProductionContext context)
    {
        foreach (var transition in transitions)
        {
            if (!ValidateTransitionSignature(transition.Method, context))
                return false;
        }

        foreach (var guard in guards)
        {
            if (!ValidateGuardSignature(guard.Method, context))
                return false;
        }

        foreach (var hook in entryHooks.Concat(exitHooks))
        {
            if (!ValidateHookSignature(hook.Method, context))
                return false;
        }

        return true;
    }

    private bool ValidateTransitionSignature(IMethodSymbol method, SourceProductionContext context)
    {
        var returnsVoid = method.ReturnsVoid;
        var returnType = method.ReturnType;
        var returnsValueTask = IsNonGenericValueTask(returnType);

        if (!returnsVoid && !returnsValueTask)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidTransitionSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        // If parameters exist, they must be CancellationToken only
        if (method.Parameters.Length > 1 ||
            (method.Parameters.Length == 1 && !IsCancellationToken(method.Parameters[0].Type)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidTransitionSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        return true;
    }

    private bool ValidateGuardSignature(IMethodSymbol method, SourceProductionContext context)
    {
        var returnType = method.ReturnType;
        var returnsBool = returnType.SpecialType == SpecialType.System_Boolean;
        var returnsValueTaskBool = IsGenericValueTaskOfBool(returnType);

        if (!returnsBool && !returnsValueTaskBool)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidGuardSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        // If parameters exist, they must be CancellationToken only
        if (method.Parameters.Length > 1 ||
            (method.Parameters.Length == 1 && !IsCancellationToken(method.Parameters[0].Type)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidGuardSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        return true;
    }

    private bool ValidateHookSignature(IMethodSymbol method, SourceProductionContext context)
    {
        var returnsVoid = method.ReturnsVoid;
        var returnType = method.ReturnType;
        var returnsValueTask = IsNonGenericValueTask(returnType);

        if (!returnsVoid && !returnsValueTask)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidHookSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        // If parameters exist, they must be CancellationToken only
        if (method.Parameters.Length > 1 ||
            (method.Parameters.Length == 1 && !IsCancellationToken(method.Parameters[0].Type)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidHookSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        return true;
    }

    private bool DetermineIfAsync(
        ImmutableArray<TransitionModel> transitions,
        ImmutableArray<GuardModel> guards,
        ImmutableArray<HookModel> entryHooks,
        ImmutableArray<HookModel> exitHooks)
    {
        foreach (var transition in transitions)
        {
            if (IsNonGenericValueTask(transition.Method.ReturnType))
                return true;
        }

        foreach (var guard in guards)
        {
            if (IsGenericValueTaskOfBool(guard.Method.ReturnType))
                return true;
        }

        foreach (var hook in entryHooks.Concat(exitHooks))
        {
            if (IsNonGenericValueTask(hook.Method.ReturnType))
                return true;
        }

        return false;
    }

    private bool IsNonGenericValueTask(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.Name == "ValueTask" &&
               namedType.Arity == 0 &&
               namedType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";
    }

    private bool IsGenericValueTaskOfBool(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.Name == "ValueTask" &&
               namedType.Arity == 1 &&
               namedType.TypeArguments.Length == 1 &&
               namedType.TypeArguments[0].SpecialType == SpecialType.System_Boolean &&
               namedType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";
    }

    private bool IsCancellationToken(ITypeSymbol type)
    {
        return type.ToDisplayString() == "System.Threading.CancellationToken";
    }

    private string GenerateStateMachine(
        INamedTypeSymbol typeSymbol,
        StateMachineConfig config,
        ITypeSymbol stateType,
        ITypeSymbol triggerType,
        ImmutableArray<TransitionModel> transitions,
        ImmutableArray<GuardModel> guards,
        ImmutableArray<HookModel> entryHooks,
        ImmutableArray<HookModel> exitHooks,
        bool needsAsync)
    {
        var sb = new StringBuilder();
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        // File header
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        // Get type keyword (class, struct, record class, record struct)
        var typeKeyword = GetTypeKeyword(typeSymbol);

        sb.AppendLine($"partial {typeKeyword} {typeSymbol.Name}");
        sb.AppendLine("{");

        // State property
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Gets the current state of the state machine.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public {config.StateTypeName} State {{ get; private set; }}");
        sb.AppendLine();

        // CanFire method
        GenerateCanFireMethod(sb, config, transitions, guards, needsAsync);

        // Fire method
        GenerateFireMethod(sb, config, stateType, triggerType, transitions, guards, entryHooks, exitHooks, false);

        // FireAsync method (if needed)
        if (needsAsync)
        {
            GenerateFireMethod(sb, config, stateType, triggerType, transitions, guards, entryHooks, exitHooks, true);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GetTypeKeyword(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.IsRecord)
        {
            return typeSymbol.IsValueType ? "record struct" : "record class";
        }
        return typeSymbol.IsValueType ? "struct" : "class";
    }

    private void GenerateCanFireMethod(
        StringBuilder sb,
        StateMachineConfig config,
        ImmutableArray<TransitionModel> transitions,
        ImmutableArray<GuardModel> guards,
        bool needsAsync)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Determines whether the specified trigger can be fired from the current state.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"trigger\">The trigger to check.</param>");
        sb.AppendLine($"    /// <returns>true if the trigger can be fired; otherwise, false.</returns>");
        sb.AppendLine($"    public bool {config.CanFireMethodName}({config.TriggerTypeName} trigger)");
        sb.AppendLine($"    {{");

        // Group transitions by (from, trigger)
        var transitionGroups = transitions
            .GroupBy(t => (t.FromState, t.Trigger))
            .OrderBy(g => g.Key.FromState)
            .ThenBy(g => g.Key.Trigger);

        if (transitionGroups.Any())
        {
            sb.AppendLine($"        return (State, trigger) switch");
            sb.AppendLine($"        {{");

            foreach (var group in transitionGroups)
            {
                var (fromState, trigger) = group.Key;
                
                // Check if there's a guard for this transition
                var guard = guards.FirstOrDefault(g => g.FromState == fromState && g.Trigger == trigger);
                
                if (guard is not null)
                {
                    // If guard is async and we're in sync context, we can't evaluate it
                    if (IsGenericValueTaskOfBool(guard.Method.ReturnType))
                    {
                        sb.AppendLine($"            ({config.StateTypeName}.{fromState}, {config.TriggerTypeName}.{trigger}) => false, // Guard is async, cannot evaluate synchronously");
                    }
                    else
                    {
                        sb.AppendLine($"            ({config.StateTypeName}.{fromState}, {config.TriggerTypeName}.{trigger}) => {guard.Method.Name}(),");
                    }
                }
                else
                {
                    sb.AppendLine($"            ({config.StateTypeName}.{fromState}, {config.TriggerTypeName}.{trigger}) => true,");
                }
            }

            sb.AppendLine($"            _ => false");
            sb.AppendLine($"        }};");
        }
        else
        {
            sb.AppendLine($"        return false;");
        }

        sb.AppendLine($"    }}");
        sb.AppendLine();
    }

    private void GenerateFireMethod(
        StringBuilder sb,
        StateMachineConfig config,
        ITypeSymbol stateType,
        ITypeSymbol triggerType,
        ImmutableArray<TransitionModel> transitions,
        ImmutableArray<GuardModel> guards,
        ImmutableArray<HookModel> entryHooks,
        ImmutableArray<HookModel> exitHooks,
        bool isAsync)
    {
        var methodName = isAsync ? config.FireAsyncMethodName : config.FireMethodName;
        var returnType = isAsync ? "global::System.Threading.Tasks.ValueTask" : "void";
        var ctParam = isAsync ? ", global::System.Threading.CancellationToken cancellationToken = default" : "";
        var awaitKeyword = isAsync ? "await " : "";
        var asyncModifier = isAsync ? "async " : "";
        var configureAwait = isAsync ? ".ConfigureAwait(false)" : "";

        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Fires the specified trigger, potentially transitioning to a new state.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"trigger\">The trigger to fire.</param>");
        if (isAsync)
        {
            sb.AppendLine($"    /// <param name=\"cancellationToken\">A cancellation token.</param>");
        }
        sb.AppendLine($"    public {asyncModifier}{returnType} {methodName}({config.TriggerTypeName} trigger{ctParam})");
        sb.AppendLine($"    {{");

        // Group transitions by (from, trigger)
        var transitionGroups = transitions
            .GroupBy(t => (t.FromState, t.Trigger))
            .OrderBy(g => g.Key.FromState)
            .ThenBy(g => g.Key.Trigger)
            .ToList();

        if (transitionGroups.Count == 0)
        {
            // No transitions defined
            if (config.InvalidTriggerPolicy == 0) // Throw
            {
                sb.AppendLine($"        throw new global::System.InvalidOperationException($\"No transitions defined for state machine.\");");
            }
            sb.AppendLine($"    }}");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"        switch (State)");
        sb.AppendLine($"        {{");

        // Group by from state
        var stateGroups = transitionGroups.GroupBy(g => g.Key.FromState);
        foreach (var stateGroup in stateGroups)
        {
            var fromState = stateGroup.Key;
            sb.AppendLine($"            case {config.StateTypeName}.{fromState}:");
            sb.AppendLine($"                switch (trigger)");
            sb.AppendLine($"                {{");

            foreach (var triggerGroup in stateGroup)
            {
                var trigger = triggerGroup.Key.Trigger;
                var transition = triggerGroup.First(); // Should only be one after validation
                var guard = guards.FirstOrDefault(g => g.FromState == fromState && g.Trigger == trigger);

                sb.AppendLine($"                    case {config.TriggerTypeName}.{trigger}:");

                // Evaluate guard if exists
                if (guard is not null)
                {
                    var guardHasCt = guard.Method.Parameters.Length > 0 && IsCancellationToken(guard.Method.Parameters[0].Type);
                    var guardCall = IsGenericValueTaskOfBool(guard.Method.ReturnType)
                        ? (isAsync 
                            ? (guardHasCt ? $"await {guard.Method.Name}(cancellationToken){configureAwait}" : $"await {guard.Method.Name}(){configureAwait}")
                            : (guardHasCt ? $"{guard.Method.Name}(global::System.Threading.CancellationToken.None).GetAwaiter().GetResult()" : $"{guard.Method.Name}().GetAwaiter().GetResult()"))
                        : $"{guard.Method.Name}()";

                    sb.AppendLine($"                        if (!{guardCall})");
                    sb.AppendLine($"                        {{");
                    
                    if (config.GuardFailurePolicy == 0) // Throw
                    {
                        sb.AppendLine($"                            throw new global::System.InvalidOperationException($\"Guard failed for transition from {fromState} on trigger {trigger}.\");");
                    }
                    else // Ignore or ReturnFalse
                    {
                        if (isAsync)
                            sb.AppendLine($"                            return;");
                        else
                            sb.AppendLine($"                            return;");
                    }
                    
                    sb.AppendLine($"                        }}");
                }

                // Execute exit hooks for fromState
                var exitHooksForState = exitHooks.Where(h => h.State == fromState).ToList();
                foreach (var exitHook in exitHooksForState)
                {
                    var hookHasCt = exitHook.Method.Parameters.Length > 0 && IsCancellationToken(exitHook.Method.Parameters[0].Type);
                    var hookCall = IsNonGenericValueTask(exitHook.Method.ReturnType)
                        ? (isAsync 
                            ? (hookHasCt ? $"await {exitHook.Method.Name}(cancellationToken){configureAwait};" : $"await {exitHook.Method.Name}(){configureAwait};")
                            : (hookHasCt ? $"{exitHook.Method.Name}(global::System.Threading.CancellationToken.None).GetAwaiter().GetResult();" : $"{exitHook.Method.Name}().GetAwaiter().GetResult();"))
                        : $"{exitHook.Method.Name}();";
                    sb.AppendLine($"                        {hookCall}");
                }

                // Execute transition action
                var transitionHasCt = transition.Method.Parameters.Length > 0 && IsCancellationToken(transition.Method.Parameters[0].Type);
                if (IsNonGenericValueTask(transition.Method.ReturnType))
                {
                    var transitionCall = isAsync 
                        ? (transitionHasCt ? $"await {transition.Method.Name}(cancellationToken){configureAwait};" : $"await {transition.Method.Name}(){configureAwait};")
                        : (transitionHasCt ? $"{transition.Method.Name}(global::System.Threading.CancellationToken.None).GetAwaiter().GetResult();" : $"{transition.Method.Name}().GetAwaiter().GetResult();");
                    sb.AppendLine($"                        {transitionCall}");
                }
                else
                {
                    sb.AppendLine($"                        {transition.Method.Name}();");
                }

                // Update state
                sb.AppendLine($"                        State = {config.StateTypeName}.{transition.ToState};");

                // Execute entry hooks for toState
                var entryHooksForState = entryHooks.Where(h => h.State == transition.ToState).ToList();
                foreach (var entryHook in entryHooksForState)
                {
                    var entryHasCt = entryHook.Method.Parameters.Length > 0 && IsCancellationToken(entryHook.Method.Parameters[0].Type);
                    var hookCall = IsNonGenericValueTask(entryHook.Method.ReturnType)
                        ? (isAsync 
                            ? (entryHasCt ? $"await {entryHook.Method.Name}(cancellationToken){configureAwait};" : $"await {entryHook.Method.Name}(){configureAwait};")
                            : (entryHasCt ? $"{entryHook.Method.Name}(global::System.Threading.CancellationToken.None).GetAwaiter().GetResult();" : $"{entryHook.Method.Name}().GetAwaiter().GetResult();"))
                        : $"{entryHook.Method.Name}();";
                    sb.AppendLine($"                        {hookCall}");
                }

                sb.AppendLine($"                        return;");
            }

            // Default case for invalid trigger in this state
            sb.AppendLine($"                    default:");
            if (config.InvalidTriggerPolicy == 0) // Throw
            {
                sb.AppendLine($"                        throw new global::System.InvalidOperationException($\"Invalid trigger {{trigger}} for state {fromState}.\");");
            }
            else // Ignore or ReturnFalse
            {
                sb.AppendLine($"                        return;");
            }
            sb.AppendLine($"                }}");
        }

        // Default case for states with no transitions
        sb.AppendLine($"            default:");
        if (config.InvalidTriggerPolicy == 0) // Throw
        {
            sb.AppendLine($"                throw new global::System.InvalidOperationException($\"No transitions defined for state {{State}}.\");");
        }
        else // Ignore or ReturnFalse
        {
            sb.AppendLine($"                return;");
        }

        sb.AppendLine($"        }}");
        sb.AppendLine($"    }}");
        sb.AppendLine();
    }

    private sealed class StateMachineConfig
    {
        public string StateTypeName { get; set; } = null!;
        public string TriggerTypeName { get; set; } = null!;
        public string FireMethodName { get; set; } = "Fire";
        public string FireAsyncMethodName { get; set; } = "FireAsync";
        public string CanFireMethodName { get; set; } = "CanFire";
        public bool? GenerateAsync { get; set; }
        public bool ForceAsync { get; set; }
        public int InvalidTriggerPolicy { get; set; } = 0; // 0=Throw, 1=Ignore, 2=ReturnFalse
        public int GuardFailurePolicy { get; set; } = 0; // 0=Throw, 1=Ignore, 2=ReturnFalse
    }

    private sealed class TransitionModel
    {
        public IMethodSymbol Method { get; set; } = null!;
        public string FromState { get; set; } = null!;
        public string Trigger { get; set; } = null!;
        public string ToState { get; set; } = null!;
    }

    private sealed class GuardModel
    {
        public IMethodSymbol Method { get; set; } = null!;
        public string FromState { get; set; } = null!;
        public string Trigger { get; set; } = null!;
    }

    private sealed class HookModel
    {
        public IMethodSymbol Method { get; set; } = null!;
        public string State { get; set; } = null!;
    }
}
