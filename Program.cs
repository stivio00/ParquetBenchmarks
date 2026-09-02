using BenchmarkDotNet.Running;

// Run all: dotnet run -c Release
// Run a subset, e.g. only writes: dotnet run -c Release -- --filter *Write*
// Run a single method: dotnet run -c Release -- --filter *ParquetNet_ReadDecode*
BenchmarkSwitcher.FromAssemblies(new[] { typeof(Program).Assembly }).Run(args);
