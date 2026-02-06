using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Template Method pattern.
/// Generates Execute/ExecuteAsync methods that invoke steps and hooks in deterministic order.
/// </summary>
[Generator]
public sealed class TemplateGenerator : IIncrementalGenerator
{
    // Diagnostic IDs
    private const string DiagIdTypeNotPartial = "PKTMP001";
    private const string DiagIdNoSteps = "PKTMP002";
    private const string DiagIdDuplicateOrder = "PKTMP003";
    private const string DiagIdInvalidStepSignature = "PKTMP004";
    private const string DiagIdInvalidHookSignature = "PKTMP005";
    private const string DiagIdMissingCancellationToken = "PKTMP007";
    private const string DiagIdHandleAndContinuePolicy = "PKTMP008";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type marked with [Template] must be partial",
        messageFormat: "Type '{0}' is marked with [Template] but is not declared as partial. Add the 'partial' keyword to the type declaration.",
        category: "PatternKit.Generators.Template",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoStepsDescriptor = new(
        id: DiagIdNoSteps,
        title: "No template steps found",
        messageFormat: "Type '{0}' has [Template] but no methods marked with [TemplateStep]. At least one step is required.",
        category: "PatternKit.Generators.Template",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateOrderDescriptor = new(
        id: DiagIdDuplicateOrder,
        title: "Duplicate step order detected",
        messageFormat: "Multiple steps have Order={0} in type '{1}'. Step orders must be unique. Conflicting steps: {2}.",
        category: "PatternKit.Generators.Template",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidStepSignatureDescriptor = new(
        id: DiagIdInvalidStepSignature,
        title: "Invalid step method signature",
        messageFormat: "Step method '{0}' has an invalid signature. Steps must return void or ValueTask and accept a context parameter (optionally with CancellationToken for async).",
        category: "PatternKit.Generators.Template",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidHookSignatureDescriptor = new(
        id: DiagIdInvalidHookSignature,
        title: "Invalid hook method signature",
        messageFormat: "Hook method '{0}' has an invalid signature. {1}.",
        category: "PatternKit.Generators.Template",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingCancellationTokenDescriptor = new(
        id: DiagIdMissingCancellationToken,
        title: "CancellationToken parameter required for async step",
        messageFormat: "Async step method '{0}' should accept a CancellationToken parameter for proper cancellation support",
        category: "PatternKit.Generators.Template",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor HandleAndContinuePolicyDescriptor = new(
        id: DiagIdHandleAndContinuePolicy,
        title: "HandleAndContinue policy not allowed with non-optional steps",
        messageFormat: "ErrorPolicy=HandleAndContinue is not allowed when non-optional steps exist. Make all steps optional or use ErrorPolicy=Rethrow.",
        category: "PatternKit.Generators.Template",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all type declarations with [Template] attribute
        var templateTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Template.TemplateAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        // Generate for each type
        context.RegisterSourceOutput(templateTypes, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Template.TemplateAttribute");
            if (attr is null)
                return;

            GenerateTemplateForType(spc, typeSymbol, attr, typeContext.TargetNode);
        });
    }

    private void GenerateTemplateForType(
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
        var config = ParseTemplateConfig(attribute);

        // Collect steps and hooks
        var steps = CollectSteps(typeSymbol, context);
        var hooks = CollectHooks(typeSymbol, context);

        // Validate steps exist
        if (steps.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NoStepsDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Validate step ordering
        if (!ValidateStepOrdering(steps, typeSymbol, context))
            return;

        // Validate signatures
        if (!ValidateSignatures(steps, hooks, typeSymbol, context))
            return;

        // Determine if async generation is needed
        var needsAsync = config.ForceAsync ||
                        config.GenerateAsync ||
                        DetermineIfAsync(steps, hooks);

        // Validate error policy
        if (config.ErrorPolicy == 1 && !ValidateHandleAndContinuePolicy(steps, context))
            return;

        // Generate the template method implementation
        var source = GenerateTemplateMethod(typeSymbol, config, steps, hooks, needsAsync);
        var fileName = $"{typeSymbol.Name}.Template.g.cs";
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

    private TemplateConfig ParseTemplateConfig(AttributeData attribute)
    {
        var config = new TemplateConfig();

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "ExecuteMethodName":
                    config.ExecuteMethodName = namedArg.Value.Value?.ToString() ?? "Execute";
                    break;
                case "ExecuteAsyncMethodName":
                    config.ExecuteAsyncMethodName = namedArg.Value.Value?.ToString() ?? "ExecuteAsync";
                    break;
                case "GenerateAsync":
                    config.GenerateAsync = namedArg.Value.Value is bool b && b;
                    break;
                case "ForceAsync":
                    config.ForceAsync = namedArg.Value.Value is bool f && f;
                    break;
                case "ErrorPolicy":
                    config.ErrorPolicy = namedArg.Value.Value is int policy ? policy : 0;
                    break;
            }
        }

        return config;
    }

    private ImmutableArray<StepModel> CollectSteps(INamedTypeSymbol typeSymbol, SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<StepModel>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var stepAttr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Template.TemplateStepAttribute");

            if (stepAttr is null)
                continue;

            // Extract order from constructor argument
            var order = stepAttr.ConstructorArguments.Length > 0 &&
                       stepAttr.ConstructorArguments[0].Value is int o ? o : 0;

            string? name = null;
            var optional = false;

            foreach (var namedArg in stepAttr.NamedArguments)
            {
                if (namedArg.Key == "Name")
                    name = namedArg.Value.Value?.ToString();
                else if (namedArg.Key == "Optional")
                    optional = namedArg.Value.Value is bool b && b;
            }

            builder.Add(new StepModel
            {
                Method = method,
                Order = order,
                Name = name ?? method.Name,
                Optional = optional
            });
        }

        return builder.ToImmutable();
    }

    private ImmutableArray<HookModel> CollectHooks(INamedTypeSymbol typeSymbol, SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<HookModel>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var hookAttr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Template.TemplateHookAttribute");

            if (hookAttr is null)
                continue;

            // Extract hook point from constructor argument
            var hookPoint = hookAttr.ConstructorArguments.Length > 0 &&
                           hookAttr.ConstructorArguments[0].Value is int hp ? hp : 0;

            builder.Add(new HookModel
            {
                Method = method,
                HookPoint = hookPoint
            });
        }

        return builder.ToImmutable();
    }

    private bool ValidateStepOrdering(
        ImmutableArray<StepModel> steps,
        INamedTypeSymbol typeSymbol,
        SourceProductionContext context)
    {
        var orderGroups = steps.GroupBy(s => s.Order).Where(g => g.Count() > 1);
        foreach (var group in orderGroups)
        {
            var stepNames = string.Join(", ", group.Select(s => s.Name));
            context.ReportDiagnostic(Diagnostic.Create(
                DuplicateOrderDescriptor,
                Location.None,
                group.Key,
                typeSymbol.Name,
                stepNames));
            return false;
        }
        return true;
    }

    private bool ValidateSignatures(
        ImmutableArray<StepModel> steps,
        ImmutableArray<HookModel> hooks,
        INamedTypeSymbol typeSymbol,
        SourceProductionContext context)
    {
        // Validate step signatures - return false if any validation fails
        if (steps.Any(step => !ValidateStepSignature(step.Method, context)))
            return false;

        // Validate hook signatures - return false if any validation fails
        if (hooks.Any(hook => !ValidateHookSignature(hook.Method, hook.HookPoint, context)))
            return false;

        return true;
    }

    private bool ValidateStepSignature(IMethodSymbol method, SourceProductionContext context)
    {
        // Step must return void or non-generic ValueTask
        var returnsVoid = method.ReturnsVoid;
        var returnType = method.ReturnType;
        var returnsValueTask = returnType is INamedTypeSymbol namedType &&
                              namedType.Name == "ValueTask" &&
                              namedType.Arity == 0 &&
                              namedType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";

        if (!returnsVoid && !returnsValueTask)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidStepSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        // Step must have at least one parameter (context)
        if (method.Parameters.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidStepSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        // If async, recommend CancellationToken parameter
        if (returnsValueTask && method.Parameters.Length == 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingCancellationTokenDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
        }

        return true;
    }

    private bool ValidateHookSignature(IMethodSymbol method, int hookPoint, SourceProductionContext context)
    {
        // Hook must return void or non-generic ValueTask
        var returnsVoid = method.ReturnsVoid;
        var returnType = method.ReturnType;
        var returnsValueTask = returnType is INamedTypeSymbol namedType &&
                              namedType.Name == "ValueTask" &&
                              namedType.Arity == 0 &&
                              namedType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";

        if (!returnsVoid && !returnsValueTask)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidHookSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name,
                "Hook must return void or ValueTask."));
            return false;
        }

        // OnError hook must accept Exception parameter
        if (hookPoint == 2) // OnError
        {
            if (method.Parameters.Length < 2)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidHookSignatureDescriptor,
                    method.Locations.FirstOrDefault(),
                    method.Name,
                    "OnError hook must accept context and Exception parameters."));
                return false;
            }
        }
        else
        {
            // BeforeAll/AfterAll hooks need at least context parameter
            if (method.Parameters.Length == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidHookSignatureDescriptor,
                    method.Locations.FirstOrDefault(),
                    method.Name,
                    "Hook must accept at least a context parameter."));
                return false;
            }
        }

        return true;
    }

    private bool DetermineIfAsync(ImmutableArray<StepModel> steps, ImmutableArray<HookModel> hooks)
    {
        // Check if any step or hook returns non-generic ValueTask or accepts CancellationToken
        foreach (var step in steps)
        {
            var returnType = step.Method.ReturnType;
            if (returnType is INamedTypeSymbol namedType &&
                namedType.Name == "ValueTask" &&
                namedType.Arity == 0 &&
                namedType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks")
                return true;

            if (step.Method.Parameters.Any(IsCancellationToken))
                return true;
        }

        foreach (var hook in hooks)
        {
            var returnType = hook.Method.ReturnType;
            if (returnType is INamedTypeSymbol namedType &&
                namedType.Name == "ValueTask" &&
                namedType.Arity == 0 &&
                namedType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks")
                return true;

            if (hook.Method.Parameters.Any(IsCancellationToken))
                return true;
        }

        return false;
    }

    private bool ValidateHandleAndContinuePolicy(
        ImmutableArray<StepModel> steps,
        SourceProductionContext context)
    {
        // For HandleAndContinue, verify all steps are optional
        var nonOptionalSteps = steps.Where(s => !s.Optional).ToList();
        if (nonOptionalSteps.Count > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                HandleAndContinuePolicyDescriptor,
                Location.None));
            return false;
        }

        return true;
    }

    private string GenerateTemplateMethod(
        INamedTypeSymbol typeSymbol,
        TemplateConfig config,
        ImmutableArray<StepModel> steps,
        ImmutableArray<HookModel> hooks,
        bool needsAsync)
    {
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? "GlobalNamespace"
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeName = typeSymbol.Name;
        var typeKind = typeSymbol.TypeKind == TypeKind.Struct ? "struct" : "class";
        var recordKeyword = typeSymbol.IsRecord ? "record " : "";

        // Sort steps by order
        var sortedSteps = steps.OrderBy(s => s.Order).ThenBy(s => s.Name).ToList();

        // Get context type from first step
        var contextType = sortedSteps[0].Method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Group hooks
        var beforeAllHooks = hooks.Where(h => h.HookPoint == 0).ToList();
        var afterAllHooks = hooks.Where(h => h.HookPoint == 1).ToList();
        var onErrorHooks = hooks.Where(h => h.HookPoint == 2).ToList();

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns != "GlobalNamespace")
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        // Type declaration
        sb.AppendLine($"partial {recordKeyword}{typeKind} {typeName}");
        sb.AppendLine("{");

        // Generate synchronous Execute method only if no async methods
        if (!needsAsync)
        {
            sb.AppendLine($"    public void {config.ExecuteMethodName}({contextType} ctx)");
            sb.AppendLine("    {");

            // BeforeAll hooks
            foreach (var hook in beforeAllHooks)
            {
                sb.AppendLine($"        {hook.Method.Name}(ctx);");
            }

            // Error handling wrapper if OnError hooks exist
            if (onErrorHooks.Count > 0)
            {
                sb.AppendLine("        try");
                sb.AppendLine("        {");

                // Steps
                foreach (var step in sortedSteps)
                {
                    sb.AppendLine($"            {step.Method.Name}(ctx);");
                }

                // AfterAll hooks (inside try - only execute on success)
                foreach (var hook in afterAllHooks)
                {
                    sb.AppendLine($"            {hook.Method.Name}(ctx);");
                }

                sb.AppendLine("        }");
                sb.AppendLine("        catch (System.Exception ex)");
                sb.AppendLine("        {");

                // OnError hooks
                foreach (var hook in onErrorHooks)
                {
                    sb.AppendLine($"            {hook.Method.Name}(ctx, ex);");
                }

                if (config.ErrorPolicy == 0) // Rethrow
                {
                    sb.AppendLine("            throw;");
                }

                sb.AppendLine("        }");
            }
            else
            {
                // Steps without try-catch
                foreach (var step in sortedSteps)
                {
                    sb.AppendLine($"        {step.Method.Name}(ctx);");
                }

                // AfterAll hooks (no error handling)
                foreach (var hook in afterAllHooks)
                {
                    sb.AppendLine($"        {hook.Method.Name}(ctx);");
                }
            }

            sb.AppendLine("    }");
        }
        // Generate asynchronous ExecuteAsync method if needed
        if (needsAsync)
        {
            sb.AppendLine();
            sb.AppendLine($"    public async System.Threading.Tasks.ValueTask {config.ExecuteAsyncMethodName}({contextType} ctx, System.Threading.CancellationToken ct = default)");
            sb.AppendLine("    {");

            // BeforeAll hooks
            foreach (var hook in beforeAllHooks)
            {
                var isAsync = IsNonGenericValueTask(hook.Method.ReturnType);
                if (isAsync)
                {
                    var hasCt = hook.Method.Parameters.Any(IsCancellationToken);
                    var args = hasCt ? "ctx, ct" : "ctx";
                    sb.AppendLine($"        await {hook.Method.Name}({args}).ConfigureAwait(false);");
                }
                else
                {
                    sb.AppendLine($"        {hook.Method.Name}(ctx);");
                }
            }

            // Error handling wrapper if OnError hooks exist
            if (onErrorHooks.Count > 0)
            {
                sb.AppendLine("        try");
                sb.AppendLine("        {");

                // Steps
                foreach (var step in sortedSteps)
                {
                    var isAsync = IsNonGenericValueTask(step.Method.ReturnType);
                    if (isAsync)
                    {
                        var hasCt = step.Method.Parameters.Any(IsCancellationToken);
                        var args = hasCt ? "ctx, ct" : "ctx";
                        sb.AppendLine($"            await {step.Method.Name}({args}).ConfigureAwait(false);");
                    }
                    else
                    {
                        sb.AppendLine($"            {step.Method.Name}(ctx);");
                    }
                }

                // AfterAll hooks (inside try - only execute on success)
                foreach (var hook in afterAllHooks)
                {
                    var isAsync = IsNonGenericValueTask(hook.Method.ReturnType);
                    if (isAsync)
                    {
                        var hasCt = hook.Method.Parameters.Any(IsCancellationToken);
                        var args = hasCt ? "ctx, ct" : "ctx";
                        sb.AppendLine($"            await {hook.Method.Name}({args}).ConfigureAwait(false);");
                    }
                    else
                    {
                        sb.AppendLine($"            {hook.Method.Name}(ctx);");
                    }
                }

                sb.AppendLine("        }");
                sb.AppendLine("        catch (System.Exception ex)");
                sb.AppendLine("        {");

                // OnError hooks
                foreach (var hook in onErrorHooks)
                {
                    var isAsync = IsNonGenericValueTask(hook.Method.ReturnType);
                    if (isAsync)
                    {
                        var hasCt = hook.Method.Parameters.Any(IsCancellationToken);
                        var args = hasCt ? "ctx, ex, ct" : "ctx, ex";
                        sb.AppendLine($"            await {hook.Method.Name}({args}).ConfigureAwait(false);");
                    }
                    else
                    {
                        sb.AppendLine($"            {hook.Method.Name}(ctx, ex);");
                    }
                }

                if (config.ErrorPolicy == 0) // Rethrow
                {
                    sb.AppendLine("            throw;");
                }

                sb.AppendLine("        }");
            }
            else
            {
                // Steps without try-catch
                foreach (var step in sortedSteps)
                {
                    var isAsync = IsNonGenericValueTask(step.Method.ReturnType);
                    if (isAsync)
                    {
                        var hasCt = step.Method.Parameters.Any(IsCancellationToken);
                        var args = hasCt ? "ctx, ct" : "ctx";
                        sb.AppendLine($"        await {step.Method.Name}({args}).ConfigureAwait(false);");
                    }
                    else
                    {
                        sb.AppendLine($"        {step.Method.Name}(ctx);");
                    }
                }

                // AfterAll hooks (no error handling)
                foreach (var hook in afterAllHooks)
                {
                    var isAsync = IsNonGenericValueTask(hook.Method.ReturnType);
                    if (isAsync)
                    {
                        var hasCt = hook.Method.Parameters.Any(IsCancellationToken);
                        var args = hasCt ? "ctx, ct" : "ctx";
                        sb.AppendLine($"        await {hook.Method.Name}({args}).ConfigureAwait(false);");
                    }
                    else
                    {
                        sb.AppendLine($"        {hook.Method.Name}(ctx);");
                    }
                }
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    // Helper method to check if a return type is non-generic ValueTask
    private static bool IsNonGenericValueTask(ITypeSymbol returnType)
    {
        return returnType is INamedTypeSymbol namedType &&
               namedType.Name == "ValueTask" &&
               namedType.Arity == 0 &&
               namedType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";
    }

    // Helper method to check if a parameter is a CancellationToken
    private static bool IsCancellationToken(IParameterSymbol parameter)
    {
        return parameter.Type.ToDisplayString() == "System.Threading.CancellationToken";
    }

    // Helper classes
    private class TemplateConfig
    {
        public string ExecuteMethodName { get; set; } = "Execute";
        public string ExecuteAsyncMethodName { get; set; } = "ExecuteAsync";
        public bool GenerateAsync { get; set; }
        public bool ForceAsync { get; set; }
        public int ErrorPolicy { get; set; } // 0 = Rethrow, 1 = HandleAndContinue
    }

    private class StepModel
    {
        public IMethodSymbol Method { get; set; } = null!;
        public int Order { get; set; }
        public string Name { get; set; } = null!;
        public bool Optional { get; set; }
    }

    private class HookModel
    {
        public IMethodSymbol Method { get; set; } = null!;
        public int HookPoint { get; set; } // 0=BeforeAll, 1=AfterAll, 2=OnError
    }
}
