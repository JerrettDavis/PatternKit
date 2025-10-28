namespace PatternKit.Examples.Tests.ProxyDemo;

// Collection to ensure tests that redirect Console output do not run in parallel.
[CollectionDefinition("ConsoleOutput", DisableParallelization = true)]
public sealed class ConsoleOutputCollection { }

