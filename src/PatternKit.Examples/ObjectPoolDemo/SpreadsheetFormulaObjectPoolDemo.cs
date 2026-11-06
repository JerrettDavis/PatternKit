using Microsoft.Extensions.DependencyInjection;
using PatternKit.Creational.ObjectPool;
using PatternKit.Generators.ObjectPool;

namespace PatternKit.Examples.ObjectPoolDemo;

public sealed record FormulaEvaluationRequest(string Cell, string Expression, IReadOnlyDictionary<string, decimal> Variables);

public sealed record FormulaEvaluationResult(string Cell, decimal Value, int TemporaryAllocations);

public sealed class FormulaEvaluationBuffer
{
    private readonly Dictionary<string, decimal> _variables = new(StringComparer.Ordinal);
    private readonly List<decimal> _stack = [];

    public int TemporaryAllocations => _variables.Count + _stack.Count;

    public void Load(IReadOnlyDictionary<string, decimal> variables)
    {
        foreach (var variable in variables)
            _variables[variable.Key] = variable.Value;
    }

    public decimal Evaluate(string expression)
    {
        _stack.Clear();
        foreach (var token in expression.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            _stack.Add(_variables.TryGetValue(token, out var value) ? value : decimal.Parse(token));

        return _stack.Sum();
    }

    public void Reset()
    {
        _variables.Clear();
        _stack.Clear();
    }
}

[GenerateObjectPool(typeof(FormulaEvaluationBuffer), FactoryMethodName = nameof(CreateGenerated), MaxRetained = 16, ResetMethodName = nameof(FormulaEvaluationBuffer.Reset))]
public static partial class SpreadsheetFormulaBufferPools
{
    public static ObjectPool<FormulaEvaluationBuffer> CreateFluent()
        => ObjectPool<FormulaEvaluationBuffer>
            .Create()
            .WithFactory(static () => new FormulaEvaluationBuffer())
            .OnReturn(static buffer => buffer.Reset())
            .WithMaxRetained(16)
            .Build();
}

public sealed class SpreadsheetFormulaService(ObjectPool<FormulaEvaluationBuffer> buffers)
{
    public FormulaEvaluationResult Evaluate(FormulaEvaluationRequest request)
    {
        using var lease = buffers.Rent();
        lease.Value.Load(request.Variables);
        var value = lease.Value.Evaluate(request.Expression);
        return new FormulaEvaluationResult(request.Cell, value, lease.Value.TemporaryAllocations);
    }
}

public sealed class SpreadsheetFormulaObjectPoolDemoRunner(SpreadsheetFormulaService service)
{
    public FormulaEvaluationResult Run(FormulaEvaluationRequest request) => service.Evaluate(request);

    public static FormulaEvaluationResult RunFluent(FormulaEvaluationRequest request)
    {
        using var pool = SpreadsheetFormulaBufferPools.CreateFluent();
        return new SpreadsheetFormulaService(pool).Evaluate(request);
    }

    public static FormulaEvaluationResult RunGenerated(FormulaEvaluationRequest request)
    {
        using var pool = SpreadsheetFormulaBufferPools.CreateGenerated();
        return new SpreadsheetFormulaService(pool).Evaluate(request);
    }
}

public static class SpreadsheetFormulaObjectPoolDemoServiceCollectionExtensions
{
    public static IServiceCollection AddSpreadsheetFormulaObjectPoolDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => SpreadsheetFormulaBufferPools.CreateGenerated());
        services.AddSingleton<SpreadsheetFormulaService>();
        services.AddSingleton<SpreadsheetFormulaObjectPoolDemoRunner>();
        return services;
    }
}
