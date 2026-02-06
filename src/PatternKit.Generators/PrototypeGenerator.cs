using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Prototype pattern.
/// Generates Clone methods with configurable cloning strategies for safe object duplication.
/// </summary>
[Generator]
public sealed class PrototypeGenerator : IIncrementalGenerator
{
    // Diagnostic IDs
    private const string DiagIdTypeNotPartial = "PKPRO001";
    private const string DiagIdNoConstructionPath = "PKPRO002";
    private const string DiagIdUnsafeReferenceCapture = "PKPRO003";
    private const string DiagIdCloneMechanismMissing = "PKPRO004";
    private const string DiagIdCustomStrategyMissing = "PKPRO005";
    private const string DiagIdAttributeMisuse = "PKPRO006";
    private const string DiagIdDeepCopyNotImplemented = "PKPRO007";
    private const string DiagIdGenericTypeNotSupported = "PKPRO008";
    private const string DiagIdNestedTypeNotSupported = "PKPRO009";
    private const string DiagIdAbstractTypeNotSupported = "PKPRO010";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type marked with [Prototype] must be partial",
        messageFormat: "Type '{0}' is marked with [Prototype] but is not declared as partial. Add the 'partial' keyword to the type declaration.",
        category: "PatternKit.Generators.Prototype",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoConstructionPathDescriptor = new(
        id: DiagIdNoConstructionPath,
        title: "Cannot construct clone target",
        messageFormat: "Cannot construct clone for type '{0}'. No supported clone construction path found (no parameterless constructor, copy constructor, or record with-expression support).",
        category: "PatternKit.Generators.Prototype",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsafeReferenceCaptureDescriptor = new(
        id: DiagIdUnsafeReferenceCapture,
        title: "Unsafe reference capture",
        messageFormat: "Member '{0}' is a mutable reference type copied by reference. Mutations will affect both the original and clone. Consider using [PrototypeStrategy(Clone)] or [PrototypeStrategy(ShallowCopy)].",
        category: "PatternKit.Generators.Prototype",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CloneMechanismMissingDescriptor = new(
        id: DiagIdCloneMechanismMissing,
        title: "Requested Clone strategy but no clone mechanism found",
        messageFormat: "Member '{0}' has [PrototypeStrategy(Clone)] but no suitable clone mechanism is available. Implement ICloneable, provide a Clone() method, copy constructor, or use a collection type with copy constructor support.",
        category: "PatternKit.Generators.Prototype",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CustomStrategyMissingDescriptor = new(
        id: DiagIdCustomStrategyMissing,
        title: "Custom strategy requires partial clone hook, but none found",
        messageFormat: "Member '{0}' has [PrototypeStrategy(Custom)] but no static partial method '{1} Clone{0}({1} value)' was found. Declare this method in your partial type.",
        category: "PatternKit.Generators.Prototype",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AttributeMisuseDescriptor = new(
        id: DiagIdAttributeMisuse,
        title: "Include/Ignore attribute misuse",
        messageFormat: "{0}",
        category: "PatternKit.Generators.Prototype",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DeepCopyNotImplementedDescriptor = new(
        id: DiagIdDeepCopyNotImplemented,
        title: "DeepCopy strategy not yet implemented",
        messageFormat: "DeepCopy strategy for member '{0}' is not yet implemented. Use Clone or Custom strategy instead.",
        category: "PatternKit.Generators.Prototype",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor GenericTypeNotSupportedDescriptor = new(
        id: DiagIdGenericTypeNotSupported,
        title: "Generic types not supported for Prototype pattern",
        messageFormat: "Type '{0}' is generic, which is not currently supported by the Prototype generator. Remove the [Prototype] attribute or use a non-generic type.",
        category: "PatternKit.Generators.Prototype",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NestedTypeNotSupportedDescriptor = new(
        id: DiagIdNestedTypeNotSupported,
        title: "Nested types not supported for Prototype pattern",
        messageFormat: "Type '{0}' is nested inside another type, which is not currently supported by the Prototype generator. Remove the [Prototype] attribute or move the type to the top level.",
        category: "PatternKit.Generators.Prototype",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AbstractTypeNotSupportedDescriptor = new(
        id: DiagIdAbstractTypeNotSupported,
        title: "Abstract types not supported for Prototype pattern",
        messageFormat: "Type '{0}' is abstract, which is not supported by the Prototype generator. Abstract types cannot be instantiated for cloning.",
        category: "PatternKit.Generators.Prototype",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all type declarations with [Prototype] attribute
        var prototypeTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Prototype.PrototypeAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        // Generate for each type
        context.RegisterSourceOutput(prototypeTypes, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Prototype.PrototypeAttribute");
            if (attr is null)
                return;

            GeneratePrototypeForType(spc, typeSymbol, attr, typeContext.TargetNode);
        });
    }

    private void GeneratePrototypeForType(
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

        // Check for generic types
        if (typeSymbol.IsGenericType)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                GenericTypeNotSupportedDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Check for nested types
        if (typeSymbol.ContainingType is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NestedTypeNotSupportedDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Check for abstract types (non-record abstract classes cannot be instantiated)
        if (typeSymbol.IsAbstract && !typeSymbol.IsRecord)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AbstractTypeNotSupportedDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Parse attribute arguments
        var config = ParsePrototypeConfig(attribute);

        // Analyze type and members
        var typeInfo = AnalyzeType(typeSymbol, config, context);
        if (typeInfo is null)
            return;

        // For records, default to "Duplicate" instead of "Clone" (which is reserved)
        if (!config.CloneMethodNameExplicit && (typeInfo.IsRecordClass || typeInfo.IsRecordStruct))
        {
            config.CloneMethodName = "Duplicate";
        }

        // Generate clone method
        var cloneSource = GenerateCloneMethod(typeInfo, config, context);
        if (!string.IsNullOrEmpty(cloneSource))
        {
            var fileName = $"{typeSymbol.Name}.Prototype.g.cs";
            context.AddSource(fileName, cloneSource);
        }
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

    private PrototypeConfig ParsePrototypeConfig(AttributeData attribute)
    {
        var config = new PrototypeConfig();

        foreach (var named in attribute.NamedArguments)
        {
            switch (named.Key)
            {
                case "Mode":
                    if (named.Value.Value is int modeValue)
                        config.Mode = (PrototypeMode)modeValue;
                    break;
                case "CloneMethodName":
                    if (named.Value.Value is string methodName)
                    {
                        config.CloneMethodName = methodName;
                        config.CloneMethodNameExplicit = true;
                    }
                    break;
                case "IncludeExplicit":
                    if (named.Value.Value is bool includeExplicit)
                        config.IncludeExplicit = includeExplicit;
                    break;
            }
        }

        return config;
    }

    private TypeInfo? AnalyzeType(
        INamedTypeSymbol typeSymbol,
        PrototypeConfig config,
        SourceProductionContext context)
    {
        var typeInfo = new TypeInfo
        {
            TypeSymbol = typeSymbol,
            TypeName = typeSymbol.Name,
            Namespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            IsClass = typeSymbol.TypeKind == TypeKind.Class && !typeSymbol.IsRecord,
            IsStruct = typeSymbol.TypeKind == TypeKind.Struct && !typeSymbol.IsRecord,
            IsRecordClass = typeSymbol.TypeKind == TypeKind.Class && typeSymbol.IsRecord,
            IsRecordStruct = typeSymbol.TypeKind == TypeKind.Struct && typeSymbol.IsRecord,
            Members = new List<MemberInfo>()
        };

        // Collect members based on inclusion mode
        var members = GetMembersForClone(typeSymbol, config, context);
        typeInfo.Members.AddRange(members);

        // Determine construction strategy
        typeInfo.ConstructionStrategy = DetermineConstructionStrategy(typeSymbol, typeInfo);
        if (typeInfo.ConstructionStrategy == ConstructionStrategy.None)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NoConstructionPathDescriptor,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name));
            return null;
        }

        return typeInfo;
    }

    private ConstructionStrategy DetermineConstructionStrategy(INamedTypeSymbol typeSymbol, TypeInfo typeInfo)
    {
        // For records, prefer with-expression if all members are init/readonly
        if (typeInfo.IsRecordClass || typeInfo.IsRecordStruct)
        {
            // Check if all members are init-only or readonly
            bool allInit = typeInfo.Members.All(member => member.IsInitOnly || member.IsReadOnly);

            if (allInit)
                return ConstructionStrategy.RecordWith;

            // Otherwise try copy constructor or parameterless
            if (HasCopyConstructor(typeSymbol))
                return ConstructionStrategy.CopyConstructor;

            if (HasParameterlessConstructor(typeSymbol))
                return ConstructionStrategy.ParameterlessConstructor;

            return ConstructionStrategy.RecordWith; // Fall back to with-expression
        }

        // For classes/structs, try copy constructor first, then parameterless
        if (HasCopyConstructor(typeSymbol))
            return ConstructionStrategy.CopyConstructor;

        if (HasParameterlessConstructor(typeSymbol))
            return ConstructionStrategy.ParameterlessConstructor;

        return ConstructionStrategy.None;
    }

    private bool HasCopyConstructor(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.Constructors.Any(c =>
            c.Parameters.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type, typeSymbol));
    }

    private bool HasParameterlessConstructor(INamedTypeSymbol typeSymbol)
    {
        // Structs always have an implicit parameterless constructor
        if (typeSymbol.TypeKind == TypeKind.Struct)
            return true;

        // Any parameterless constructor is usable from the generated clone method,
        // which is emitted into the same partial type, regardless of accessibility.
        return typeSymbol.Constructors.Any(c => c.Parameters.Length == 0);
    }

    private List<MemberInfo> GetMembersForClone(
        INamedTypeSymbol typeSymbol,
        PrototypeConfig config,
        SourceProductionContext context)
    {
        var members = new List<MemberInfo>();
        var includeAll = !config.IncludeExplicit;

        // Get all instance properties and fields
        var candidateMembers = typeSymbol.GetMembers()
            .Where(m => (m is IPropertySymbol || m is IFieldSymbol) &&
                        !m.IsStatic &&
                        m.DeclaredAccessibility == Accessibility.Public);

        foreach (var member in candidateMembers)
        {
            // Check for attributes
            var hasIgnore = HasAttribute(member, "PatternKit.Generators.Prototype.PrototypeIgnoreAttribute");
            var hasInclude = HasAttribute(member, "PatternKit.Generators.Prototype.PrototypeIncludeAttribute");
            var strategyAttr = GetAttribute(member, "PatternKit.Generators.Prototype.PrototypeStrategyAttribute");

            // Check for attribute misuse
            if (includeAll && hasInclude)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    AttributeMisuseDescriptor,
                    member.Locations.FirstOrDefault(),
                    $"[PrototypeInclude] on member '{member.Name}' has no effect when IncludeExplicit is false. Remove the attribute or set IncludeExplicit=true."));
            }
            else if (!includeAll && hasIgnore)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    AttributeMisuseDescriptor,
                    member.Locations.FirstOrDefault(),
                    $"[PrototypeIgnore] on member '{member.Name}' has no effect when IncludeExplicit is true. Remove the attribute or set IncludeExplicit=false."));
            }

            // Determine if this member should be included
            bool shouldInclude = includeAll ? !hasIgnore : hasInclude;
            if (!shouldInclude)
                continue;

            // Extract member type and characteristics
            ITypeSymbol? memberType = null;
            bool isReadOnly = false;
            bool isInitOnly = false;

            if (member is IPropertySymbol prop)
            {
                // Must have a getter
                if (prop.GetMethod is null || prop.GetMethod.DeclaredAccessibility != Accessibility.Public)
                    continue;

                memberType = prop.Type;
                isReadOnly = prop.SetMethod is null;
                isInitOnly = prop.SetMethod?.IsInitOnly ?? false;
            }
            else if (member is IFieldSymbol fld)
            {
                memberType = fld.Type;
                isReadOnly = fld.IsReadOnly;
            }

            if (memberType is null)
                continue;

            // Determine clone strategy
            var strategy = DetermineCloneStrategy(member, memberType, strategyAttr, config, context);
            if (strategy is null)
                continue; // Error already reported

            members.Add(new MemberInfo
            {
                Name = member.Name,
                Type = memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                TypeSymbol = memberType,
                IsProperty = member is IPropertySymbol,
                IsField = member is IFieldSymbol,
                IsReadOnly = isReadOnly,
                IsInitOnly = isInitOnly,
                CloneStrategy = strategy.Value,
                Symbol = member
            });
        }

        // Sort by member name for deterministic output
        members.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        return members;
    }

    private CloneStrategy? DetermineCloneStrategy(
        ISymbol member,
        ITypeSymbol memberType,
        AttributeData? strategyAttr,
        PrototypeConfig config,
        SourceProductionContext context)
    {
        // If explicit strategy provided, validate and use it
        if (strategyAttr is not null)
        {
            var ctorArg = strategyAttr.ConstructorArguments.FirstOrDefault();
            if (ctorArg.Value is int strategyValue)
            {
                var strategy = (CloneStrategy)strategyValue;

                // Validate strategy
                if (strategy == CloneStrategy.Clone)
                {
                    if (!HasCloneMechanism(memberType))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            CloneMechanismMissingDescriptor,
                            member.Locations.FirstOrDefault(),
                            member.Name));
                        return null;
                    }
                }
                else if (strategy == CloneStrategy.Custom)
                {
                    // Check for custom partial method with correct signature (parameter and return type must match member type)
                    var containingType = member.ContainingType;
                    var methodName = $"Clone{member.Name}";
                    var hasCustomMethod = containingType.GetMembers(methodName)
                        .OfType<IMethodSymbol>()
                        .Any(m => m.IsStatic &&
                                  m.IsPartialDefinition &&
                                  m.Parameters.Length == 1 &&
                                  SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, memberType) &&
                                  SymbolEqualityComparer.Default.Equals(m.ReturnType, memberType));

                    if (!hasCustomMethod)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            CustomStrategyMissingDescriptor,
                            member.Locations.FirstOrDefault(),
                            member.Name,
                            memberType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                        return null;
                    }
                }
                else if (strategy == CloneStrategy.DeepCopy)
                {
                    // DeepCopy is v2 - not yet supported
                    context.ReportDiagnostic(Diagnostic.Create(
                        DeepCopyNotImplementedDescriptor,
                        member.Locations.FirstOrDefault(),
                        member.Name));
                    return null;
                }

                return strategy;
            }
        }

        // Otherwise, infer based on mode
        return InferCloneStrategy(member, memberType, config, context);
    }

    private CloneStrategy InferCloneStrategy(
        ISymbol member,
        ITypeSymbol memberType,
        PrototypeConfig config,
        SourceProductionContext context)
    {
        // Value types and string: always copy (safe)
        if (memberType.IsValueType || memberType.SpecialType == SpecialType.System_String)
            return CloneStrategy.ByReference;

        // Reference types depend on mode
        switch (config.Mode)
        {
            case PrototypeMode.ShallowWithWarnings:
                // Warn about mutable reference types
                if (!IsImmutableReferenceType(memberType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsafeReferenceCaptureDescriptor,
                        member.Locations.FirstOrDefault(),
                        member.Name));
                }
                return CloneStrategy.ByReference;

            case PrototypeMode.Shallow:
                return CloneStrategy.ByReference;

            case PrototypeMode.DeepWhenPossible:
                if (HasCloneMechanism(memberType))
                    return CloneStrategy.Clone;
                return CloneStrategy.ByReference;

            default:
                return CloneStrategy.ByReference;
        }
    }

    private bool IsImmutableReferenceType(ITypeSymbol type)
    {
        // String is immutable
        if (type.SpecialType == SpecialType.System_String)
            return true;

        // Value types are always safe (but caller should check for this first)
        if (type.IsValueType)
            return true;

        // Check for known immutable collections (basic check)
        var typeName = type.ToDisplayString();
        if (typeName.StartsWith("System.Collections.Immutable."))
            return true;

        // Conservative: assume mutable
        return false;
    }

    private bool HasCloneMechanism(ITypeSymbol type)
    {
        // Check for ICloneable
        if (ImplementsICloneable(type))
            return true;

        // Check for Clone() method returning same type (instance methods only)
        var cloneMethod = type.GetMembers("Clone")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => !m.IsStatic &&
                                 m.Parameters.Length == 0 &&
                                 SymbolEqualityComparer.Default.Equals(m.ReturnType, type));
        if (cloneMethod is not null)
            return true;

        // Check for copy constructor
        if (type is INamedTypeSymbol namedType && HasCopyConstructor(namedType))
            return true;

        // Check for List<T> or similar collections with copy constructors
        if (IsCollectionWithCopyConstructor(type))
            return true;

        return false;
    }

    private bool ImplementsICloneable(ITypeSymbol type)
    {
        return type.AllInterfaces.Any(i =>
            i.ToDisplayString() == "System.ICloneable");
    }

    private static readonly HashSet<string> CollectionsWithCopyConstructor = new HashSet<string>
    {
        "System.Collections.Generic.List<T>",
        "System.Collections.Generic.HashSet<T>",
        "System.Collections.Generic.Queue<T>",
        "System.Collections.Generic.Stack<T>",
        "System.Collections.Generic.LinkedList<T>",
        "System.Collections.Generic.Dictionary<TKey, TValue>",
        "System.Collections.Generic.SortedSet<T>",
        "System.Collections.Generic.SortedDictionary<TKey, TValue>"
    };

    private bool IsCollectionWithCopyConstructor(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        var typeName = namedType.ConstructedFrom.ToDisplayString();
        return CollectionsWithCopyConstructor.Contains(typeName);
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == attributeName);
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.ToDisplayString() == attributeName);
    }

    private string GenerateCloneMethod(TypeInfo typeInfo, PrototypeConfig config, SourceProductionContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine();

        // Only add namespace declaration if not in global namespace
        if (!string.IsNullOrEmpty(typeInfo.Namespace))
        {
            sb.AppendLine($"namespace {typeInfo.Namespace};");
            sb.AppendLine();
        }

        // Determine type declaration keyword
        string typeKeyword;
        if (typeInfo.IsRecordClass)
            typeKeyword = "partial record class";
        else if (typeInfo.IsRecordStruct)
            typeKeyword = "partial record struct";
        else if (typeInfo.IsClass)
            typeKeyword = "partial class";
        else
            typeKeyword = "partial struct";

        sb.AppendLine($"{typeKeyword} {typeInfo.TypeName}");
        sb.AppendLine("{");

        // Generate Clone method
        GenerateCloneMethodBody(sb, typeInfo, config, context);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private void GenerateCloneMethodBody(StringBuilder sb, TypeInfo typeInfo, PrototypeConfig config, SourceProductionContext context)
    {
        sb.AppendLine($"    /// <summary>Creates a clone of this instance.</summary>");
        sb.AppendLine($"    public {typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {config.CloneMethodName}()");
        sb.AppendLine("    {");

        // Generate member cloning expressions
        var cloneExprs = new Dictionary<string, string>();
        foreach (var member in typeInfo.Members)
        {
            var expr = GenerateCloneExpression(member, typeInfo, context);
            cloneExprs[member.Name] = expr;
        }

        // Generate construction based on strategy
        switch (typeInfo.ConstructionStrategy)
        {
            case ConstructionStrategy.RecordWith:
                GenerateRecordWithConstruction(sb, typeInfo, cloneExprs);
                break;

            case ConstructionStrategy.CopyConstructor:
                GenerateCopyConstructorConstruction(sb, typeInfo, cloneExprs);
                break;

            case ConstructionStrategy.ParameterlessConstructor:
                GenerateParameterlessConstructorConstruction(sb, typeInfo, cloneExprs);
                break;
        }

        sb.AppendLine("    }");
    }

    private string GenerateCloneExpression(MemberInfo member, TypeInfo typeInfo, SourceProductionContext context)
    {
        switch (member.CloneStrategy)
        {
            case CloneStrategy.ByReference:
                return $"this.{member.Name}";

            case CloneStrategy.ShallowCopy:
                return GenerateShallowCopyExpression(member);

            case CloneStrategy.Clone:
                return GenerateCloneCallExpression(member);

            case CloneStrategy.Custom:
                return $"Clone{member.Name}(this.{member.Name})";

            default:
                return $"this.{member.Name}";
        }
    }

    private string GenerateShallowCopyExpression(MemberInfo member)
    {
        // For collections, create a new collection with the same elements
        if (IsCollectionWithCopyConstructor(member.TypeSymbol))
        {
            return $"new {member.Type}(this.{member.Name})";
        }

        // For arrays
        if (member.TypeSymbol is IArrayTypeSymbol)
        {
            return $"(({member.Type})this.{member.Name}.Clone())";
        }

        // Default: just copy reference
        return $"this.{member.Name}";
    }

    private string GenerateCloneCallExpression(MemberInfo member)
    {
        // Check for ICloneable
        if (ImplementsICloneable(member.TypeSymbol))
        {
            return $"({member.Type})this.{member.Name}.Clone()";
        }

        // Check for Clone() method (instance methods only)
        var cloneMethod = member.TypeSymbol.GetMembers("Clone")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => !m.IsStatic && m.Parameters.Length == 0);
        if (cloneMethod is not null)
        {
            return $"this.{member.Name}.Clone()";
        }

        // Check for copy constructor
        if (member.TypeSymbol is INamedTypeSymbol namedType && HasCopyConstructor(namedType))
        {
            return $"new {member.Type}(this.{member.Name})";
        }

        // For collections with copy constructors
        if (IsCollectionWithCopyConstructor(member.TypeSymbol))
        {
            return $"new {member.Type}(this.{member.Name})";
        }

        // Fallback
        return $"this.{member.Name}";
    }

    private void GenerateRecordWithConstruction(StringBuilder sb, TypeInfo typeInfo, Dictionary<string, string> cloneExprs)
    {
        // For records with init properties, use with-expression
        sb.Append("        return this with { ");

        var assignments = typeInfo.Members
            .Where(m => !m.IsReadOnly) // Can't set readonly members in with-expression
            .Select(m => $"{m.Name} = {cloneExprs[m.Name]}");

        sb.Append(string.Join(", ", assignments));

        sb.AppendLine(" };");
    }

    private void GenerateCopyConstructorConstruction(StringBuilder sb, TypeInfo typeInfo, Dictionary<string, string> cloneExprs)
    {
        // Create using copy constructor, then set members that need custom cloning
        sb.AppendLine($"        var clone = new {typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}(this);");

        // Override members that need special cloning
        foreach (var member in typeInfo.Members.Where(m => m.CloneStrategy != CloneStrategy.ByReference && !m.IsReadOnly && !m.IsInitOnly))
        {
            sb.AppendLine($"        clone.{member.Name} = {cloneExprs[member.Name]};");
        }

        sb.AppendLine("        return clone;");
    }

    private void GenerateParameterlessConstructorConstruction(StringBuilder sb, TypeInfo typeInfo, Dictionary<string, string> cloneExprs)
    {
        // Use object initializer syntax if possible
        // Init-only properties can be set in object initializers
        var settableMembers = typeInfo.Members.Where(m => !m.IsReadOnly).ToList();

        if (settableMembers.Count > 0)
        {
            sb.AppendLine($"        return new {typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");
            sb.AppendLine("        {");
            foreach (var member in settableMembers)
            {
                sb.AppendLine($"            {member.Name} = {cloneExprs[member.Name]},");
            }
            sb.AppendLine("        };");
        }
        else
        {
            sb.AppendLine($"        return new {typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}();");
        }
    }

    // Helper classes
    private class PrototypeConfig
    {
        public PrototypeMode Mode { get; set; } = PrototypeMode.ShallowWithWarnings;
        public string CloneMethodName { get; set; } = "Clone";
        public bool CloneMethodNameExplicit { get; set; }
        public bool IncludeExplicit { get; set; }
    }

    // Internal enums that mirror the public attribute enums
    // These must stay in sync with PatternKit.Generators.Prototype.PrototypeMode
    private enum PrototypeMode
    {
        ShallowWithWarnings = 0,
        Shallow = 1,
        DeepWhenPossible = 2
    }

    private enum ConstructionStrategy
    {
        None,
        RecordWith,
        CopyConstructor,
        ParameterlessConstructor
    }

    // Internal enum that mirrors PatternKit.Generators.Prototype.PrototypeCloneStrategy
    // Values must stay in sync with the public enum
    private enum CloneStrategy
    {
        ByReference = 0,
        ShallowCopy = 1,
        Clone = 2,
        DeepCopy = 3,
        Custom = 4
    }

    private class TypeInfo
    {
        public INamedTypeSymbol TypeSymbol { get; set; } = null!;
        public string TypeName { get; set; } = "";
        public string Namespace { get; set; } = "";
        public bool IsClass { get; set; }
        public bool IsStruct { get; set; }
        public bool IsRecordClass { get; set; }
        public bool IsRecordStruct { get; set; }
        public List<MemberInfo> Members { get; set; } = new();
        public ConstructionStrategy ConstructionStrategy { get; set; }
    }

    private class MemberInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public ITypeSymbol TypeSymbol { get; set; } = null!;
        public bool IsProperty { get; set; }
        public bool IsField { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsInitOnly { get; set; }
        public CloneStrategy CloneStrategy { get; set; }
        public ISymbol Symbol { get; set; } = null!;
    }
}
