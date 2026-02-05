using PatternKit.Generators.Template;

namespace PatternKit.Examples.TemplateMethodGeneratorDemo;

/// <summary>
/// Context for data import workflow - contains all state for the import process.
/// </summary>
public class ImportContext
{
    public string FilePath { get; set; } = "";
    public string[] RawData { get; set; } = Array.Empty<string>();
    public List<DataRecord> Records { get; set; } = new();
    public List<string> Log { get; set; } = new();
    public bool ValidationPassed { get; set; }
}

/// <summary>
/// Represents a single data record after transformation.
/// </summary>
public record DataRecord(string Name, string Value);

/// <summary>
/// Import workflow using Template Method pattern via source generator.
/// This demonstrates a realistic data import pipeline with validation, transformation, and persistence.
/// </summary>
[Template]
public partial class ImportWorkflow
{
    /// <summary>
    /// Invoked before any steps execute. Sets up the import context.
    /// </summary>
    [TemplateHook(HookPoint.BeforeAll)]
    private void OnStart(ImportContext ctx)
    {
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Starting import from: {ctx.FilePath}");
    }

    /// <summary>
    /// Step 1: Load raw data from the source.
    /// </summary>
    [TemplateStep(0, Name = "Load")]
    private void LoadData(ImportContext ctx)
    {
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Loading data...");
        
        // Only load if RawData hasn't been pre-set (e.g., for testing)
        if (ctx.RawData.Length == 0)
        {
            // Simulate loading from file
            ctx.RawData = File.Exists(ctx.FilePath)
                ? File.ReadAllLines(ctx.FilePath)
                : new[]
                {
                    "Name:Alice;Value:100",
                    "Name:Bob;Value:200",
                    "Name:Charlie;Value:300"
                };
        }
        
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Loaded {ctx.RawData.Length} lines");
    }

    /// <summary>
    /// Step 2: Validate the loaded data.
    /// </summary>
    [TemplateStep(1, Name = "Validate")]
    private void ValidateData(ImportContext ctx)
    {
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Validating data...");
        
        // Simple validation: ensure each line has required format
        var invalidLines = ctx.RawData
            .Where(line => !line.Contains("Name:") || !line.Contains("Value:"))
            .ToList();
        
        if (invalidLines.Any())
        {
            ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] ERROR: Found {invalidLines.Count} invalid lines");
            ctx.ValidationPassed = false;
            throw new InvalidOperationException($"Validation failed: {invalidLines.Count} invalid lines");
        }
        
        ctx.ValidationPassed = true;
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Validation passed");
    }

    /// <summary>
    /// Step 3: Transform raw data into structured records.
    /// </summary>
    [TemplateStep(2, Name = "Transform")]
    private void TransformData(ImportContext ctx)
    {
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Transforming data...");
        
        var records = ctx.RawData
            .Select(line =>
            {
                var parts = line.Split(';');
                var name = parts[0].Split(':')[1];
                var value = parts[1].Split(':')[1];
                return new DataRecord(name, value);
            });
        
        ctx.Records.AddRange(records);
        
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Transformed {ctx.Records.Count} records");
    }

    /// <summary>
    /// Step 4: Persist the transformed records.
    /// </summary>
    [TemplateStep(3, Name = "Persist")]
    private void PersistData(ImportContext ctx)
    {
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Persisting data...");
        
        // Simulate persistence
        foreach (var record in ctx.Records)
        {
            ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}]   Saved: {record.Name} = {record.Value}");
        }
        
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Persisted {ctx.Records.Count} records");
    }

    /// <summary>
    /// Invoked when any step throws an exception.
    /// </summary>
    [TemplateHook(HookPoint.OnError)]
    private void OnError(ImportContext ctx, Exception ex)
    {
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] ERROR: {ex.Message}");
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Import failed");
    }

    /// <summary>
    /// Invoked after all steps complete successfully.
    /// </summary>
    [TemplateHook(HookPoint.AfterAll)]
    private void OnComplete(ImportContext ctx)
    {
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Import completed successfully");
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Summary: {ctx.Records.Count} records imported");
    }
}

/// <summary>
/// Demo runner that executes the import workflow.
/// </summary>
public static class ImportWorkflowDemo
{
    public static List<string> Run(string? filePath = null)
    {
        var ctx = new ImportContext
        {
            FilePath = filePath ?? "sample.csv"
        };
        
        var workflow = new ImportWorkflow();
        
        try
        {
            workflow.Execute(ctx);
        }
        catch (Exception)
        {
            // Exception was logged by OnError hook
        }
        
        return ctx.Log;
    }
    
    public static List<string> RunWithInvalidData()
    {
        var ctx = new ImportContext
        {
            FilePath = "invalid.csv",
            RawData = new[]
            {
                "Name:Alice;Value:100",
                "InvalidLine",  // This will cause validation to fail
                "Name:Bob;Value:200"
            }
        };
        
        var workflow = new ImportWorkflow();
        
        try
        {
            workflow.Execute(ctx);
        }
        catch (Exception)
        {
            // Exception was logged by OnError hook
        }
        
        return ctx.Log;
    }
}
