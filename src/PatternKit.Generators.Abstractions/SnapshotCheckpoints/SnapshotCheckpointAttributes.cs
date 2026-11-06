namespace PatternKit.Generators.SnapshotCheckpoints;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateSnapshotCheckpointManagerAttribute : Attribute
{
    public GenerateSnapshotCheckpointManagerAttribute(Type keyType, Type snapshotType)
    {
        KeyType = keyType;
        SnapshotType = snapshotType;
    }

    public Type KeyType { get; }

    public Type SnapshotType { get; }

    public string FactoryMethodName { get; set; } = "Create";

    public string ManagerName { get; set; } = "snapshot-checkpoints";
}
