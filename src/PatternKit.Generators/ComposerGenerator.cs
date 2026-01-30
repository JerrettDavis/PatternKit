using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PatternKit.Generators.Composer;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Composer pattern.
/// Generates deterministic composition of ordered pipeline components into executable pipelines.
/// </summary>
[Generator]
public sealed class ComposerGenerator : IIncrementalGenerator
{
    // Diagnostic IDs
    private const string DiagIdNotPartial = "PKCOM001";
    private const string DiagIdNoSteps = "PKCOM002";
    private const string DiagIdDuplicateOrder = "PKCOM003";
    private const string DiagIdNoTerminal = "PKCOM004";
    private const string DiagIdMultipleTerminals = "PKCOM005";
    private const string DiagIdInvalidStepSignature = "PKCOM006";
    private const string DiagIdInvalidTerminalSignature = "PKCOM007";
    private const string DiagIdAsyncNotEnabled = "PKCOM008";
    private const string DiagIdMissingCancellationToken = "PKCOM009";

    private static readonly DiagnosticDescriptor NotPartialDescriptor = new(
        id: DiagIdNotPartial,
        title: "Composer type must be partial",
        messageFormat: "Type '{0}' marked with [Composer] must be declared as partial",
        category: "PatternKit.Generators.Composer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoStepsDescriptor = new(
        id: DiagIdNoSteps,
        title: "No compose steps found",
        messageFormat: "Type '{0}' marked with [Composer] has no methods marked with [ComposeStep]",
        category: "PatternKit.Generators.Composer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateOrderDescriptor = new(
        id: DiagIdDuplicateOrder,
        title: "Duplicate step order",
        messageFormat: "Multiple steps have Order={0}. Each step must have a unique Order value",
        category: "PatternKit.Generators.Composer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoTerminalDescriptor = new(
        id: DiagIdNoTerminal,
        title: "Missing terminal step",
        messageFormat: "Type '{0}' marked with [Composer] must have exactly one method marked with [ComposeTerminal]",
        category: "PatternKit.Generators.Composer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MultipleTerminalsDescriptor = new(
        id: DiagIdMultipleTerminals,
        title: "Multiple terminal steps",
        messageFormat: "Type '{0}' has multiple methods marked with [ComposeTerminal]. Only one terminal is allowed",
        category: "PatternKit.Generators.Composer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidStepSignatureDescriptor = new(
        id: DiagIdInvalidStepSignature,
        title: "Invalid step method signature",
        messageFormat: "Method '{0}' has an invalid signature for a pipeline step. Expected: TOut Step(in TIn, Func<TIn, TOut> next) or ValueTask<TOut> StepAsync(TIn, Func<TIn, ValueTask<TOut>> next, CancellationToken)",
        category: "PatternKit.Generators.Composer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidTerminalSignatureDescriptor = new(
        id: DiagIdInvalidTerminalSignature,
        title: "Invalid terminal method signature",
        messageFormat: "Method '{0}' has an invalid signature for a terminal. Expected: TOut Terminal(in TIn) or ValueTask<TOut> TerminalAsync(TIn, CancellationToken)",
        category: "PatternKit.Generators.Composer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AsyncNotEnabledDescriptor = new(
        id: DiagIdAsyncNotEnabled,
        title: "Async step detected but async generation disabled",
        messageFormat: "Method '{0}' is async but async generation is disabled. Set GenerateAsync=true or ForceAsync=true on [Composer]",
        category: "PatternKit.Generators.Composer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingCancellationTokenDescriptor = new(
        id: DiagIdMissingCancellationToken,
        title: "CancellationToken parameter required",
        messageFormat: "Method '{0}' is async but missing CancellationToken parameter. Async methods should have a CancellationToken parameter",
        category: "PatternKit.Generators.Composer",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all types marked with [Composer]
        var composerTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Composer.ComposerAttribute",
            predicate: static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        // Generate for each composer type
        context.RegisterSourceOutput(composerTypes, (spc, composerContext) =>
        {
            if (composerContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attr = composerContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Composer.ComposerAttribute");
            if (attr is null)
                return;

            GenerateComposer(spc, typeSymbol, attr, composerContext.TargetNode);
        });
    }

    private void GenerateComposer(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attribute,
        SyntaxNode node)
    {
        // Check if type is partial
        if (!IsPartial(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotPartialDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Parse attribute configuration
        var config = ParseComposerConfig(attribute);

        // Find all steps and terminal
        var steps = FindSteps(typeSymbol, context);
        var terminals = FindTerminals(typeSymbol, context);

        // Validate we have steps
        if (steps.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NoStepsDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Validate exactly one terminal
        if (terminals.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NoTerminalDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        if (terminals.Count > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MultipleTerminalsDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        var terminal = terminals[0];

        // Check for duplicate orders
        var orderGroups = steps.GroupBy(s => s.Order).Where(g => g.Count() > 1).ToList();
        if (orderGroups.Any())
        {
            foreach (var group in orderGroups)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateOrderDescriptor,
                    group.First().Method.Locations.FirstOrDefault() ?? node.GetLocation(),
                    group.Key));
            }
            return;
        }

        // Validate signatures and determine async mode
        bool hasAsyncSteps = steps.Any(s => s.IsAsync) || terminal.IsAsync;
        bool shouldGenerateAsync = config.ForceAsync || (config.GenerateAsync ?? hasAsyncSteps);

        if (hasAsyncSteps && config.GenerateAsync == false)
        {
            var asyncStep = steps.FirstOrDefault(s => s.IsAsync);
            var methodToReport = asyncStep?.Method ?? terminal.Method;
            context.ReportDiagnostic(Diagnostic.Create(
                AsyncNotEnabledDescriptor,
                methodToReport.Locations.FirstOrDefault() ?? node.GetLocation(),
                methodToReport.Name));
            return;
        }

        // Validate step signatures
        foreach (var step in steps)
        {
            if (!ValidateStepSignature(step, context))
                return;
        }

        // Validate terminal signature
        if (!ValidateTerminalSignature(terminal, context))
            return;

        // Determine pipeline input and output types
        var inputType = DetermineInputType(steps, terminal);
        var outputType = DetermineOutputType(steps, terminal);

        if (inputType == null || outputType == null)
        {
            // Signature validation should have caught this, but be safe
            return;
        }

        // Sort steps by order
        var orderedSteps = config.WrapOrder == ComposerWrapOrder.OuterFirst
            ? steps.OrderBy(s => s.Order).ToList()
            : steps.OrderByDescending(s => s.Order).ToList();

        // Generate the source
        var source = GenerateSource(typeSymbol, config, orderedSteps, terminal, inputType, outputType, shouldGenerateAsync);

        var fileName = $"{typeSymbol.Name}.Composer.g.cs";
        context.AddSource(fileName, source);
    }

    private ComposerConfig ParseComposerConfig(AttributeData attribute)
    {
        var config = new ComposerConfig();

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "InvokeMethodName":
                    if (namedArg.Value.Value is string invokeName && !string.IsNullOrWhiteSpace(invokeName))
                        config.InvokeMethodName = invokeName;
                    break;
                case "InvokeAsyncMethodName":
                    if (namedArg.Value.Value is string invokeAsyncName && !string.IsNullOrWhiteSpace(invokeAsyncName))
                        config.InvokeAsyncMethodName = invokeAsyncName;
                    break;
                case "GenerateAsync":
                    if (namedArg.Value.Value is bool genAsync)
                        config.GenerateAsync = genAsync;
                    break;
                case "ForceAsync":
                    if (namedArg.Value.Value is bool forceAsync)
                        config.ForceAsync = forceAsync;
                    break;
                case "WrapOrder":
                    if (namedArg.Value.Value is int wrapOrder && wrapOrder >= 0 && wrapOrder <= 1)
                        config.WrapOrder = (ComposerWrapOrder)wrapOrder;
                    break;
            }
        }

        return config;
    }

    private List<StepInfo> FindSteps(INamedTypeSymbol typeSymbol, SourceProductionContext context)
    {
        var steps = new List<StepInfo>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var stepAttr = method.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Composer.ComposeStepAttribute");

            if (stepAttr == null)
                continue;

            // Check for [ComposeIgnore]
            var ignoreAttr = method.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Composer.ComposeIgnoreAttribute");
            if (ignoreAttr != null)
                continue;

            int order = 0;
            string? name = null;

            if (stepAttr.ConstructorArguments.Length > 0 &&
                stepAttr.ConstructorArguments[0].Value is int orderValue)
            {
                order = orderValue;
            }

            foreach (var namedArg in stepAttr.NamedArguments)
            {
                if (namedArg.Key == "Order" && namedArg.Value.Value is int orderVal)
                    order = orderVal;
                else if (namedArg.Key == "Name" && namedArg.Value.Value is string nameVal)
                    name = nameVal;
            }

            bool isAsync = IsAsyncMethod(method);

            steps.Add(new StepInfo
            {
                Method = method,
                Order = order,
                Name = name ?? method.Name,
                IsAsync = isAsync
            });
        }

        return steps;
    }

    private List<TerminalInfo> FindTerminals(INamedTypeSymbol typeSymbol, SourceProductionContext context)
    {
        var terminals = new List<TerminalInfo>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var terminalAttr = method.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Composer.ComposeTerminalAttribute");

            if (terminalAttr == null)
                continue;

            bool isAsync = IsAsyncMethod(method);

            terminals.Add(new TerminalInfo
            {
                Method = method,
                IsAsync = isAsync
            });
        }

        return terminals;
    }

    private bool ValidateStepSignature(StepInfo step, SourceProductionContext context)
    {
        var method = step.Method;

        // Basic checks: should have at least 2 parameters (input, next)
        // Async version: input, next, optionally cancellationToken
        if (method.Parameters.Length < 2)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidStepSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        // Check if second parameter is a Func delegate
        var nextParam = method.Parameters[1];
        if (nextParam.Type is not INamedTypeSymbol nextType)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidStepSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        // Validate it's actually System.Func by checking namespace and name
        var fullName = nextType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!fullName.StartsWith("global::System.Func<"))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidStepSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        // For async methods, check for CancellationToken
        if (step.IsAsync && method.Parameters.Length >= 3)
        {
            var lastParam = method.Parameters[method.Parameters.Length - 1];
            if (lastParam.Type.ToDisplayString() != "System.Threading.CancellationToken")
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MissingCancellationTokenDescriptor,
                    method.Locations.FirstOrDefault(),
                    method.Name));
            }
        }

        return true;
    }

    private bool ValidateTerminalSignature(TerminalInfo terminal, SourceProductionContext context)
    {
        var method = terminal.Method;

        // Terminal should have exactly 1 parameter (input) or 2 for async (input, cancellationToken)
        if (method.Parameters.Length < 1 || method.Parameters.Length > 2)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidTerminalSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        // For async terminals, check for CancellationToken
        if (terminal.IsAsync && method.Parameters.Length == 2)
        {
            var lastParam = method.Parameters[1];
            if (lastParam.Type.ToDisplayString() != "System.Threading.CancellationToken")
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MissingCancellationTokenDescriptor,
                    method.Locations.FirstOrDefault(),
                    method.Name));
            }
        }

        return true;
    }

    private ITypeSymbol? DetermineInputType(List<StepInfo> steps, TerminalInfo terminal)
    {
        // Get input type from terminal (first parameter)
        return terminal.Method.Parameters.FirstOrDefault()?.Type;
    }

    private ITypeSymbol? DetermineOutputType(List<StepInfo> steps, TerminalInfo terminal)
    {
        // Get output type from terminal return type
        var returnType = terminal.Method.ReturnType;

        // If it's ValueTask<T> or Task<T>, unwrap it
        if (returnType is INamedTypeSymbol namedType && 
            (namedType.Name == "ValueTask" || namedType.Name == "Task") &&
            namedType.TypeArguments.Length > 0)
        {
            return namedType.TypeArguments[0];
        }

        return returnType;
    }

    private string GenerateSource(
        INamedTypeSymbol typeSymbol,
        ComposerConfig config,
        List<StepInfo> orderedSteps,
        TerminalInfo terminal,
        ITypeSymbol inputType,
        ITypeSymbol outputType,
        bool generateAsync)
    {
        var sb = new StringBuilder();
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeKind = typeSymbol.TypeKind == TypeKind.Struct ? "struct" : "class";
        var isRecord = typeSymbol.IsRecord;
        var typeDecl = isRecord ? $"partial record {typeKind}" : $"partial {typeKind}";

        // File header
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();

        if (ns != null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        // Type declaration
        sb.AppendLine($"{typeDecl} {typeSymbol.Name}");
        sb.AppendLine("{");

        bool isStruct = typeSymbol.TypeKind == TypeKind.Struct;

        // Generate sync Invoke if we have sync steps (always generate unless ForceAsync with async steps)
        bool hasSyncSteps = !orderedSteps.Any(s => s.IsAsync) && !terminal.IsAsync;
        bool generatedSyncVersion = false;
        if (hasSyncSteps)
        {
            GenerateSyncInvoke(sb, config, orderedSteps, terminal, inputType, outputType, isStruct);
            generatedSyncVersion = true;
        }

        // Generate async InvokeAsync if needed
        if (generateAsync)
        {
            // Add a blank line only if we also generated the sync version
            if (generatedSyncVersion)
                sb.AppendLine();
            GenerateAsyncInvoke(sb, config, orderedSteps, terminal, inputType, outputType, isStruct);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private void GenerateSyncInvoke(
        StringBuilder sb,
        ComposerConfig config,
        List<StepInfo> orderedSteps,
        TerminalInfo terminal,
        ITypeSymbol inputType,
        ITypeSymbol outputType,
        bool isStruct)
    {
        var inputTypeStr = inputType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var outputTypeStr = outputType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        sb.AppendLine($"    public {outputTypeStr} {config.InvokeMethodName}(in {inputTypeStr} input)");
        sb.AppendLine("    {");

        if (isStruct)
        {
            // For structs, we need to avoid lambdas that capture 'this'
            // We'll create a copy of 'this' and use it in local functions
            sb.AppendLine($"        var self = this;");
            
            // Start with terminal wrapped as a local function using the copy
            sb.AppendLine($"        {outputTypeStr} terminalFunc({inputTypeStr} arg) => self.{terminal.Method.Name}(in arg);");
            sb.AppendLine($"        global::System.Func<{inputTypeStr}, {outputTypeStr}> pipeline = terminalFunc;");

            // Wrap each step around the previous pipeline
            for (int i = orderedSteps.Count - 1; i >= 0; i--)
            {
                var step = orderedSteps[i];
                var funcName = $"step{i}Func";
                sb.AppendLine($"        {outputTypeStr} {funcName}({inputTypeStr} arg) => self.{step.Method.Name}(in arg, pipeline);");
                sb.AppendLine($"        pipeline = {funcName};");
            }

            // Invoke the final pipeline
            sb.AppendLine($"        return pipeline(input);");
        }
        else
        {
            // For classes, use the lambda approach
            // Build the pipeline from innermost (terminal) to outermost
            // We'll build a chain of Func delegates
            
            // Start with terminal wrapped as a Func
            sb.AppendLine($"        global::System.Func<{inputTypeStr}, {outputTypeStr}> pipeline = (arg) => {terminal.Method.Name}(in arg);");

            // Wrap each step around the previous pipeline
            for (int i = orderedSteps.Count - 1; i >= 0; i--)
            {
                var step = orderedSteps[i];
                sb.AppendLine($"        pipeline = (arg) => {step.Method.Name}(in arg, pipeline);");
            }

            // Invoke the final pipeline
            sb.AppendLine($"        return pipeline(input);");
        }
        
        sb.AppendLine("    }");
    }

    private void GenerateAsyncInvoke(
        StringBuilder sb,
        ComposerConfig config,
        List<StepInfo> orderedSteps,
        TerminalInfo terminal,
        ITypeSymbol inputType,
        ITypeSymbol outputType,
        bool isStruct)
    {
        var inputTypeStr = inputType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var outputTypeStr = outputType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        sb.AppendLine($"    public global::System.Threading.Tasks.ValueTask<{outputTypeStr}> {config.InvokeAsyncMethodName}({inputTypeStr} input, global::System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");

        if (isStruct)
        {
            // For structs, we need to avoid lambdas that capture 'this'
            // We'll create a copy of 'this' and use it in local functions, similar to sync approach
            sb.AppendLine($"        var self = this;");
            
            // Start with terminal wrapped as a local function using the copy
            if (terminal.IsAsync)
            {
                if (terminal.Method.Parameters.Length > 1 && 
                    terminal.Method.Parameters[1].Type.ToDisplayString() == "System.Threading.CancellationToken")
                {
                    sb.AppendLine($"        global::System.Threading.Tasks.ValueTask<{outputTypeStr}> terminalFunc({inputTypeStr} arg) => self.{terminal.Method.Name}(arg, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"        global::System.Threading.Tasks.ValueTask<{outputTypeStr}> terminalFunc({inputTypeStr} arg) => self.{terminal.Method.Name}(arg);");
                }
            }
            else
            {
                sb.AppendLine($"        global::System.Threading.Tasks.ValueTask<{outputTypeStr}> terminalFunc({inputTypeStr} arg) => new global::System.Threading.Tasks.ValueTask<{outputTypeStr}>(self.{terminal.Method.Name}(in arg));");
            }
            
            sb.AppendLine($"        global::System.Func<{inputTypeStr}, global::System.Threading.Tasks.ValueTask<{outputTypeStr}>> pipeline = terminalFunc;");

            // Wrap each step around the previous pipeline
            for (int i = orderedSteps.Count - 1; i >= 0; i--)
            {
                var step = orderedSteps[i];
                var funcName = $"step{i}Func";

                if (step.IsAsync)
                {
                    // Check if step has cancellationToken parameter
                    if (step.Method.Parameters.Length > 2 && 
                        step.Method.Parameters[2].Type.ToDisplayString() == "System.Threading.CancellationToken")
                    {
                        sb.AppendLine($"        global::System.Threading.Tasks.ValueTask<{outputTypeStr}> {funcName}({inputTypeStr} arg) => self.{step.Method.Name}(arg, pipeline, cancellationToken);");
                    }
                    else
                    {
                        sb.AppendLine($"        global::System.Threading.Tasks.ValueTask<{outputTypeStr}> {funcName}({inputTypeStr} arg) => self.{step.Method.Name}(arg, pipeline);");
                    }
                }
                else
                {
                    // Wrap sync step to work with async pipeline
                    // WARNING: GetAwaiter().GetResult() can deadlock in certain synchronization contexts (UI thread, ASP.NET pre-Core)
                    // Avoid using mixed sync/async pipelines in contexts with custom SynchronizationContext
                    sb.AppendLine($"        global::System.Threading.Tasks.ValueTask<{outputTypeStr}> {funcName}({inputTypeStr} arg) => new global::System.Threading.Tasks.ValueTask<{outputTypeStr}>(self.{step.Method.Name}(in arg, inp => pipeline(inp).GetAwaiter().GetResult()));");
                }
                
                sb.AppendLine($"        pipeline = {funcName};");
            }

            // Invoke the final pipeline
            sb.AppendLine($"        return pipeline(input);");
        }
        else
        {
            // Build the async pipeline using Func delegates
            // If terminal is async, use it directly; otherwise wrap in ValueTask.FromResult
            if (terminal.IsAsync)
            {
                if (terminal.Method.Parameters.Length > 1 && 
                    terminal.Method.Parameters[1].Type.ToDisplayString() == "System.Threading.CancellationToken")
                {
                    sb.AppendLine($"        global::System.Func<{inputTypeStr}, global::System.Threading.Tasks.ValueTask<{outputTypeStr}>> pipeline = (arg) => {terminal.Method.Name}(arg, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"        global::System.Func<{inputTypeStr}, global::System.Threading.Tasks.ValueTask<{outputTypeStr}>> pipeline = (arg) => {terminal.Method.Name}(arg);");
                }
            }
            else
            {
                sb.AppendLine($"        global::System.Func<{inputTypeStr}, global::System.Threading.Tasks.ValueTask<{outputTypeStr}>> pipeline = (arg) => new global::System.Threading.Tasks.ValueTask<{outputTypeStr}>({terminal.Method.Name}(in arg));");
            }

            // Wrap each step around the previous pipeline
            for (int i = orderedSteps.Count - 1; i >= 0; i--)
            {
                var step = orderedSteps[i];

                if (step.IsAsync)
                {
                    // Check if step has cancellationToken parameter
                    if (step.Method.Parameters.Length > 2 && 
                        step.Method.Parameters[2].Type.ToDisplayString() == "System.Threading.CancellationToken")
                    {
                        sb.AppendLine($"        pipeline = (arg) => {step.Method.Name}(arg, pipeline, cancellationToken);");
                    }
                    else
                    {
                        sb.AppendLine($"        pipeline = (arg) => {step.Method.Name}(arg, pipeline);");
                    }
                }
                else
                {
                    // Wrap sync step to work with async pipeline
                    // WARNING: GetAwaiter().GetResult() can deadlock in certain synchronization contexts (UI thread, ASP.NET pre-Core)
                    // Avoid using mixed sync/async pipelines in contexts with custom SynchronizationContext
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            var prevPipeline = pipeline;");
                    sb.AppendLine($"            pipeline = (arg) => new global::System.Threading.Tasks.ValueTask<{outputTypeStr}>({step.Method.Name}(in arg, inp => prevPipeline(inp).GetAwaiter().GetResult()));");
                    sb.AppendLine($"        }}");
                }
            }

            // Invoke the final pipeline
            sb.AppendLine($"        return pipeline(input);");
        }
        
        sb.AppendLine("    }");
    }

    private bool IsAsyncMethod(IMethodSymbol method)
    {
        var returnType = method.ReturnType;
        if (returnType is not INamedTypeSymbol namedType)
            return false;

        var fullName = namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return fullName == "global::System.Threading.Tasks.Task" ||
               fullName.StartsWith("global::System.Threading.Tasks.Task<") ||
               fullName == "global::System.Threading.Tasks.ValueTask" ||
               fullName.StartsWith("global::System.Threading.Tasks.ValueTask<");
    }

    private bool IsPartial(SyntaxNode node)
    {
        return node switch
        {
            ClassDeclarationSyntax cls => cls.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
            StructDeclarationSyntax str => str.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
            RecordDeclarationSyntax rec => rec.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
            _ => false
        };
    }

    private class ComposerConfig
    {
        public string InvokeMethodName { get; set; } = "Invoke";
        public string InvokeAsyncMethodName { get; set; } = "InvokeAsync";
        public bool? GenerateAsync { get; set; }
        public bool ForceAsync { get; set; }
        public ComposerWrapOrder WrapOrder { get; set; } = ComposerWrapOrder.OuterFirst;
    }

    private class StepInfo
    {
        public IMethodSymbol Method { get; set; } = null!;
        public int Order { get; set; }
        public string Name { get; set; } = null!;
        public bool IsAsync { get; set; }
    }

    private class TerminalInfo
    {
        public IMethodSymbol Method { get; set; } = null!;
        public bool IsAsync { get; set; }
    }
}
