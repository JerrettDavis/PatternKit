using System;
using System.Collections.Generic;
using PatternKit.Generators.Factories;

namespace PatternKit.Examples.Generators;

// NOTE: This is a minimal stub to ensure the examples and docfx metadata build without
// depending on source generator output. The real implementation may be augmented
// by the PatternKit.Generators FactoryClass source generator via partial types.

public partial class OrchestratorStepFactory
{
    private readonly IServiceProvider _services;
    private readonly IReadOnlyDictionary<string, Type> _stepTypes;

    public OrchestratorStepFactory(IServiceProvider? services = null)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));

        // These keys must match the [FactoryClassKey] attributes on the step types.
        _stepTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "seed", typeof(SeedDataStep) },
            { "warm-cache", typeof(WarmCacheStep) },
            { "start-workers", typeof(StartWorkersStep) },
        };
    }

    // Use a different method name so we don't collide with the source-generated
    // OrchestratorStepFactory.Create method.
    public IOrchestratorStep CreateFromKey(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        if (!_stepTypes.TryGetValue(key, out var type))
        {
            throw new KeyNotFoundException($"Unknown orchestrator step key: '{key}'.");
        }

        // Resolve from the service provider if possible, otherwise fall back to Activator.
        var instance = _services.GetService(type) ?? Activator.CreateInstance(type);
        if (instance is not IOrchestratorStep step)
        {
            throw new InvalidOperationException($"Type '{type.FullName}' does not implement {nameof(IOrchestratorStep)}.");
        }

        return step;
    }
}
