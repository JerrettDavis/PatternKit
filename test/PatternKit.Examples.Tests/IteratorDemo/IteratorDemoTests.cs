using PatternKit.Examples.IteratorDemo;
using static PatternKit.Examples.IteratorDemo.IteratorDemo;
using TinyBDD;

namespace PatternKit.Examples.Tests.IteratorDemoTests;

public sealed class IteratorDemoTests
{
    [Scenario("SensorReading Record Works")]
    [Fact]
    public void SensorReading_Record_Works()
    {
        var reading = new SensorReading("sensor-001", "temperature", 25.5, DateTime.UtcNow);

        ScenarioExpect.Equal("sensor-001", reading.SensorId);
        ScenarioExpect.Equal("temperature", reading.Type);
        ScenarioExpect.Equal(25.5, reading.Value);
    }

    [Scenario("ProcessedReading Record Works")]
    [Fact]
    public void ProcessedReading_Record_Works()
    {
        var reading = new ProcessedReading("sensor-001", "temperature", 25.5, 0.5, "normal");

        ScenarioExpect.Equal("sensor-001", reading.SensorId);
        ScenarioExpect.Equal("temperature", reading.Type);
        ScenarioExpect.Equal(25.5, reading.Value);
        ScenarioExpect.Equal(0.5, reading.NormalizedValue);
        ScenarioExpect.Equal("normal", reading.Status);
    }

    [Scenario("Alert Record Works")]
    [Fact]
    public void Alert_Record_Works()
    {
        var alert = new Alert("sensor-001", "High temp", "critical", DateTime.UtcNow);

        ScenarioExpect.Equal("sensor-001", alert.SensorId);
        ScenarioExpect.Equal("High temp", alert.Message);
        ScenarioExpect.Equal("critical", alert.Severity);
    }

    [Scenario("AggregatedStats Record Works")]
    [Fact]
    public void AggregatedStats_Record_Works()
    {
        var stats = new AggregatedStats("temperature", 20.0, 45.0, 32.5, 100);

        ScenarioExpect.Equal("temperature", stats.Type);
        ScenarioExpect.Equal(20.0, stats.Min);
        ScenarioExpect.Equal(45.0, stats.Max);
        ScenarioExpect.Equal(32.5, stats.Average);
        ScenarioExpect.Equal(100, stats.Count);
    }

    [Scenario("GenerateSensorReadings Creates Specified Count")]
    [Fact]
    public void GenerateSensorReadings_Creates_Specified_Count()
    {
        var readings = GenerateSensorReadings(10).ToList();

        ScenarioExpect.Equal(10, readings.Count);
    }

    [Scenario("GenerateSensorReadings Creates Valid Readings")]
    [Fact]
    public void GenerateSensorReadings_Creates_Valid_Readings()
    {
        var readings = GenerateSensorReadings(20).ToList();

        foreach (var reading in readings)
        {
            ScenarioExpect.NotNull(reading.SensorId);
            ScenarioExpect.NotNull(reading.Type);
            ScenarioExpect.True(reading.Value > 0);
            ScenarioExpect.Contains(reading.Type, new[] { "temperature", "humidity", "pressure" });
        }
    }

    [Scenario("IsValidReading Returns True For Valid")]
    [Fact]
    public void IsValidReading_Returns_True_For_Valid()
    {
        var reading = new SensorReading("sensor-001", "temperature", 25.0, DateTime.UtcNow);

        ScenarioExpect.True(IsValidReading(reading));
    }

    [Scenario("IsValidReading Returns False For Zero")]
    [Fact]
    public void IsValidReading_Returns_False_For_Zero()
    {
        var reading = new SensorReading("sensor-001", "temperature", 0, DateTime.UtcNow);

        ScenarioExpect.False(IsValidReading(reading));
    }

    [Scenario("IsValidReading Returns False For High Value")]
    [Fact]
    public void IsValidReading_Returns_False_For_High_Value()
    {
        var reading = new SensorReading("sensor-001", "temperature", 15000, DateTime.UtcNow);

        ScenarioExpect.False(IsValidReading(reading));
    }

    [Scenario("NormalizeValue Temperature")]
    [Fact]
    public void NormalizeValue_Temperature()
    {
        var normalized = NormalizeValue("temperature", 35);

        ScenarioExpect.Equal(0.5, normalized); // (35 - 20) / 30 = 0.5
    }

    [Scenario("NormalizeValue Humidity")]
    [Fact]
    public void NormalizeValue_Humidity()
    {
        var normalized = NormalizeValue("humidity", 50);

        ScenarioExpect.Equal(0.5, normalized); // 50 / 100 = 0.5
    }

    [Scenario("NormalizeValue Pressure")]
    [Fact]
    public void NormalizeValue_Pressure()
    {
        var normalized = NormalizeValue("pressure", 1010);

        ScenarioExpect.Equal(0.5, normalized); // (1010 - 980) / 60 = 0.5
    }

    [Scenario("DetermineStatus Temperature Critical")]
    [Fact]
    public void DetermineStatus_Temperature_Critical()
    {
        var status = DetermineStatus("temperature", 50);

        ScenarioExpect.Equal("critical", status);
    }

    [Scenario("DetermineStatus Temperature Warning")]
    [Fact]
    public void DetermineStatus_Temperature_Warning()
    {
        var status = DetermineStatus("temperature", 40);

        ScenarioExpect.Equal("warning", status);
    }

    [Scenario("DetermineStatus Temperature Normal")]
    [Fact]
    public void DetermineStatus_Temperature_Normal()
    {
        var status = DetermineStatus("temperature", 25);

        ScenarioExpect.Equal("normal", status);
    }

    [Scenario("DetermineStatus Humidity Critical")]
    [Fact]
    public void DetermineStatus_Humidity_Critical()
    {
        var status = DetermineStatus("humidity", 90);

        ScenarioExpect.Equal("critical", status);
    }

    [Scenario("DetermineStatus Humidity Warning")]
    [Fact]
    public void DetermineStatus_Humidity_Warning()
    {
        var status = DetermineStatus("humidity", 75);

        ScenarioExpect.Equal("warning", status);
    }

    [Scenario("DetermineStatus Pressure Warning")]
    [Fact]
    public void DetermineStatus_Pressure_Warning()
    {
        var status = DetermineStatus("pressure", 1035);

        ScenarioExpect.Equal("warning", status);
    }

    [Scenario("ProcessReading Creates ProcessedReading")]
    [Fact]
    public void ProcessReading_Creates_ProcessedReading()
    {
        var reading = new SensorReading("sensor-001", "temperature", 35, DateTime.UtcNow);

        var processed = ProcessReading(reading);

        ScenarioExpect.Equal("sensor-001", processed.SensorId);
        ScenarioExpect.Equal("temperature", processed.Type);
        ScenarioExpect.Equal(35, processed.Value);
        ScenarioExpect.Equal(0.5, processed.NormalizedValue);
        ScenarioExpect.Equal("normal", processed.Status);
    }

    [Scenario("CreateAlert From ProcessedReading")]
    [Fact]
    public void CreateAlert_From_ProcessedReading()
    {
        var processed = new ProcessedReading("sensor-001", "temperature", 50, 1.0, "critical");

        var alert = CreateAlert(processed);

        ScenarioExpect.Equal("sensor-001", alert.SensorId);
        ScenarioExpect.Equal("critical", alert.Severity);
        ScenarioExpect.Contains("TEMPERATURE", alert.Message);
    }

    [Scenario("CreateProcessingPipeline Filters And Transforms")]
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

        ScenarioExpect.Single(results);
        ScenarioExpect.Equal("s2", results[0].SensorId);
        ScenarioExpect.Equal("critical", results[0].Status);
    }

    [Scenario("CreateSharedPipeline Creates SharedFlow")]
    [Fact]
    public void CreateSharedPipeline_Creates_SharedFlow()
    {
        var readings = GenerateSensorReadings(10).ToList();

        var shared = CreateSharedPipeline(readings);

        ScenarioExpect.NotNull(shared);
    }

    [Scenario("CreateSharedPipeline Forks Work Independently")]
    [Fact]
    public void CreateSharedPipeline_Forks_Work_Independently()
    {
        var readings = GenerateSensorReadings(10).ToList();

        var shared = CreateSharedPipeline(readings);

        var fork1 = shared.Fork().ToList();
        var fork2 = shared.Fork().ToList();

        ScenarioExpect.Equal(fork1.Count, fork2.Count);
    }

    [Scenario("Run Executes Without Errors")]
    [Fact]
    public void Run_Executes_Without_Errors()
    {
        PatternKit.Examples.IteratorDemo.IteratorDemo.Run();
    }
}
