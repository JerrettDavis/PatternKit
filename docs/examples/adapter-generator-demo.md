# Adapter Generator Demo

## Goal

Show how the `[GenerateAdapter]` source generator creates an object adapter that bridges a legacy Fahrenheit temperature sensor to a modern Celsius-based interface.

## Key Idea

The `[GenerateAdapter]` attribute on a static partial host class, combined with `[AdapterMap]` on mapping methods, generates a sealed adapter class. The adapter stores a reference to the adaptee and forwards each target interface method through the corresponding mapping method.

## Code Snippet

```csharp
public interface ITemperatureSensor
{
    double ReadCelsius();
    string GetName();
}

public class FahrenheitSensor
{
    public double GetTemperatureF() => 212.0;
    public string ModelName => "Thermo-2000";
}

[GenerateAdapter(Target = typeof(ITemperatureSensor), Adaptee = typeof(FahrenheitSensor))]
public static partial class SensorAdapterHost
{
    [AdapterMap(TargetMember = "ReadCelsius")]
    public static double ReadCelsius(FahrenheitSensor adaptee)
        => (adaptee.GetTemperatureF() - 32.0) * 5.0 / 9.0;

    [AdapterMap(TargetMember = "GetName")]
    public static string GetName(FahrenheitSensor adaptee)
        => $"Adapted({adaptee.ModelName})";
}

// Usage:
var legacy = new FahrenheitSensor("Thermo-2000", 212.0);
ITemperatureSensor sensor = new FahrenheitSensorToTemperatureSensorAdapter(legacy);
Console.WriteLine($"{sensor.GetName()}: {sensor.ReadCelsius():F1}C");
// Output: Adapted(Thermo-2000): 100.0C
```

## Mental Model

```
ITemperatureSensor.ReadCelsius()
       |
       v
FahrenheitSensorToTemperatureSensorAdapter
       |
       +-- _adaptee (FahrenheitSensor)
       |
       v
SensorAdapterHost.ReadCelsius(_adaptee)
       |
       v
(fahrenheit - 32) * 5/9  -->  Celsius result
```

The generated adapter is a thin shell: it stores the adaptee, and each interface method calls the corresponding static mapping method with `_adaptee` as the first argument.

## Test References

- `AdapterGeneratorDemoTests.Adapter_Converts_Fahrenheit_To_Celsius`
- `AdapterGeneratorDemoTests.Adapter_Converts_Freezing_Point`
- `AdapterGeneratorDemoTests.Adapter_Delegates_Name`
- `AdapterGeneratorDemoTests.Demo_Run_Executes_Without_Errors`
