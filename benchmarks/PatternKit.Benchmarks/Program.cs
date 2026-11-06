using BenchmarkDotNet.Running;
using PatternKit.Benchmarks;

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args, PatternKitBenchmarkConfig.Create());
