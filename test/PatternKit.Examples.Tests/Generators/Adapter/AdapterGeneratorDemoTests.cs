using PatternKit.Examples.Generators.Adapter;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Generators.Adapter;

[Feature("Adapter Generator Example")]
public sealed class AdapterGeneratorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Adapter converts 212F to 100C")]
    [Fact]
    public Task Adapter_Converts_Fahrenheit_To_Celsius() =>
        Given("a Fahrenheit sensor reading 212F", () => true)
        .When("adapted to the Celsius interface", _ =>
        {
            var sensor = new AdapterGeneratorDemo.FahrenheitSensor("Test", 212.0);
            AdapterGeneratorDemo.ITemperatureSensor adapted =
                new FahrenheitSensorToTemperatureSensorAdapter(sensor);
            return adapted;
        })
        .Then("it reads 100C", adapted =>
        {
            Assert.Equal(100.0, adapted.ReadCelsius(), precision: 1);
        })
        .AssertPassed();

    [Scenario("Adapter converts 32F to 0C")]
    [Fact]
    public Task Adapter_Converts_Freezing_Point() =>
        Given("a Fahrenheit sensor reading 32F", () => true)
        .When("adapted to the Celsius interface", _ =>
        {
            var sensor = new AdapterGeneratorDemo.FahrenheitSensor("Ice", 32.0);
            AdapterGeneratorDemo.ITemperatureSensor adapted =
                new FahrenheitSensorToTemperatureSensorAdapter(sensor);
            return adapted;
        })
        .Then("it reads 0C", adapted =>
        {
            Assert.Equal(0.0, adapted.ReadCelsius(), precision: 1);
        })
        .AssertPassed();

    [Scenario("Adapter delegates the sensor name")]
    [Fact]
    public Task Adapter_Delegates_Name() =>
        Given("a Fahrenheit sensor named Thermo-X", () => true)
        .When("adapted to the Celsius interface", _ =>
        {
            var sensor = new AdapterGeneratorDemo.FahrenheitSensor("Thermo-X", 72.0);
            AdapterGeneratorDemo.ITemperatureSensor adapted =
                new FahrenheitSensorToTemperatureSensorAdapter(sensor);
            return adapted;
        })
        .Then("the adapted name wraps the original", adapted =>
        {
            Assert.Equal("Adapted(Thermo-X)", adapted.GetName());
        })
        .AssertPassed();

    [Scenario("Demo runs without errors")]
    [Fact]
    public Task Demo_Run_Executes_Without_Errors() =>
        Given("the adapter generator demo", () => true)
        .When("the demo is executed", _ => AdapterGeneratorDemo.Run())
        .Then("it produces expected output", log =>
        {
            Assert.NotEmpty(log);
            Assert.Contains(log, l => l.Contains("100.0"));
            Assert.Contains(log, l => l.Contains("0.0"));
        })
        .AssertPassed();
}
