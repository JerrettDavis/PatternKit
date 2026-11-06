using BenchmarkDotNet.Attributes;
using PatternKit.Behavioral.NullObject;
using PatternKit.Generators.NullObject;

namespace PatternKit.Benchmarks.Behavioral;

[BenchmarkCategory("Behavioral", "NullObject")]
public class NullObjectBenchmarks
{
    private static readonly Notification Notification = new("C-100", "Statement ready");

    [Benchmark(Baseline = true, Description = "Fluent: create null object")]
    [BenchmarkCategory("Fluent", "Construction")]
    public NullObject<INullNotificationChannel> Fluent_CreateNullObject()
        => NullObject<INullNotificationChannel>
            .Create(NullNotificationChannel.Instance)
            .Build();

    [Benchmark(Description = "Generated: get null object instance")]
    [BenchmarkCategory("Generated", "Construction")]
    public INullNotificationChannel Generated_GetInstance()
        => NullNotificationChannel.Instance;

    [Benchmark(Description = "Fluent: invoke null object")]
    [BenchmarkCategory("Fluent", "Execution")]
    public string Fluent_Invoke()
        => Fluent_CreateNullObject().Instance.Send(Notification);

    [Benchmark(Description = "Generated: invoke null object")]
    [BenchmarkCategory("Generated", "Execution")]
    public string Generated_Invoke()
        => NullNotificationChannel.Instance.Send(Notification);
}

public sealed record Notification(string CustomerId, string Subject);

[GenerateNullObject(TypeName = "NullNotificationChannel")]
public interface INullNotificationChannel
{
    [NullObjectDefault("suppressed")]
    string Send(Notification notification);
}
