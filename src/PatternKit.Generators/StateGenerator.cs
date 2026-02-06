using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the State Machine pattern.
/// Generates State property, Fire, FireAsync, and CanFire methods
/// based on declared transitions, guards, and entry/exit hooks.
/// </summary>
[Generator]
public sealed class StateGenerator : IIncrementalGenerator
{
    private const string DiagIdTypeNotPartial = "PKST001";
    private const string DiagIdStateNotEnum = "PKST002";
    private const string DiagIdTriggerNotEnum = "PKST003";
    private const string DiagIdDuplicateTransition = "PKST004";
    private const string DiagIdInvalidTransitionSignature = "PKST005";
    private const string DiagIdInvalidGuardSignature = "PKST006";
    private const string DiagIdInvalidHookSignature = "PKST007";
    private const string DiagIdAsyncDisabled = "PKST008";

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
        messageFormat: "State type '{0}' specified in [StateMachine] on '{1}' is not an enum type.",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor TriggerNotEnumDescriptor = new(
        id: DiagIdTriggerNotEnum,
        title: "Trigger type must be an enum",
        messageFormat: "Trigger type '{0}' specified in [StateMachine] on '{1}' is not an enum type.",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateTransitionDescriptor = new(
        id: DiagIdDuplicateTransition,
        title: "Duplicate state transition",
        messageFormat: "Duplicate transition from state '{0}' on trigger '{1}' in type '{2}'. Each (state, trigger) pair must have a unique transition.",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidTransitionSignatureDescriptor = new(
        id: DiagIdInvalidTransitionSignature,
        title: "Invalid transition method signature",
        messageFormat: "Transition method '{0}' has an invalid signature. Must return void or ValueTask and accept zero parameters or a CancellationToken.",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidGuardSignatureDescriptor = new(
        id: DiagIdInvalidGuardSignature,
        title: "Invalid guard method signature",
        messageFormat: "Guard method '{0}' has an invalid signature. Must return bool and accept zero parameters.",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidHookSignatureDescriptor = new(
        id: DiagIdInvalidHookSignature,
        title: "Invalid entry/exit hook signature",
        messageFormat: "Hook method '{0}' has an invalid signature. Must return void or ValueTask and accept zero parameters or a CancellationToken.",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AsyncDisabledDescriptor = new(
        id: DiagIdAsyncDisabled,
        title: "Async methods found but GenerateAsync is false",
        messageFormat: "Type '{0}' has async transition/hook methods but GenerateAsync is not enabled. Set GenerateAsync=true or ForceAsync=true on [StateMachine].",
        category: "PatternKit.Generators.State",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.State.StateMachineAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        context.RegisterSourceOutput(targets, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.State.StateMachineAttribute");
            if (attr is null)
                return;

            GenerateStateMachine(spc, typeSymbol, attr, typeContext.TargetNode);
        });
    }

    private void GenerateStateMachine(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attribute,
        SyntaxNode node)
    {
        if (!GeneratorUtilities.IsPartialType(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNotPartialDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        var config = ParseConfig(attribute);

        // Validate state and trigger types
        if (config.StateType is null || config.StateType.TypeKind != TypeKind.Enum)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                StateNotEnumDescriptor,
                node.GetLocation(),
                config.StateType?.Name ?? "null",
                typeSymbol.Name));
            return;
        }

        if (config.TriggerType is null || config.TriggerType.TypeKind != TypeKind.Enum)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TriggerNotEnumDescriptor,
                node.GetLocation(),
                config.TriggerType?.Name ?? "null",
                typeSymbol.Name));
            return;
        }

        // Collect transitions, guards, hooks
        var transitions = CollectTransitions(typeSymbol, config, context);
        var guards = CollectGuards(typeSymbol, context);
        var entryHooks = CollectHooks(typeSymbol, "PatternKit.Generators.State.StateEntryAttribute", context);
        var exitHooks = CollectHooks(typeSymbol, "PatternKit.Generators.State.StateExitAttribute", context);

        // Validate no duplicate transitions
        if (!ValidateUniqueTransitions(transitions, typeSymbol, context))
            return;

        // Determine async
        var hasAsyncMethods = transitions.Any(t => IsNonGenericValueTask(t.Method.ReturnType))
            || entryHooks.Any(h => IsNonGenericValueTask(h.Method.ReturnType))
            || exitHooks.Any(h => IsNonGenericValueTask(h.Method.ReturnType));
        var needsAsync = config.ForceAsync || config.GenerateAsync || hasAsyncMethods;

        var source = EmitSource(typeSymbol, config, transitions, guards, entryHooks, exitHooks, needsAsync);
        var fileName = $"{typeSymbol.Name}.StateMachine.g.cs";
        context.AddSource(fileName, source);
    }

    private StateConfig ParseConfig(AttributeData attribute)
    {
        var config = new StateConfig();

        if (attribute.ConstructorArguments.Length >= 2)
        {
            if (attribute.ConstructorArguments[0].Value is INamedTypeSymbol stateType)
                config.StateType = stateType;
            if (attribute.ConstructorArguments[1].Value is INamedTypeSymbol triggerType)
                config.TriggerType = triggerType;
        }

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
                    config.GenerateAsync = namedArg.Value.Value is bool ga && ga;
                    break;
                case "ForceAsync":
                    config.ForceAsync = namedArg.Value.Value is bool fa && fa;
                    break;
                case "InvalidTrigger":
                    config.InvalidTrigger = namedArg.Value.Value is int it ? it : 0;
                    break;
                case "GuardFailure":
                    config.GuardFailure = namedArg.Value.Value is int gf ? gf : 0;
                    break;
            }
        }

        return config;
    }

    private ImmutableArray<TransitionModel> CollectTransitions(
        INamedTypeSymbol typeSymbol, StateConfig config, SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<TransitionModel>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.State.StateTransitionAttribute");
            if (attr is null)
                continue;

            string? from = null, trigger = null, to = null;
            foreach (var namedArg in attr.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "From":
                        from = GetEnumMemberName(namedArg.Value, config.StateType!);
                        break;
                    case "Trigger":
                        trigger = GetEnumMemberName(namedArg.Value, config.TriggerType!);
                        break;
                    case "To":
                        to = GetEnumMemberName(namedArg.Value, config.StateType!);
                        break;
                }
            }

            if (from is not null && trigger is not null && to is not null)
            {
                // Validate signature
                if (!ValidateTransitionSignature(method, context))
                    continue;

                builder.Add(new TransitionModel
                {
                    Method = method,
                    FromState = from,
                    Trigger = trigger,
                    ToState = to
                });
            }
        }

        return builder.ToImmutable();
    }

    private ImmutableArray<GuardModel> CollectGuards(
        INamedTypeSymbol typeSymbol, SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<GuardModel>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.State.StateGuardAttribute");
            if (attr is null)
                continue;

            string? from = null, trigger = null;
            foreach (var namedArg in attr.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "From":
                        from = GetEnumMemberNameFromTypedConstant(namedArg.Value);
                        break;
                    case "Trigger":
                        trigger = GetEnumMemberNameFromTypedConstant(namedArg.Value);
                        break;
                }
            }

            if (from is not null && trigger is not null)
            {
                // Validate signature: must return bool, zero parameters
                if (!ValidateGuardSignature(method, context))
                    continue;

                builder.Add(new GuardModel
                {
                    Method = method,
                    FromState = from,
                    Trigger = trigger
                });
            }
        }

        return builder.ToImmutable();
    }

    private ImmutableArray<HookModel> CollectHooks(
        INamedTypeSymbol typeSymbol, string attributeFqn, SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<HookModel>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == attributeFqn);
            if (attr is null)
                continue;

            string? state = null;
            if (attr.ConstructorArguments.Length > 0)
            {
                state = GetEnumMemberNameFromTypedConstant(attr.ConstructorArguments[0]);
            }

            if (state is not null)
            {
                if (!ValidateHookSignature(method, context))
                    continue;

                builder.Add(new HookModel
                {
                    Method = method,
                    State = state
                });
            }
        }

        return builder.ToImmutable();
    }

    private string? GetEnumMemberName(TypedConstant constant, INamedTypeSymbol enumType)
    {
        return GetEnumMemberNameFromTypedConstant(constant);
    }

    private static string? GetEnumMemberNameFromTypedConstant(TypedConstant constant)
    {
        // The attribute stores enum values; we need the member name
        if (constant.Type is INamedTypeSymbol namedType && namedType.TypeKind == TypeKind.Enum)
        {
            var value = constant.Value;
            foreach (var member in namedType.GetMembers().OfType<IFieldSymbol>())
            {
                if (member.HasConstantValue && Equals(member.ConstantValue, value))
                {
                    return member.Name;
                }
            }
        }
        return null;
    }

    private bool ValidateTransitionSignature(IMethodSymbol method, SourceProductionContext context)
    {
        var returnsVoid = method.ReturnsVoid;
        var returnsValueTask = IsNonGenericValueTask(method.ReturnType);

        if (!returnsVoid && !returnsValueTask)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidTransitionSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        // Accept zero params or a single CancellationToken
        if (method.Parameters.Length > 1 ||
            (method.Parameters.Length == 1 && !GeneratorUtilities.IsCancellationToken(method.Parameters[0])))
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
        if (method.ReturnType.SpecialType != SpecialType.System_Boolean || method.Parameters.Length != 0)
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
        var returnsValueTask = IsNonGenericValueTask(method.ReturnType);

        if (!returnsVoid && !returnsValueTask)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidHookSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        if (method.Parameters.Length > 1 ||
            (method.Parameters.Length == 1 && !GeneratorUtilities.IsCancellationToken(method.Parameters[0])))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidHookSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        return true;
    }

    private bool ValidateUniqueTransitions(
        ImmutableArray<TransitionModel> transitions,
        INamedTypeSymbol typeSymbol,
        SourceProductionContext context)
    {
        var seen = new HashSet<string>();
        foreach (var t in transitions)
        {
            var key = $"{t.FromState}|{t.Trigger}";
            if (!seen.Add(key))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateTransitionDescriptor,
                    t.Method.Locations.FirstOrDefault(),
                    t.FromState,
                    t.Trigger,
                    typeSymbol.Name));
                return false;
            }
        }
        return true;
    }

    private static bool IsNonGenericValueTask(ITypeSymbol returnType)
    {
        return returnType is INamedTypeSymbol namedType &&
               namedType.Name == "ValueTask" &&
               namedType.Arity == 0 &&
               namedType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";
    }

    private string EmitSource(
        INamedTypeSymbol typeSymbol,
        StateConfig config,
        ImmutableArray<TransitionModel> transitions,
        ImmutableArray<GuardModel> guards,
        ImmutableArray<HookModel> entryHooks,
        ImmutableArray<HookModel> exitHooks,
        bool needsAsync)
    {
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeName = typeSymbol.Name;
        var typeKind = typeSymbol.TypeKind == TypeKind.Struct ? "struct" : "class";
        var recordKeyword = typeSymbol.IsRecord ? "record " : "";
        var stateFqn = config.StateType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var triggerFqn = config.TriggerType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"partial {recordKeyword}{typeKind} {typeName}");
        sb.AppendLine("{");

        // State property
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the current state of the state machine.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public {stateFqn} State {{ get; private set; }}");
        sb.AppendLine();

        // CanFire method
        EmitCanFire(sb, config, transitions, guards, stateFqn, triggerFqn);

        // Fire method (sync)
        sb.AppendLine();
        EmitFire(sb, config, transitions, guards, entryHooks, exitHooks, stateFqn, triggerFqn);

        // FireAsync method
        if (needsAsync)
        {
            sb.AppendLine();
            EmitFireAsync(sb, config, transitions, guards, entryHooks, exitHooks, stateFqn, triggerFqn);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private void EmitCanFire(
        StringBuilder sb,
        StateConfig config,
        ImmutableArray<TransitionModel> transitions,
        ImmutableArray<GuardModel> guards,
        string stateFqn,
        string triggerFqn)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Returns true if the specified trigger can be fired from the current state.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public bool {config.CanFireMethodName}({triggerFqn} trigger)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (State)");
        sb.AppendLine("        {");

        // Group transitions by FromState
        var byState = transitions.GroupBy(t => t.FromState).OrderBy(g => g.Key);
        foreach (var group in byState)
        {
            sb.AppendLine($"            case {stateFqn}.{group.Key}:");
            sb.AppendLine("                switch (trigger)");
            sb.AppendLine("                {");
            foreach (var t in group.OrderBy(t => t.Trigger))
            {
                var guard = guards.FirstOrDefault(g => g.FromState == t.FromState && g.Trigger == t.Trigger);
                if (guard is not null)
                {
                    sb.AppendLine($"                    case {triggerFqn}.{t.Trigger}:");
                    sb.AppendLine($"                        return {guard.Method.Name}();");
                }
                else
                {
                    sb.AppendLine($"                    case {triggerFqn}.{t.Trigger}:");
                    sb.AppendLine("                        return true;");
                }
            }
            sb.AppendLine("                    default:");
            sb.AppendLine("                        return false;");
            sb.AppendLine("                }");
        }

        sb.AppendLine("            default:");
        sb.AppendLine("                return false;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private void EmitFire(
        StringBuilder sb,
        StateConfig config,
        ImmutableArray<TransitionModel> transitions,
        ImmutableArray<GuardModel> guards,
        ImmutableArray<HookModel> entryHooks,
        ImmutableArray<HookModel> exitHooks,
        string stateFqn,
        string triggerFqn)
    {
        // InvalidTrigger: 0=Throw, 1=Ignore, 2=ReturnFalse
        var returnsBool = config.InvalidTrigger == 2 || config.GuardFailure == 2;
        var returnType = returnsBool ? "bool" : "void";

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Fires the specified trigger, executing the transition if valid.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public {returnType} {config.FireMethodName}({triggerFqn} trigger)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (State)");
        sb.AppendLine("        {");

        var byState = transitions.GroupBy(t => t.FromState).OrderBy(g => g.Key);
        foreach (var group in byState)
        {
            sb.AppendLine($"            case {stateFqn}.{group.Key}:");
            sb.AppendLine("                switch (trigger)");
            sb.AppendLine("                {");
            foreach (var t in group.OrderBy(t => t.Trigger))
            {
                sb.AppendLine($"                    case {triggerFqn}.{t.Trigger}:");
                sb.AppendLine("                    {");

                // Guard check
                var guard = guards.FirstOrDefault(g => g.FromState == t.FromState && g.Trigger == t.Trigger);
                if (guard is not null)
                {
                    sb.AppendLine($"                        if (!{guard.Method.Name}())");
                    EmitGuardFailure(sb, config, returnsBool);
                }

                // Exit hooks
                foreach (var hook in exitHooks.Where(h => h.State == t.FromState).OrderBy(h => h.Method.Name))
                {
                    var hasCt = hook.Method.Parameters.Any(GeneratorUtilities.IsCancellationToken);
                    sb.AppendLine($"                        {hook.Method.Name}({(hasCt ? "default" : "")});");
                }

                // Transition action
                {
                    var hasCt = t.Method.Parameters.Any(GeneratorUtilities.IsCancellationToken);
                    sb.AppendLine($"                        {t.Method.Name}({(hasCt ? "default" : "")});");
                }

                // State update
                sb.AppendLine($"                        State = {stateFqn}.{t.ToState};");

                // Entry hooks
                foreach (var hook in entryHooks.Where(h => h.State == t.ToState).OrderBy(h => h.Method.Name))
                {
                    var hasCt = hook.Method.Parameters.Any(GeneratorUtilities.IsCancellationToken);
                    sb.AppendLine($"                        {hook.Method.Name}({(hasCt ? "default" : "")});");
                }

                if (returnsBool)
                    sb.AppendLine("                        return true;");
                else
                    sb.AppendLine("                        return;");
                sb.AppendLine("                    }");
            }
            sb.AppendLine("                    default:");
            EmitInvalidTrigger(sb, config, returnsBool, stateFqn, triggerFqn);
            sb.AppendLine("                }");
        }

        // Default case for states with no transitions
        sb.AppendLine("            default:");
        EmitInvalidTriggerDefault(sb, config, returnsBool, stateFqn, triggerFqn);
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private void EmitFireAsync(
        StringBuilder sb,
        StateConfig config,
        ImmutableArray<TransitionModel> transitions,
        ImmutableArray<GuardModel> guards,
        ImmutableArray<HookModel> entryHooks,
        ImmutableArray<HookModel> exitHooks,
        string stateFqn,
        string triggerFqn)
    {
        var returnsBool = config.InvalidTrigger == 2 || config.GuardFailure == 2;
        var returnType = returnsBool
            ? "System.Threading.Tasks.ValueTask<bool>"
            : "System.Threading.Tasks.ValueTask";

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Fires the specified trigger asynchronously, executing the transition if valid.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public async {returnType} {config.FireAsyncMethodName}({triggerFqn} trigger, System.Threading.CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (State)");
        sb.AppendLine("        {");

        var byState = transitions.GroupBy(t => t.FromState).OrderBy(g => g.Key);
        foreach (var group in byState)
        {
            sb.AppendLine($"            case {stateFqn}.{group.Key}:");
            sb.AppendLine("                switch (trigger)");
            sb.AppendLine("                {");
            foreach (var t in group.OrderBy(t => t.Trigger))
            {
                sb.AppendLine($"                    case {triggerFqn}.{t.Trigger}:");
                sb.AppendLine("                    {");

                // Guard check
                var guard = guards.FirstOrDefault(g => g.FromState == t.FromState && g.Trigger == t.Trigger);
                if (guard is not null)
                {
                    sb.AppendLine($"                        if (!{guard.Method.Name}())");
                    EmitGuardFailureAsync(sb, config, returnsBool);
                }

                // Exit hooks
                foreach (var hook in exitHooks.Where(h => h.State == t.FromState).OrderBy(h => h.Method.Name))
                {
                    var isAsync = IsNonGenericValueTask(hook.Method.ReturnType);
                    var hasCt = hook.Method.Parameters.Any(GeneratorUtilities.IsCancellationToken);
                    if (isAsync)
                        sb.AppendLine($"                        await {hook.Method.Name}({(hasCt ? "ct" : "")}).ConfigureAwait(false);");
                    else
                        sb.AppendLine($"                        {hook.Method.Name}();");
                }

                // Transition action
                var tIsAsync = IsNonGenericValueTask(t.Method.ReturnType);
                var tHasCt = t.Method.Parameters.Any(GeneratorUtilities.IsCancellationToken);
                if (tIsAsync)
                    sb.AppendLine($"                        await {t.Method.Name}({(tHasCt ? "ct" : "")}).ConfigureAwait(false);");
                else
                    sb.AppendLine($"                        {t.Method.Name}();");

                // State update
                sb.AppendLine($"                        State = {stateFqn}.{t.ToState};");

                // Entry hooks
                foreach (var hook in entryHooks.Where(h => h.State == t.ToState).OrderBy(h => h.Method.Name))
                {
                    var isAsync = IsNonGenericValueTask(hook.Method.ReturnType);
                    var hasCt = hook.Method.Parameters.Any(GeneratorUtilities.IsCancellationToken);
                    if (isAsync)
                        sb.AppendLine($"                        await {hook.Method.Name}({(hasCt ? "ct" : "")}).ConfigureAwait(false);");
                    else
                        sb.AppendLine($"                        {hook.Method.Name}();");
                }

                if (returnsBool)
                    sb.AppendLine("                        return true;");
                else
                    sb.AppendLine("                        return;");
                sb.AppendLine("                    }");
            }
            sb.AppendLine("                    default:");
            EmitInvalidTriggerAsync(sb, config, returnsBool, stateFqn, triggerFqn);
            sb.AppendLine("                }");
        }

        sb.AppendLine("            default:");
        EmitInvalidTriggerDefaultAsync(sb, config, returnsBool, stateFqn, triggerFqn);
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private void EmitGuardFailure(StringBuilder sb, StateConfig config, bool returnsBool)
    {
        // GuardFailure: 0=Throw, 1=Ignore, 2=ReturnFalse
        if (config.GuardFailure == 0)
        {
            sb.AppendLine("                            throw new System.InvalidOperationException(\"Guard condition failed for transition.\");");
        }
        else if (config.GuardFailure == 1)
        {
            if (returnsBool)
                sb.AppendLine("                            return false;");
            else
                sb.AppendLine("                            return;");
        }
        else
        {
            sb.AppendLine("                            return false;");
        }
    }

    private void EmitGuardFailureAsync(StringBuilder sb, StateConfig config, bool returnsBool)
    {
        if (config.GuardFailure == 0)
        {
            sb.AppendLine("                            throw new System.InvalidOperationException(\"Guard condition failed for transition.\");");
        }
        else if (config.GuardFailure == 1)
        {
            if (returnsBool)
                sb.AppendLine("                            return false;");
            else
                sb.AppendLine("                            return;");
        }
        else
        {
            sb.AppendLine("                            return false;");
        }
    }

    private void EmitInvalidTrigger(StringBuilder sb, StateConfig config, bool returnsBool, string stateFqn, string triggerFqn)
    {
        // InvalidTrigger: 0=Throw, 1=Ignore, 2=ReturnFalse
        if (config.InvalidTrigger == 0)
        {
            sb.AppendLine("                        throw new System.InvalidOperationException($\"No valid transition from state {State} for trigger {trigger}.\");");
        }
        else if (config.InvalidTrigger == 1)
        {
            if (returnsBool)
                sb.AppendLine("                        return false;");
            else
                sb.AppendLine("                        return;");
        }
        else
        {
            sb.AppendLine("                        return false;");
        }
    }

    private void EmitInvalidTriggerDefault(StringBuilder sb, StateConfig config, bool returnsBool, string stateFqn, string triggerFqn)
    {
        if (config.InvalidTrigger == 0)
        {
            sb.AppendLine("                throw new System.InvalidOperationException($\"No valid transition from state {State} for trigger {trigger}.\");");
        }
        else if (config.InvalidTrigger == 1)
        {
            if (returnsBool)
                sb.AppendLine("                return false;");
            else
                sb.AppendLine("                return;");
        }
        else
        {
            sb.AppendLine("                return false;");
        }
    }

    private void EmitInvalidTriggerAsync(StringBuilder sb, StateConfig config, bool returnsBool, string stateFqn, string triggerFqn)
    {
        EmitInvalidTrigger(sb, config, returnsBool, stateFqn, triggerFqn);
    }

    private void EmitInvalidTriggerDefaultAsync(StringBuilder sb, StateConfig config, bool returnsBool, string stateFqn, string triggerFqn)
    {
        EmitInvalidTriggerDefault(sb, config, returnsBool, stateFqn, triggerFqn);
    }

    private class StateConfig
    {
        public INamedTypeSymbol? StateType { get; set; }
        public INamedTypeSymbol? TriggerType { get; set; }
        public string FireMethodName { get; set; } = "Fire";
        public string FireAsyncMethodName { get; set; } = "FireAsync";
        public string CanFireMethodName { get; set; } = "CanFire";
        public bool GenerateAsync { get; set; }
        public bool ForceAsync { get; set; }
        public int InvalidTrigger { get; set; } // 0=Throw, 1=Ignore, 2=ReturnFalse
        public int GuardFailure { get; set; } // 0=Throw, 1=Ignore, 2=ReturnFalse
    }

    private class TransitionModel
    {
        public IMethodSymbol Method { get; set; } = null!;
        public string FromState { get; set; } = null!;
        public string Trigger { get; set; } = null!;
        public string ToState { get; set; } = null!;
    }

    private class GuardModel
    {
        public IMethodSymbol Method { get; set; } = null!;
        public string FromState { get; set; } = null!;
        public string Trigger { get; set; } = null!;
    }

    private class HookModel
    {
        public IMethodSymbol Method { get; set; } = null!;
        public string State { get; set; } = null!;
    }
}
