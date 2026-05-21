using System;

namespace PatternKit.Generators.Cloud;

/// <summary>
/// Generates a typed external-configuration-store factory for a partial class or struct.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateExternalConfigurationStoreAttribute : Attribute
{
    /// <summary>Creates an external-configuration-store generator attribute.</summary>
    public GenerateExternalConfigurationStoreAttribute(Type settingsType)
        => SettingsType = settingsType ?? throw new ArgumentNullException(nameof(settingsType));

    /// <summary>Typed settings loaded by the generated store.</summary>
    public Type SettingsType { get; }

    /// <summary>Name of the generated factory method.</summary>
    public string FactoryName { get; set; } = "Create";

    /// <summary>Name assigned to the generated configuration store.</summary>
    public string StoreName { get; set; } = "external-configuration-store";

    /// <summary>Cache duration in milliseconds for successful snapshots.</summary>
    public int CacheMilliseconds { get; set; }
}

/// <summary>Marks the static async method that loads configuration from an external source.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ExternalConfigurationLoaderAttribute : Attribute;

/// <summary>Marks a static settings validator for a generated external configuration store.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ExternalConfigurationValidatorAttribute : Attribute
{
    /// <summary>Creates a validator attribute.</summary>
    public ExternalConfigurationValidatorAttribute(string rejectionReason, int order)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new ArgumentException("Validation rejection reason cannot be null, empty, or whitespace.", nameof(rejectionReason));

        RejectionReason = rejectionReason;
        Order = order;
    }

    /// <summary>Reason returned when this validator rejects settings.</summary>
    public string RejectionReason { get; }

    /// <summary>Validator order in the generated store.</summary>
    public int Order { get; }
}
