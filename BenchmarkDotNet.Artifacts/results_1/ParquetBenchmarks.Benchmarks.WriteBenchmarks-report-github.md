```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a
  Job-OIFEUM : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a

Runtime=.NET 10.0  IterationCount=5  WarmupCount=2  

```
| Method                    | Mean       | Error    | StdDev   | Ratio | Gen0       | Gen1      | Gen2      | Allocated  | Alloc Ratio |
|-------------------------- |-----------:|---------:|---------:|------:|-----------:|----------:|----------:|-----------:|------------:|
| ParquetNet_Write          | 1,152.0 ms | 65.39 ms | 10.12 ms |  1.00 | 22000.0000 |         - |         - | 1135.98 MB |        1.00 |
| ParquetSharp_Column_Write |   801.4 ms | 19.29 ms |  2.99 ms |  0.70 | 19000.0000 | 1000.0000 |         - |  540.75 MB |        0.48 |
| ParquetSharp_Arrow_Write  |   539.5 ms | 57.45 ms |  8.89 ms |  0.47 |  1000.0000 | 1000.0000 | 1000.0000 | 1004.77 MB |        0.88 |
| DuckDb_Write              |   788.9 ms | 25.64 ms |  3.97 ms |  0.68 | 97000.0000 |         - |         - |  778.43 MB |        0.69 |
