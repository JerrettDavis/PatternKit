using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Chain of Responsibility / Pipeline pattern.
/// Generates Handle/TryHandle/HandleAsync methods that orchestrate handlers in deterministic order.
/// </summary>
[Generator]
public sealed class ChainGenerator : IIncrementalGenerator
{
    // Diagnostic IDs
    private const string DiagIdTypeNotPartial = "PKCH001";
    private const string DiagIdNoHandlers = "PKCH002";
    private const string DiagIdDuplicateOrder = "PKCH003";
    private const string DiagIdInvalidSignature = "PKCH004";
    private const string DiagIdMissingTerminal = "PKCH005";
    private const string DiagIdMultipleTerminals = "PKCH006";
    private const string DiagIdMissingDefault = "PKCH007";
    private const string DiagIdAsyncNotEnabled = "PKCH008";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type marked with [Chain] must be partial",
        messageFormat: "Type '{0}' is marked with [Chain] but is not declared as partial. Add the 'partial' keyword to the type declaration.",
        category: "PatternKit.Generators.Chain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoHandlersDescriptor = new(
        id: DiagIdNoHandlers,
        title: "No chain handlers found",
        messageFormat: "Type '{0}' has [Chain] but no methods marked with [ChainHandler]. At least one handler is required.",
        category: "PatternKit.Generators.Chain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateOrderDescriptor = new(
        id: DiagIdDuplicateOrder,
        title: "Duplicate handler order detected",
        messageFormat: "Multiple handlers have Order={0} in type '{1}'. Handler orders must be unique. Conflicting handlers: {2}.",
        category: "PatternKit.Generators.Chain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSignatureDescriptor = new(
        id: DiagIdInvalidSignature,
        title: "Invalid handler method signature",
        messageFormat: "Handler method '{0}' has an invalid signature. {1}",
        category: "PatternKit.Generators.Chain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingTerminalDescriptor = new(
        id: DiagIdMissingTerminal,
        title: "Missing terminal handler for pipeline model",
        messageFormat: "Type '{0}' uses ChainModel.Pipeline but has no method marked with [ChainTerminal]. A terminal handler is required.",
        category: "PatternKit.Generators.Chain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MultipleTerminalsDescriptor = new(
        id: DiagIdMultipleTerminals,
        title: "Multiple terminal handlers",
        messageFormat: "Type '{0}' has multiple methods marked with [ChainTerminal]. Only one terminal is allowed.",
        category: "PatternKit.Generators.Chain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingDefaultDescriptor = new(
        id: DiagIdMissingDefault,
        title: "Missing default handler for responsibility model",
        messageFormat: "Type '{0}' uses ChainModel.Responsibility but has no method marked with [ChainDefault]. A default handler is recommended.",
        category: "PatternKit.Generators.Chain",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AsyncNotEnabledDescriptor = new(
        id: DiagIdAsyncNotEnabled,
        title: "Async handler detected but async generation disabled",
        messageFormat: "Handler method '{0}' is async but async generation is not enabled. Set GenerateAsync=true or ForceAsync=true on [Chain].",
        category: "PatternKit.Generators.Chain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var chainTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Chain.ChainAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        context.RegisterSourceOutput(chainTypes, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Chain.ChainAttribute");
            if (attr is null)
                return;

            GenerateChainForType(spc, typeSymbol, attr, typeContext.TargetNode);
        });
    }

    private void GenerateChainForType(
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

        var config = ParseChainConfig(attribute);
        var handlers = CollectHandlers(typeSymbol);
        var defaults = CollectDefaults(typeSymbol);
        var terminals = CollectTerminals(typeSymbol);

        if (handlers.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NoHandlersDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Validate ordering
        if (!ValidateHandlerOrdering(handlers, typeSymbol, context))
            return;

        // Model-specific validation
        if (config.Model == 0) // Responsibility
        {
            if (defaults.Length == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MissingDefaultDescriptor,
                    node.GetLocation(),
                    typeSymbol.Name));
                // Warning only, continue generation
            }

            // Validate handler signatures for responsibility model
            foreach (var handler in handlers)
            {
                if (!ValidateResponsibilityHandlerSignature(handler.Method, context))
                    return;
            }

            // Validate default signatures
            foreach (var def in defaults)
            {
                if (!ValidateDefaultSignature(def, context))
                    return;
            }
        }
        else // Pipeline
        {
            if (terminals.Length == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MissingTerminalDescriptor,
                    node.GetLocation(),
                    typeSymbol.Name));
                return;
            }

            if (terminals.Length > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MultipleTerminalsDescriptor,
                    node.GetLocation(),
                    typeSymbol.Name));
                return;
            }

            // Validate handler signatures for pipeline model
            foreach (var handler in handlers)
            {
                if (!ValidatePipelineHandlerSignature(handler.Method, context))
                    return;
            }

            // Validate terminal signature
            if (!ValidateTerminalSignature(terminals[0], context))
                return;
        }

        // Determine async
        bool hasAsync = DetermineIfAsync(handlers, defaults, terminals);
        bool needsAsync = config.ForceAsync || config.GenerateAsync || hasAsync;

        if (hasAsync && !config.GenerateAsync && !config.ForceAsync)
        {
            var asyncMethod = handlers.FirstOrDefault(h => GeneratorUtilities.IsAsyncMethod(h.Method));
            if (asyncMethod is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    AsyncNotEnabledDescriptor,
                    asyncMethod.Method.Locations.FirstOrDefault(),
                    asyncMethod.Method.Name));
                return;
            }
        }

        var source = config.Model == 0
            ? GenerateResponsibilityChain(typeSymbol, config, handlers, defaults, needsAsync)
            : GeneratePipelineChain(typeSymbol, config, handlers, terminals[0], needsAsync);

        var fileName = $"{typeSymbol.Name}.Chain.g.cs";
        context.AddSource(fileName, source);
    }

    private ChainConfig ParseChainConfig(AttributeData attribute)
    {
        var config = new ChainConfig();

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "Model":
                    config.Model = namedArg.Value.Value is int m ? m : 0;
                    break;
                case "HandleMethodName":
                    config.HandleMethodName = namedArg.Value.Value?.ToString() ?? "Handle";
                    break;
                case "TryHandleMethodName":
                    config.TryHandleMethodName = namedArg.Value.Value?.ToString() ?? "TryHandle";
                    break;
                case "HandleAsyncMethodName":
                    config.HandleAsyncMethodName = namedArg.Value.Value?.ToString() ?? "HandleAsync";
                    break;
                case "GenerateAsync":
                    config.GenerateAsync = namedArg.Value.Value is bool ga && ga;
                    break;
                case "ForceAsync":
                    config.ForceAsync = namedArg.Value.Value is bool fa && fa;
                    break;
            }
        }

        return config;
    }

    private ImmutableArray<HandlerModel> CollectHandlers(INamedTypeSymbol typeSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<HandlerModel>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var handlerAttr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Chain.ChainHandlerAttribute");

            if (handlerAttr is null)
                continue;

            var order = handlerAttr.ConstructorArguments.Length > 0 &&
                       handlerAttr.ConstructorArguments[0].Value is int o ? o : 0;

            string? name = null;
            foreach (var namedArg in handlerAttr.NamedArguments)
            {
                if (namedArg.Key == "Name")
                    name = namedArg.Value.Value?.ToString();
            }

            builder.Add(new HandlerModel
            {
                Method = method,
                Order = order,
                Name = name ?? method.Name
            });
        }

        return builder.ToImmutable();
    }

    private ImmutableArray<IMethodSymbol> CollectDefaults(INamedTypeSymbol typeSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<IMethodSymbol>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Chain.ChainDefaultAttribute");

            if (attr is not null)
                builder.Add(method);
        }

        return builder.ToImmutable();
    }

    private ImmutableArray<IMethodSymbol> CollectTerminals(INamedTypeSymbol typeSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<IMethodSymbol>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Chain.ChainTerminalAttribute");

            if (attr is not null)
                builder.Add(method);
        }

        return builder.ToImmutable();
    }

    private bool ValidateHandlerOrdering(
        ImmutableArray<HandlerModel> handlers,
        INamedTypeSymbol typeSymbol,
        SourceProductionContext context)
    {
        var orderGroups = handlers.GroupBy(h => h.Order).Where(g => g.Count() > 1);
        foreach (var group in orderGroups)
        {
            var names = string.Join(", ", group.Select(h => h.Name));
            context.ReportDiagnostic(Diagnostic.Create(
                DuplicateOrderDescriptor,
                Location.None,
                group.Key,
                typeSymbol.Name,
                names));
            return false;
        }
        return true;
    }

    private bool ValidateResponsibilityHandlerSignature(IMethodSymbol method, SourceProductionContext context)
    {
        // Responsibility handler: bool TryHandle(in TIn input, out TOut output)
        // Must return bool and have at least 2 params (in TIn, out TOut)
        if (method.ReturnType.SpecialType != SpecialType.System_Boolean)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name,
                "Responsibility handler must return bool."));
            return false;
        }

        if (method.Parameters.Length < 2)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name,
                "Responsibility handler must have at least two parameters: (in TIn input, out TOut output)."));
            return false;
        }

        return true;
    }

    private bool ValidateDefaultSignature(IMethodSymbol method, SourceProductionContext context)
    {
        // Default: TOut DefaultHandler(in TIn input)
        if (method.Parameters.Length < 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name,
                "Default handler must accept at least one parameter (in TIn input)."));
            return false;
        }

        return true;
    }

    private bool ValidatePipelineHandlerSignature(IMethodSymbol method, SourceProductionContext context)
    {
        // Pipeline handler: TOut Handle(in TIn input, Func<TIn, TOut> next)
        if (method.Parameters.Length < 2)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name,
                "Pipeline handler must have at least two parameters: (in TIn input, Func<TIn, TOut> next)."));
            return false;
        }

        // Check second param is Func
        var nextParam = method.Parameters[1];
        if (nextParam.Type is not INamedTypeSymbol nextType)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name,
                "Second parameter must be a Func<TIn, TOut> delegate."));
            return false;
        }

        var fullName = nextType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!fullName.StartsWith("global::System.Func<"))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name,
                "Second parameter must be a Func<TIn, TOut> delegate."));
            return false;
        }

        return true;
    }

    private bool ValidateTerminalSignature(IMethodSymbol method, SourceProductionContext context)
    {
        // Terminal: TOut Terminal(in TIn input)
        if (method.Parameters.Length < 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name,
                "Terminal handler must accept at least one parameter (in TIn input)."));
            return false;
        }

        return true;
    }

    private bool DetermineIfAsync(
        ImmutableArray<HandlerModel> handlers,
        ImmutableArray<IMethodSymbol> defaults,
        ImmutableArray<IMethodSymbol> terminals)
    {
        foreach (var h in handlers)
        {
            if (GeneratorUtilities.IsAsyncMethod(h.Method))
                return true;
        }

        foreach (var d in defaults)
        {
            if (GeneratorUtilities.IsAsyncMethod(d))
                return true;
        }

        foreach (var t in terminals)
        {
            if (GeneratorUtilities.IsAsyncMethod(t))
                return true;
        }

        return false;
    }

    private string GenerateResponsibilityChain(
        INamedTypeSymbol typeSymbol,
        ChainConfig config,
        ImmutableArray<HandlerModel> handlers,
        ImmutableArray<IMethodSymbol> defaults,
        bool needsAsync)
    {
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeName = typeSymbol.Name;
        var typeKind = typeSymbol.TypeKind == TypeKind.Struct ? "struct" : "class";
        var recordKeyword = typeSymbol.IsRecord ? "record " : "";

        var sortedHandlers = handlers.OrderBy(h => h.Order).ThenBy(h => h.Name).ToList();

        // Determine input/output types from handler signature
        var firstHandler = sortedHandlers[0].Method;
        var inputType = firstHandler.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var outputType = firstHandler.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        // Remove 'out ' prefix if present in display
        var returnType = firstHandler.Parameters.Length > 1
            ? firstHandler.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : "void";

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

        // Generate TryHandle method
        sb.AppendLine($"    public bool {config.TryHandleMethodName}(in {inputType} input, out {returnType} output)");
        sb.AppendLine("    {");

        foreach (var handler in sortedHandlers)
        {
            sb.AppendLine($"        if ({handler.Method.Name}(in input, out output))");
            sb.AppendLine("            return true;");
            sb.AppendLine();
        }

        // Default handler
        if (defaults.Length > 0)
        {
            var def = defaults[0];
            sb.AppendLine($"        output = {def.Name}(in input);");
            sb.AppendLine("        return true;");
        }
        else
        {
            sb.AppendLine($"        output = default!;");
            sb.AppendLine("        return false;");
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate Handle method (throws if no handler matches and no default)
        sb.AppendLine($"    public {returnType} {config.HandleMethodName}(in {inputType} input)");
        sb.AppendLine("    {");
        sb.AppendLine($"        if ({config.TryHandleMethodName}(in input, out var result))");
        sb.AppendLine("            return result;");
        sb.AppendLine();
        sb.AppendLine($"        throw new global::System.InvalidOperationException(\"No handler in chain '{typeName}' could handle the request.\");");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GeneratePipelineChain(
        INamedTypeSymbol typeSymbol,
        ChainConfig config,
        ImmutableArray<HandlerModel> handlers,
        IMethodSymbol terminal,
        bool needsAsync)
    {
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeName = typeSymbol.Name;
        var typeKind = typeSymbol.TypeKind == TypeKind.Struct ? "struct" : "class";
        var recordKeyword = typeSymbol.IsRecord ? "record " : "";
        var isStruct = typeSymbol.TypeKind == TypeKind.Struct;

        var sortedHandlers = handlers.OrderBy(h => h.Order).ThenBy(h => h.Name).ToList();

        // Determine input/output types from terminal
        var inputType = terminal.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var outputType = terminal.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

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

        // Generate Handle method
        sb.AppendLine($"    public {outputType} {config.HandleMethodName}(in {inputType} input)");
        sb.AppendLine("    {");

        if (isStruct)
        {
            sb.AppendLine("        var self = this;");
            sb.AppendLine($"        {outputType} terminalFunc({inputType} arg) => self.{terminal.Name}(in arg);");
            sb.AppendLine($"        global::System.Func<{inputType}, {outputType}> pipeline = terminalFunc;");

            for (int i = sortedHandlers.Count - 1; i >= 0; i--)
            {
                var handler = sortedHandlers[i];
                var funcName = $"step{i}Func";
                sb.AppendLine($"        {outputType} {funcName}({inputType} arg) => self.{handler.Method.Name}(in arg, pipeline);");
                sb.AppendLine($"        pipeline = {funcName};");
            }

            sb.AppendLine("        return pipeline(input);");
        }
        else
        {
            sb.AppendLine($"        global::System.Func<{inputType}, {outputType}> pipeline = (arg) => {terminal.Name}(in arg);");

            for (int i = sortedHandlers.Count - 1; i >= 0; i--)
            {
                var handler = sortedHandlers[i];
                var prevName = $"__prev{i}";
                sb.AppendLine($"        var {prevName} = pipeline;");
                sb.AppendLine($"        pipeline = (arg) => {handler.Method.Name}(in arg, {prevName});");
            }

            sb.AppendLine("        return pipeline(input);");
        }

        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    private class ChainConfig
    {
        public int Model { get; set; } // 0 = Responsibility, 1 = Pipeline
        public string HandleMethodName { get; set; } = "Handle";
        public string TryHandleMethodName { get; set; } = "TryHandle";
        public string HandleAsyncMethodName { get; set; } = "HandleAsync";
        public bool GenerateAsync { get; set; }
        public bool ForceAsync { get; set; }
    }

    private class HandlerModel
    {
        public IMethodSymbol Method { get; set; } = null!;
        public int Order { get; set; }
        public string Name { get; set; } = null!;
    }
}
