using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Command pattern.
/// Generates Execute/ExecuteAsync static methods that dispatch to annotated handler methods.
/// </summary>
[Generator]
public sealed class CommandGenerator : IIncrementalGenerator
{
    // Diagnostic IDs
    private const string DiagIdTypeNotPartial = "PKCMD001";
    private const string DiagIdNoHandler = "PKCMD002";
    private const string DiagIdMultipleHandlers = "PKCMD003";
    private const string DiagIdInvalidHandlerSignature = "PKCMD004";
    private const string DiagIdAsyncDisabled = "PKCMD005";
    private const string DiagIdHostNotStaticPartial = "PKCMD006";
    private const string DiagIdCaseSignatureInvalid = "PKCMD007";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type marked with [Command] must be partial",
        messageFormat: "Type '{0}' is marked with [Command] but is not declared as partial. Add the 'partial' keyword to the type declaration.",
        category: "PatternKit.Generators.Command",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoHandlerDescriptor = new(
        id: DiagIdNoHandler,
        title: "No handler method found",
        messageFormat: "Type '{0}' has [Command] but no method marked with [CommandHandler]. A handler method is required.",
        category: "PatternKit.Generators.Command",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MultipleHandlersDescriptor = new(
        id: DiagIdMultipleHandlers,
        title: "Multiple handler methods found",
        messageFormat: "Type '{0}' has multiple methods marked with [CommandHandler]. Only one handler method is allowed per command.",
        category: "PatternKit.Generators.Command",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidHandlerSignatureDescriptor = new(
        id: DiagIdInvalidHandlerSignature,
        title: "Invalid handler method signature",
        messageFormat: "Handler method '{0}' has an invalid signature. Handler must accept the command as its first parameter.",
        category: "PatternKit.Generators.Command",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AsyncDisabledDescriptor = new(
        id: DiagIdAsyncDisabled,
        title: "Async handler but GenerateAsync is disabled",
        messageFormat: "Handler method '{0}' returns a Task/ValueTask but GenerateAsync is not enabled. Set GenerateAsync = true or ForceAsync = true.",
        category: "PatternKit.Generators.Command",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor HostNotStaticPartialDescriptor = new(
        id: DiagIdHostNotStaticPartial,
        title: "CommandHost must be static partial",
        messageFormat: "Type '{0}' is marked with [CommandHost] but is not declared as static partial. Add both 'static' and 'partial' keywords.",
        category: "PatternKit.Generators.Command",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CaseSignatureInvalidDescriptor = new(
        id: DiagIdCaseSignatureInvalid,
        title: "Invalid command case method signature",
        messageFormat: "Command case method '{0}' has an invalid signature. Cases must be static and return void or ValueTask.",
        category: "PatternKit.Generators.Command",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Handle [Command] on types
        var commandTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Command.CommandAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        context.RegisterSourceOutput(commandTypes, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Command.CommandAttribute");
            if (attr is null)
                return;

            GenerateCommandForType(spc, typeSymbol, attr, typeContext.TargetNode);
        });

        // Handle [CommandHost] on static classes
        var hostTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Command.CommandHostAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        context.RegisterSourceOutput(hostTypes, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            GenerateCommandHost(spc, typeSymbol, typeContext.TargetNode);
        });
    }

    private void GenerateCommandForType(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attribute,
        SyntaxNode node)
    {
        // Check if type is partial
        if (!GeneratorUtilities.IsPartialType(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNotPartialDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Parse attribute config
        var config = ParseCommandConfig(attribute);

        // Find handler methods
        var handlers = CollectHandlerMethods(typeSymbol);

        if (handlers.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NoHandlerDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        if (handlers.Count > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MultipleHandlersDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        var handler = handlers[0];

        // Validate handler signature
        if (!ValidateHandlerSignature(handler, typeSymbol, context))
            return;

        // Determine if async
        var isAsync = config.ForceAsync || config.GenerateAsync || GeneratorUtilities.IsAsyncMethod(handler);

        // Generate code
        var source = GenerateCommandMethods(typeSymbol, config, handler, isAsync);
        var fileName = $"{typeSymbol.Name}.Command.g.cs";
        context.AddSource(fileName, source);
    }

    private void GenerateCommandHost(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        SyntaxNode node)
    {
        // Must be static partial
        if (!GeneratorUtilities.IsPartialType(node) || !typeSymbol.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                HostNotStaticPartialDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Find case methods
        var cases = CollectCaseMethods(typeSymbol);
        if (cases.Count == 0)
            return;

        // Validate case methods
        foreach (var caseMethod in cases)
        {
            if (!caseMethod.IsStatic ||
                (!caseMethod.ReturnsVoid && !IsNonGenericValueTask(caseMethod.ReturnType)))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    CaseSignatureInvalidDescriptor,
                    caseMethod.Locations.FirstOrDefault(),
                    caseMethod.Name));
                return;
            }
        }

        // Generate host dispatch
        var source = GenerateHostMethods(typeSymbol, cases);
        var fileName = $"{typeSymbol.Name}.Command.g.cs";
        context.AddSource(fileName, source);
    }

    private CommandConfig ParseCommandConfig(AttributeData attribute)
    {
        var config = new CommandConfig();

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "CommandTypeName":
                    config.CommandTypeName = namedArg.Value.Value?.ToString();
                    break;
                case "GenerateAsync":
                    config.GenerateAsync = namedArg.Value.Value is bool ga && ga;
                    break;
                case "ForceAsync":
                    config.ForceAsync = namedArg.Value.Value is bool fa && fa;
                    break;
                case "GenerateUndo":
                    config.GenerateUndo = namedArg.Value.Value is bool gu && gu;
                    break;
            }
        }

        return config;
    }

    private List<IMethodSymbol> CollectHandlerMethods(INamedTypeSymbol typeSymbol)
    {
        var handlers = new List<IMethodSymbol>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var handlerAttr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Command.CommandHandlerAttribute");

            if (handlerAttr is not null)
                handlers.Add(method);
        }

        return handlers;
    }

    private List<IMethodSymbol> CollectCaseMethods(INamedTypeSymbol typeSymbol)
    {
        var cases = new List<IMethodSymbol>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var caseAttr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Command.CommandCaseAttribute");

            if (caseAttr is not null)
                cases.Add(method);
        }

        // Sort by name for stable emission
        cases.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.Ordinal));
        return cases;
    }

    private bool ValidateHandlerSignature(
        IMethodSymbol handler,
        INamedTypeSymbol commandType,
        SourceProductionContext context)
    {
        // Must have at least one parameter (the command itself or relevant data)
        if (handler.Parameters.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidHandlerSignatureDescriptor,
                handler.Locations.FirstOrDefault(),
                handler.Name));
            return false;
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

    private static bool IsTaskType(ITypeSymbol returnType)
    {
        return returnType is INamedTypeSymbol namedType &&
               namedType.Name == "Task" &&
               namedType.Arity == 0 &&
               namedType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";
    }

    private string GenerateCommandMethods(
        INamedTypeSymbol typeSymbol,
        CommandConfig config,
        IMethodSymbol handler,
        bool needsAsync)
    {
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeName = typeSymbol.Name;
        var typeKind = typeSymbol.TypeKind == TypeKind.Struct ? "struct" : "class";
        var recordKeyword = typeSymbol.IsRecord ? "record " : "";
        var commandTypeDisplay = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var sb = new StringBuilder();

        // Header
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

        // Sync Execute method
        if (!needsAsync || config.ForceAsync)
        {
            GenerateSyncExecuteMethod(sb, handler, commandTypeDisplay);
        }

        // Async ExecuteAsync method
        if (needsAsync)
        {
            if (!needsAsync || config.ForceAsync)
                sb.AppendLine();
            GenerateAsyncExecuteMethod(sb, handler, commandTypeDisplay);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private void GenerateSyncExecuteMethod(
        StringBuilder sb,
        IMethodSymbol handler,
        string commandTypeDisplay)
    {
        var handlerIsStatic = handler.IsStatic;
        var handlerName = handler.Name;

        // Build parameter list (skip the first param if it's the command type for static handlers)
        var extraParams = GetExtraParameters(handler);
        var paramList = $"{commandTypeDisplay} command";
        if (extraParams.Length > 0)
        {
            var extras = string.Join(", ", extraParams.Select(p =>
                $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"));
            paramList += ", " + extras;
        }

        sb.AppendLine($"    /// <summary>Executes the command by invoking the handler.</summary>");
        sb.AppendLine($"    public static void Execute({paramList})");
        sb.AppendLine("    {");

        var callArgs = BuildCallArgs(handler, handlerIsStatic);
        if (handlerIsStatic)
        {
            sb.AppendLine($"        {handlerName}({callArgs});");
        }
        else
        {
            sb.AppendLine($"        command.{handlerName}({callArgs});");
        }

        sb.AppendLine("    }");
    }

    private void GenerateAsyncExecuteMethod(
        StringBuilder sb,
        IMethodSymbol handler,
        string commandTypeDisplay)
    {
        var handlerIsStatic = handler.IsStatic;
        var handlerName = handler.Name;
        var isHandlerAsync = GeneratorUtilities.IsAsyncMethod(handler);

        // Build parameter list
        var extraParams = GetExtraParameters(handler);
        var paramList = $"{commandTypeDisplay} command";

        // Add CancellationToken if handler accepts one
        var hasCt = handler.Parameters.Any(GeneratorUtilities.IsCancellationToken);
        var extraNonCt = extraParams.Where(p => !GeneratorUtilities.IsCancellationToken(p)).ToArray();

        if (extraNonCt.Length > 0)
        {
            var extras = string.Join(", ", extraNonCt.Select(p =>
                $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"));
            paramList += ", " + extras;
        }

        paramList += ", System.Threading.CancellationToken ct = default";

        sb.AppendLine($"    /// <summary>Asynchronously executes the command by invoking the handler.</summary>");

        if (isHandlerAsync)
        {
            sb.AppendLine($"    public static async System.Threading.Tasks.ValueTask ExecuteAsync({paramList})");
            sb.AppendLine("    {");

            var callArgs = BuildAsyncCallArgs(handler, handlerIsStatic, hasCt);
            if (handlerIsStatic)
            {
                sb.AppendLine($"        await {handlerName}({callArgs}).ConfigureAwait(false);");
            }
            else
            {
                sb.AppendLine($"        await command.{handlerName}({callArgs}).ConfigureAwait(false);");
            }
        }
        else
        {
            sb.AppendLine($"    public static System.Threading.Tasks.ValueTask ExecuteAsync({paramList})");
            sb.AppendLine("    {");

            var callArgs = BuildCallArgs(handler, handlerIsStatic);
            if (handlerIsStatic)
            {
                sb.AppendLine($"        {handlerName}({callArgs});");
            }
            else
            {
                sb.AppendLine($"        command.{handlerName}({callArgs});");
            }
            sb.AppendLine("        return default;");
        }

        sb.AppendLine("    }");
    }

    private IParameterSymbol[] GetExtraParameters(IMethodSymbol handler)
    {
        if (handler.IsStatic && handler.Parameters.Length > 1)
        {
            // Skip the first param (command) and CancellationToken
            return handler.Parameters.Skip(1)
                .Where(p => !GeneratorUtilities.IsCancellationToken(p))
                .ToArray();
        }

        if (!handler.IsStatic && handler.Parameters.Length > 0)
        {
            // For instance handlers, skip the first param (command itself) and CancellationToken
            return handler.Parameters.Skip(1)
                .Where(p => !GeneratorUtilities.IsCancellationToken(p))
                .ToArray();
        }

        return System.Array.Empty<IParameterSymbol>();
    }

    private string BuildCallArgs(IMethodSymbol handler, bool isStatic)
    {
        if (isStatic)
        {
            // First parameter is always the command, map it to the generated parameter name
            return string.Join(", ", handler.Parameters.Select((p, i) => i == 0 ? "command" : p.Name));
        }
        else
        {
            // For instance handlers, first parameter is the command, map it to the generated parameter name
            return string.Join(", ", handler.Parameters.Select((p, i) => i == 0 ? "command" : p.Name));
        }
    }

    private string BuildAsyncCallArgs(IMethodSymbol handler, bool isStatic, bool hasCt)
    {
        var args = new List<string>();
        foreach (var p in handler.Parameters)
        {
            if (p.Ordinal == 0)
            {
                // First parameter is always the command itself
                args.Add("command");
            }
            else if (GeneratorUtilities.IsCancellationToken(p))
            {
                args.Add("ct");
            }
            else
            {
                args.Add(p.Name);
            }
        }
        return string.Join(", ", args);
    }

    private string GenerateHostMethods(
        INamedTypeSymbol typeSymbol,
        List<IMethodSymbol> cases)
    {
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeName = typeSymbol.Name;

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"static partial class {typeName}");
        sb.AppendLine("{");

        for (int i = 0; i < cases.Count; i++)
        {
            var caseMethod = cases[i];
            var isAsync = IsNonGenericValueTask(caseMethod.ReturnType);

            if (i > 0) sb.AppendLine();

            // Build parameter string
            var paramStr = string.Join(", ", caseMethod.Parameters.Select(p =>
                $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"));

            var argStr = string.Join(", ", caseMethod.Parameters.Select(p => p.Name));

            if (isAsync)
            {
                sb.AppendLine($"    /// <summary>Executes the {caseMethod.Name} command asynchronously.</summary>");
                sb.AppendLine($"    public static async System.Threading.Tasks.ValueTask Execute{caseMethod.Name}Async({paramStr})");
                sb.AppendLine("    {");
                sb.AppendLine($"        await {caseMethod.Name}({argStr}).ConfigureAwait(false);");
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine($"    /// <summary>Executes the {caseMethod.Name} command.</summary>");
                sb.AppendLine($"    public static void Execute{caseMethod.Name}({paramStr})");
                sb.AppendLine("    {");
                sb.AppendLine($"        {caseMethod.Name}({argStr});");
                sb.AppendLine("    }");
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    // Helper classes
    private class CommandConfig
    {
        public string? CommandTypeName { get; set; }
        public bool GenerateAsync { get; set; }
        public bool ForceAsync { get; set; }
        public bool GenerateUndo { get; set; }
    }
}
