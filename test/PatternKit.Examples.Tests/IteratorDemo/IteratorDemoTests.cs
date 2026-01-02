using PatternKit.Examples.IteratorDemo;
using static PatternKit.Examples.IteratorDemo.IteratorDemo;

namespace PatternKit.Examples.Tests.IteratorDemoTests;

public sealed class IteratorDemoTests
{
    [Fact]
    public void SensorReading_Record_Works()
    {
        var reading = new SensorReading("sensor-001", "temperature", 25.5, DateTime.UtcNow);

        Assert.Equal("sensor-001", reading.SensorId);
        Assert.Equal("temperature", reading.Type);
        Assert.Equal(25.5, reading.Value);
    }

    [Fact]
    public void ProcessedReading_Record_Works()
    {
        var reading = new ProcessedReading("sensor-001", "temperature", 25.5, 0.5, "normal");

        Assert.Equal("sensor-001", reading.SensorId);
        Assert.Equal("temperature", reading.Type);
        Assert.Equal(25.5, reading.Value);
        Assert.Equal(0.5, reading.NormalizedValue);
        Assert.Equal("normal", reading.Status);
    }

    [Fact]
    public void Alert_Record_Works()
    {
        var alert = new Alert("sensor-001", "High temp", "critical", DateTime.UtcNow);

        Assert.Equal("sensor-001", alert.SensorId);
        Assert.Equal("High temp", alert.Message);
        Assert.Equal("critical", alert.Severity);
    }

    [Fact]
    public void AggregatedStats_Record_Works()
    {
        var stats = new AggregatedStats("temperature", 20.0, 45.0, 32.5, 100);

        Assert.Equal("temperature", stats.Type);
        Assert.Equal(20.0, stats.Min);
        Assert.Equal(45.0, stats.Max);
        Assert.Equal(32.5, stats.Average);
        Assert.Equal(100, stats.Count);
    }

    [Fact]
    public void GenerateSensorReadings_Creates_Specified_Count()
    {
        var readings = GenerateSensorReadings(10).ToList();

        Assert.Equal(10, readings.Count);
    }

    [Fact]
    public void GenerateSensorReadings_Creates_Valid_Readings()
    {
        var readings = GenerateSensorReadings(20).ToList();

        foreach (var reading in readings)
        {
            Assert.NotNull(reading.SensorId);
            Assert.NotNull(reading.Type);
            Assert.True(reading.Value > 0);
            Assert.Contains(reading.Type, new[] { "temperature", "humidity", "pressure" });
        }
    }

    [Fact]
    public void IsValidReading_Returns_True_For_Valid()
    {
        var reading = new SensorReading("sensor-001", "temperature", 25.0, DateTime.UtcNow);

        Assert.True(IsValidReading(reading));
    }

    [Fact]
    public void IsValidReading_Returns_False_For_Zero()
    {
        var reading = new SensorReading("sensor-001", "temperature", 0, DateTime.UtcNow);

        Assert.False(IsValidReading(reading));
    }

    [Fact]
    public void IsValidReading_Returns_False_For_High_Value()
    {
        var reading = new SensorReading("sensor-001", "temperature", 15000, DateTime.UtcNow);

        Assert.False(IsValidReading(reading));
    }

    [Fact]
    public void NormalizeValue_Temperature()
    {
        var normalized = NormalizeValue("temperature", 35);

        Assert.Equal(0.5, normalized); // (35 - 20) / 30 = 0.5
    }

    [Fact]
    public void NormalizeValue_Humidity()
    {
        var normalized = NormalizeValue("humidity", 50);

        Assert.Equal(0.5, normalized); // 50 / 100 = 0.5
    }

    [Fact]
    public void NormalizeValue_Pressure()
    {
        var normalized = NormalizeValue("pressure", 1010);

        Assert.Equal(0.5, normalized); // (1010 - 980) / 60 = 0.5
    }

    [Fact]
    public void DetermineStatus_Temperature_Critical()
    {
        var status = DetermineStatus("temperature", 50);

        Assert.Equal("critical", status);
    }

    [Fact]
    public void DetermineStatus_Temperature_Warning()
    {
        var status = DetermineStatus("temperature", 40);

        Assert.Equal("warning", status);
    }

    [Fact]
    public void DetermineStatus_Temperature_Normal()
    {
        var status = DetermineStatus("temperature", 25);

        Assert.Equal("normal", status);
    }

    [Fact]
    public void DetermineStatus_Humidity_Critical()
    {
        var status = DetermineStatus("humidity", 90);

        Assert.Equal("critical", status);
    }

    [Fact]
    public void DetermineStatus_Humidity_Warning()
    {
        var status = DetermineStatus("humidity", 75);

        Assert.Equal("warning", status);
    }

    [Fact]
    public void DetermineStatus_Pressure_Warning()
    {
        var status = DetermineStatus("pressure", 1035);

        Assert.Equal("warning", status);
    }

    [Fact]
    public void ProcessReading_Creates_ProcessedReading()
    {
        var reading = new SensorReading("sensor-001", "temperature", 35, DateTime.UtcNow);

        var processed = ProcessReading(reading);

        Assert.Equal("sensor-001", processed.SensorId);
        Assert.Equal("temperature", processed.Type);
        Assert.Equal(35, processed.Value);
        Assert.Equal(0.5, processed.NormalizedValue);
        Assert.Equal("normal", processed.Status);
    }

    [Fact]
    public void CreateAlert_From_ProcessedReading()
    {
        var processed = new ProcessedReading("sensor-001", "temperature", 50, 1.0, "critical");

        var alert = CreateAlert(processed);

        Assert.Equal("sensor-001", alert.SensorId);
        Assert.Equal("critical", alert.Severity);
        Assert.Contains("TEMPERATURE", alert.Message);
    }

    [Fact]
    public void CreateProcessingPipeline_Filters_And_Transforms()
    {
        var readings = new[]
        {
            new SensorReading("s1", "temperature", 25, DateTime.UtcNow), // normal
            new SensorReading("s2", "temperature", 50, DateTime.UtcNow), // critical
            new SensorReading("s3", "temperature", 0, DateTime.UtcNow),  // invalid
        };

        var pipeline = CreateProcessingPipeline(readings);
        var results = pipeline.ToList();

        Assert.Single(results);
        Assert.Equal("s2", results[0].SensorId);
        Assert.Equal("critical", results[0].Status);
    }

    [Fact]
    public void CreateSharedPipeline_Creates_SharedFlow()
    {
        var readings = GenerateSensorReadings(10).ToList();

        var shared = CreateSharedPipeline(readings);

        Assert.NotNull(shared);
    }

    [Fact]
    public void CreateSharedPipeline_Forks_Work_Independently()
    {
        var readings = GenerateSensorReadings(10).ToList();

        var shared = CreateSharedPipeline(readings);

        var fork1 = shared.Fork().ToList();
        var fork2 = shared.Fork().ToList();

        Assert.Equal(fork1.Count, fork2.Count);
    }

    [Fact]
    public void Run_Executes_Without_Errors()
    {
        PatternKit.Examples.IteratorDemo.IteratorDemo.Run();
    }
}
