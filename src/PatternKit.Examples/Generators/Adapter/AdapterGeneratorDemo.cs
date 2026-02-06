using PatternKit.Generators.Adapter;

namespace PatternKit.Examples.Generators.Adapter;

/// <summary>
/// Demonstrates the Adapter generator with a temperature sensor scenario.
/// A legacy Fahrenheit sensor is adapted to a modern Celsius-based interface.
/// </summary>
public static class AdapterGeneratorDemo
{
    /// <summary>
    /// The modern target interface expected by the application.
    /// </summary>
    public interface ITemperatureSensor
    {
        /// <summary>Returns the current temperature in Celsius.</summary>
        double ReadCelsius();

        /// <summary>Returns the sensor display name.</summary>
        string GetName();
    }

    /// <summary>
    /// A legacy sensor that only reports temperature in Fahrenheit.
    /// </summary>
    public class FahrenheitSensor
    {
        private readonly string _model;
        private readonly double _fahrenheit;

        public FahrenheitSensor(string model, double fahrenheit)
        {
            _model = model;
            _fahrenheit = fahrenheit;
        }

        public double GetTemperatureF() => _fahrenheit;

        public string ModelName => _model;
    }

    /// <summary>
    /// Host class containing the mapping methods that bridge <see cref="FahrenheitSensor"/>
    /// to <see cref="ITemperatureSensor"/>. The generator produces
    /// <c>FahrenheitSensorToTemperatureSensorAdapter</c> implementing <see cref="ITemperatureSensor"/>.
    /// </summary>
    [GenerateAdapter(
        Target = typeof(ITemperatureSensor),
        Adaptee = typeof(FahrenheitSensor))]
    public static partial class SensorAdapterHost
    {
        [AdapterMap(TargetMember = "ReadCelsius")]
        public static double ReadCelsius(FahrenheitSensor adaptee)
            => (adaptee.GetTemperatureF() - 32.0) * 5.0 / 9.0;

        [AdapterMap(TargetMember = "GetName")]
        public static string GetName(FahrenheitSensor adaptee)
            => $"Adapted({adaptee.ModelName})";
    }

    /// <summary>
    /// Runs a demonstration of the generated adapter.
    /// </summary>
    public static List<string> Run()
    {
        var log = new List<string>();

        // Create a legacy sensor
        var legacySensor = new FahrenheitSensor("Thermo-2000", 212.0);
        log.Add($"Legacy sensor: {legacySensor.ModelName}, {legacySensor.GetTemperatureF()}F");

        // Adapt it to the modern interface
        ITemperatureSensor adapted = new FahrenheitSensorToTemperatureSensorAdapter(legacySensor);
        log.Add($"Adapted name: {adapted.GetName()}");
        log.Add($"Celsius reading: {adapted.ReadCelsius():F1}");

        // Another sensor
        var freezingSensor = new FahrenheitSensor("Ice-100", 32.0);
        ITemperatureSensor adaptedFreezing = new FahrenheitSensorToTemperatureSensorAdapter(freezingSensor);
        log.Add($"Freezing point: {adaptedFreezing.ReadCelsius():F1}C");

        // Demonstrate polymorphism
        var sensors = new List<ITemperatureSensor> { adapted, adaptedFreezing };
        log.Add($"Total sensors: {sensors.Count}");
        foreach (var s in sensors)
        {
            log.Add($"  {s.GetName()}: {s.ReadCelsius():F1}C");
        }

        return log;
    }
}
