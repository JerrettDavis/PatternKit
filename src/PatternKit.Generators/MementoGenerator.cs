using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Memento pattern.
/// Generates immutable memento structs and optional caretaker classes for undo/redo history.
/// </summary>
[Generator]
public sealed class MementoGenerator : IIncrementalGenerator
{
    // Diagnostic IDs
    private const string DiagIdTypeNotPartial = "PKMEM001";
    private const string DiagIdInaccessibleMember = "PKMEM002";
    private const string DiagIdUnsafeReferenceCapture = "PKMEM003";
    private const string DiagIdCloneMechanismMissing = "PKMEM004";
    private const string DiagIdRecordRestoreFailed = "PKMEM005";
    private const string DiagIdInitOnlyRestriction = "PKMEM006";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type marked with [Memento] must be partial",
        messageFormat: "Type '{0}' is marked with [Memento] but is not declared as partial. Add the 'partial' keyword to the type declaration.",
        category: "PatternKit.Generators.Memento",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InaccessibleMemberDescriptor = new(
        id: DiagIdInaccessibleMember,
        title: "Member is inaccessible for memento capture or restore",
        messageFormat: "Member '{0}' cannot be accessed for memento operations. Ensure the member has appropriate accessibility.",
        category: "PatternKit.Generators.Memento",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsafeReferenceCaptureDescriptor = new(
        id: DiagIdUnsafeReferenceCapture,
        title: "Unsafe reference capture",
        messageFormat: "Member '{0}' is a mutable reference type captured by reference. Mutations will affect all snapshots. Consider using [MementoStrategy(Clone)] or [MementoStrategy(DeepCopy)].",
        category: "PatternKit.Generators.Memento",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CloneMechanismMissingDescriptor = new(
        id: DiagIdCloneMechanismMissing,
        title: "Clone strategy requested but mechanism missing",
        messageFormat: "Member '{0}' has [MementoStrategy(Clone)] but no suitable clone mechanism is available. Implement ICloneable or provide a custom cloner.",
        category: "PatternKit.Generators.Memento",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RecordRestoreFailedDescriptor = new(
        id: DiagIdRecordRestoreFailed,
        title: "Record restore generation failed",
        messageFormat: "Cannot generate RestoreNew for record type '{0}'. No accessible constructor or with-expression path is viable. Ensure the record has an accessible primary constructor.",
        category: "PatternKit.Generators.Memento",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InitOnlyRestrictionDescriptor = new(
        id: DiagIdInitOnlyRestriction,
        title: "Init-only or readonly restrictions prevent in-place restore",
        messageFormat: "Member '{0}' is init-only or readonly, preventing in-place restore. Only RestoreNew() will be generated for this type.",
        category: "PatternKit.Generators.Memento",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all type declarations with [Memento] attribute
        var mementoTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.MementoAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        // Generate for each type
        context.RegisterSourceOutput(mementoTypes, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.MementoAttribute");
            if (attr is null)
                return;

            GenerateMementoForType(spc, typeSymbol, attr, typeContext.TargetNode);
        });
    }

    private void GenerateMementoForType(
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

        // Parse attribute arguments
        var config = ParseMementoConfig(attribute);

        // Analyze type and members
        var typeInfo = AnalyzeType(typeSymbol, config, context);
        if (typeInfo is null)
            return;

        // Generate memento struct
        var mementoSource = GenerateMementoStruct(typeInfo, context);
        if (!string.IsNullOrEmpty(mementoSource))
        {
            var fileName = $"{typeSymbol.Name}.Memento.g.cs";
            context.AddSource(fileName, mementoSource);
        }

        // Generate caretaker if requested
        if (config.GenerateCaretaker)
        {
            var caretakerSource = GenerateCaretaker(typeInfo, config, context);
            if (!string.IsNullOrEmpty(caretakerSource))
            {
                var fileName = $"{typeSymbol.Name}.History.g.cs";
                context.AddSource(fileName, caretakerSource);
            }
        }
    }

    private MementoConfig ParseMementoConfig(AttributeData attribute)
    {
        var config = new MementoConfig();

        foreach (var named in attribute.NamedArguments)
        {
            switch (named.Key)
            {
                case nameof(MementoAttribute.GenerateCaretaker):
                    config.GenerateCaretaker = (bool)named.Value.Value!;
                    break;
                case nameof(MementoAttribute.Capacity):
                    config.Capacity = (int)named.Value.Value!;
                    break;
                case nameof(MementoAttribute.InclusionMode):
                    config.InclusionMode = (int)named.Value.Value!;
                    break;
                case nameof(MementoAttribute.SkipDuplicates):
                    config.SkipDuplicates = (bool)named.Value.Value!;
                    break;
            }
        }

        return config;
    }

    private TypeInfo? AnalyzeType(
        INamedTypeSymbol typeSymbol,
        MementoConfig config,
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
        var members = GetMembersForMemento(typeSymbol, config, context);
        typeInfo.Members.AddRange(members);

        if (typeInfo.Members.Count == 0)
        {
            // No members to capture - this might be intentional, but warn
            return typeInfo;
        }

        return typeInfo;
    }

    private List<MemberInfo> GetMembersForMemento(
        INamedTypeSymbol typeSymbol,
        MementoConfig config,
        SourceProductionContext context)
    {
        var members = new List<MemberInfo>();
        var includeAll = config.InclusionMode == 0; // IncludeAll

        // Filter to only public instance properties and fields
        var candidateMembers = typeSymbol.GetMembers()
            .Where(m => (m is IPropertySymbol || m is IFieldSymbol) &&
                        !m.IsStatic &&
                        m.DeclaredAccessibility == Accessibility.Public);

        foreach (var member in candidateMembers)
        {
            // Check for attributes
            var hasIgnore = GeneratorUtilities.HasAttribute(member, "PatternKit.Generators.MementoIgnoreAttribute");
            var hasInclude = GeneratorUtilities.HasAttribute(member, "PatternKit.Generators.MementoIncludeAttribute");
            var strategyAttr = GetAttribute(member, "PatternKit.Generators.MementoStrategyAttribute");

            // Determine if this member should be included
            bool shouldInclude = includeAll ? !hasIgnore : hasInclude;
            if (!shouldInclude)
                continue;

            // Ensure member has a getter
            ITypeSymbol? memberType = null;
            bool isReadOnly = false;
            bool isInitOnly = false;

            if (member is IPropertySymbol prop)
            {
                if (prop.GetMethod is null || prop.GetMethod.DeclaredAccessibility != Accessibility.Public)
                    continue;

                // Skip properties that cannot be restored:
                // - computed properties (no setter)
                // - init-only properties on non-record types
                if (prop.SetMethod is null || (prop.SetMethod.IsInitOnly && !typeSymbol.IsRecord))
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

            // Determine capture strategy
            var strategy = DetermineCaptureStrategy(member, memberType, strategyAttr, context);

            members.Add(new MemberInfo
            {
                Name = member.Name,
                Type = memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                TypeSymbol = memberType,
                IsProperty = member is IPropertySymbol,
                IsField = member is IFieldSymbol,
                IsReadOnly = isReadOnly,
                IsInitOnly = isInitOnly,
                CaptureStrategy = strategy
            });
        }

        return members;
    }

    private int DetermineCaptureStrategy(
        ISymbol member,
        ITypeSymbol memberType,
        AttributeData? strategyAttr,
        SourceProductionContext context)
    {
        // If explicit strategy provided, use it
        if (strategyAttr is not null)
        {
            var ctorArg = strategyAttr.ConstructorArguments.FirstOrDefault();
            if (ctorArg.Value is int strategyValue)
                return strategyValue;
        }

        // Otherwise, infer safe default
        // Value types and string: ByReference (safe)
        if (memberType.IsValueType || memberType.SpecialType == SpecialType.System_String)
            return 0; // ByReference

        // Reference types: warn and default to ByReference
        context.ReportDiagnostic(Diagnostic.Create(
            UnsafeReferenceCaptureDescriptor,
            member.Locations.FirstOrDefault(),
            member.Name));

        return 0; // ByReference (with warning)
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.ToDisplayString() == attributeName);
    }

    private string GenerateMementoStruct(TypeInfo typeInfo, SourceProductionContext context)
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

        sb.AppendLine($"public readonly partial struct {typeInfo.TypeName}Memento");
        sb.AppendLine("{");

        // Version field (use computed hash to avoid conflicts)
        sb.AppendLine("    /// <summary>Memento version for compatibility checking.</summary>");
        sb.AppendLine($"    public int MementoVersion => {ComputeVersionHash(typeInfo)};");
        sb.AppendLine();

        // Member properties
        foreach (var member in typeInfo.Members)
        {
            sb.AppendLine($"    public {member.Type} {member.Name} {{ get; }}");
        }
        sb.AppendLine();

        // Constructor
        sb.Append($"    private {typeInfo.TypeName}Memento(");
        sb.Append(string.Join(", ", typeInfo.Members.Select(m => $"{m.Type} {GeneratorUtilities.ToCamelCase(m.Name)}")));
        sb.AppendLine(")");
        sb.AppendLine("    {");
        foreach (var member in typeInfo.Members)
        {
            sb.AppendLine($"        {member.Name} = {GeneratorUtilities.ToCamelCase(member.Name)};");
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        // Capture method
        GenerateCaptureMethod(sb, typeInfo);
        sb.AppendLine();

        // Restore methods
        GenerateRestoreMethods(sb, typeInfo, context);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private void GenerateCaptureMethod(StringBuilder sb, TypeInfo typeInfo)
    {
        sb.AppendLine($"    /// <summary>Captures the current state of the originator as an immutable memento.</summary>");
        sb.AppendLine($"    public static {typeInfo.TypeName}Memento Capture(in {typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} originator)");
        sb.AppendLine("    {");
        sb.Append($"        return new {typeInfo.TypeName}Memento(");
        sb.Append(string.Join(", ", typeInfo.Members.Select(m => $"originator.{m.Name}")));
        sb.AppendLine(");");
        sb.AppendLine("    }");
    }

    private void GenerateRestoreMethods(StringBuilder sb, TypeInfo typeInfo, SourceProductionContext context)
    {
        // Always generate RestoreNew for all types
        GenerateRestoreNewMethod(sb, typeInfo, context);

        // For mutable types (non-record or record with setters), also generate in-place Restore
        bool hasMutableMembers = typeInfo.Members.Any(m => !m.IsReadOnly && !m.IsInitOnly);
        if (!typeInfo.IsRecordClass && !typeInfo.IsRecordStruct && hasMutableMembers)
        {
            sb.AppendLine();
            GenerateInPlaceRestoreMethod(sb, typeInfo);
        }
    }

    private void GenerateRestoreNewMethod(StringBuilder sb, TypeInfo typeInfo, SourceProductionContext context)
    {
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>Restores the memento state by creating a new instance.</summary>");
        sb.AppendLine($"    public {typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} RestoreNew()");
        sb.AppendLine("    {");

        if (typeInfo.IsRecordClass || typeInfo.IsRecordStruct)
        {
            // For records, try using positional constructor if parameters match members
            // For now, use simple object initializer which works with records
            sb.Append($"        return new {typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");

            // Try to use positional constructor
            if (typeInfo.Members.Count > 0)
            {
                // Check if we can use positional parameters (primary constructor)
                // Verify parameter names and types match the members
                var primaryCtor = typeInfo.TypeSymbol.Constructors.FirstOrDefault(c =>
                {
                    if (c.Parameters.Length != typeInfo.Members.Count)
                        return false;

                    // Check if parameter names and types match members (case-insensitive for names)
                    for (int i = 0; i < c.Parameters.Length; i++)
                    {
                        var param = c.Parameters[i];
                        var member = typeInfo.Members.FirstOrDefault(m =>
                            string.Equals(m.Name, param.Name, StringComparison.OrdinalIgnoreCase) &&
                            SymbolEqualityComparer.Default.Equals(m.TypeSymbol, param.Type));

                        if (member is null)
                            return false;
                    }

                    return true;
                });

                if (primaryCtor is not null)
                {
                    // Use positional constructor, ordered by parameter order
                    sb.Append("(");
                    var orderedMembers = primaryCtor.Parameters
                        .Select(p => typeInfo.Members.First(m =>
                            string.Equals(m.Name, p.Name, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    sb.Append(string.Join(", ", orderedMembers.Select(m => $"this.{m.Name}")));
                    sb.AppendLine(");");
                }
                else
                {
                    // Fall back to object initializer
                    sb.AppendLine("()");
                    sb.AppendLine("        {");
                    foreach (var member in typeInfo.Members)
                    {
                        sb.AppendLine($"            {member.Name} = this.{member.Name},");
                    }
                    sb.AppendLine("        };");
                }
            }
            else
            {
                sb.AppendLine("();");
            }
        }
        else
        {
            // For classes/structs, use object initializer (only include settable members)
            sb.Append($"        return new {typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");
            var settableMembers = typeInfo.Members.Where(m => !m.IsReadOnly && !m.IsInitOnly).ToList();
            if (settableMembers.Count > 0)
            {
                sb.AppendLine("()");
                sb.AppendLine("        {");
                foreach (var member in settableMembers)
                {
                    sb.AppendLine($"            {member.Name} = this.{member.Name},");
                }
                sb.AppendLine("        };");
            }
            else
            {
                sb.AppendLine("();");
            }
        }

        sb.AppendLine("    }");
    }

    private void GenerateInPlaceRestoreMethod(StringBuilder sb, TypeInfo typeInfo)
    {
        sb.AppendLine($"    /// <summary>Restores the memento state to an existing originator instance (in-place).</summary>");
        sb.AppendLine($"    public void Restore({typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} originator)");
        sb.AppendLine("    {");

        // Filter to only settable members
        var settableMembers = typeInfo.Members.Where(m => !m.IsReadOnly && !m.IsInitOnly);
        foreach (var member in settableMembers)
        {
            sb.AppendLine($"        originator.{member.Name} = this.{member.Name};");
        }

        sb.AppendLine("    }");
    }

    private string GenerateCaretaker(TypeInfo typeInfo, MementoConfig config, SourceProductionContext context)
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

        sb.AppendLine($"/// <summary>Manages undo/redo history for {typeInfo.TypeName}.</summary>");
        sb.AppendLine($"public sealed partial class {typeInfo.TypeName}History");
        sb.AppendLine("{");

        // Fields
        sb.AppendLine($"    private readonly System.Collections.Generic.List<{typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}> _states = new();");
        sb.AppendLine("    private int _currentIndex = -1;");
        if (config.Capacity > 0)
        {
            sb.AppendLine($"    private const int MaxCapacity = {config.Capacity};");
        }
        sb.AppendLine();

        // Properties
        sb.AppendLine("    /// <summary>Total number of states in history.</summary>");
        sb.AppendLine("    public int Count => _states.Count;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>True if undo is possible.</summary>");
        sb.AppendLine("    public bool CanUndo => _currentIndex > 0;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>True if redo is possible.</summary>");
        sb.AppendLine("    public bool CanRedo => _currentIndex >= 0 && _currentIndex < _states.Count - 1;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Current state (or default if empty).</summary>");
        sb.AppendLine($"    public {typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} Current");
        sb.AppendLine("    {");
        sb.AppendLine("        get => _currentIndex >= 0 ? _states[_currentIndex] : default!;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Constructor
        sb.AppendLine($"    public {typeInfo.TypeName}History({typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} initial)");
        sb.AppendLine("    {");
        sb.AppendLine("        _states.Add(initial);");
        sb.AppendLine("        _currentIndex = 0;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Capture method
        sb.AppendLine($"    /// <summary>Captures a new state, truncating forward history if not at the end.</summary>");
        sb.AppendLine($"    public void Capture({typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} state)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Truncate forward history if we're not at the end");
        sb.AppendLine("        if (_currentIndex < _states.Count - 1)");
        sb.AppendLine("        {");
        sb.AppendLine("            _states.RemoveRange(_currentIndex + 1, _states.Count - _currentIndex - 1);");
        sb.AppendLine("        }");
        sb.AppendLine();

        if (config.SkipDuplicates)
        {
            sb.AppendLine("        // Skip duplicates");
            sb.AppendLine("        if (_currentIndex >= 0 && System.Collections.Generic.EqualityComparer<" + typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">.Default.Equals(_states[_currentIndex], state))");
            sb.AppendLine("            return;");
            sb.AppendLine();
        }

        sb.AppendLine("        _states.Add(state);");
        sb.AppendLine("        _currentIndex++;");
        sb.AppendLine();

        if (config.Capacity > 0)
        {
            sb.AppendLine("        // Evict oldest if over capacity");
            sb.AppendLine("        if (_states.Count > MaxCapacity)");
            sb.AppendLine("        {");
            sb.AppendLine("            _states.RemoveAt(0);");
            sb.AppendLine("            _currentIndex--;");
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // Undo method
        sb.AppendLine("    /// <summary>Moves back to the previous state.</summary>");
        sb.AppendLine("    public bool Undo()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (!CanUndo) return false;");
        sb.AppendLine("        _currentIndex--;");
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Redo method
        sb.AppendLine("    /// <summary>Moves forward to the next state.</summary>");
        sb.AppendLine("    public bool Redo()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (!CanRedo) return false;");
        sb.AppendLine("        _currentIndex++;");
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Clear method
        sb.AppendLine("    /// <summary>Clears all history and resets to initial state.</summary>");
        sb.AppendLine($"    public void Clear({typeInfo.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} initial)");
        sb.AppendLine("    {");
        sb.AppendLine("        _states.Clear();");
        sb.AppendLine("        _states.Add(initial);");
        sb.AppendLine("        _currentIndex = 0;");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static int ComputeVersionHash(TypeInfo typeInfo)
    {
        // Simple deterministic hash based on member names and types
        // Using FNV-1a hash algorithm for compatibility with netstandard2.0
        unchecked
        {
            const int FnvPrime = 16777619;
            int hash = (int)2166136261;

            foreach (var member in typeInfo.Members.OrderBy(m => m.Name))
            {
                foreach (char c in member.Name)
                {
                    hash = (hash ^ c) * FnvPrime;
                }
                foreach (char c in member.Type)
                {
                    hash = (hash ^ c) * FnvPrime;
                }
            }

            return hash;
        }
    }

    // Helper classes
    private class MementoConfig
    {
        public bool GenerateCaretaker { get; set; }
        public int Capacity { get; set; }
        public int InclusionMode { get; set; }
        public bool SkipDuplicates { get; set; } = true;
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
        public int CaptureStrategy { get; set; }
    }
}
