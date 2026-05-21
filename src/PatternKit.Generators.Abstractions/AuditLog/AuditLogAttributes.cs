namespace PatternKit.Generators.AuditLog;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateAuditLogAttribute : Attribute
{
    public GenerateAuditLogAttribute(Type entryType, Type keyType)
    {
        EntryType = entryType ?? throw new ArgumentNullException(nameof(entryType));
        KeyType = keyType ?? throw new ArgumentNullException(nameof(keyType));
    }

    public Type EntryType { get; }

    public Type KeyType { get; }

    public string FactoryName { get; set; } = "Create";

    public string LogName { get; set; } = "audit-log";
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class AuditLogKeySelectorAttribute : Attribute;
