```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a
  Job-OIFEUM : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a

Runtime=.NET 10.0  IterationCount=5  WarmupCount=2  

```
| Method                    | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0         | Gen1        | Gen2      | Allocated     | Alloc Ratio |
|-------------------------- |------------:|------------:|----------:|------:|--------:|-------------:|------------:|----------:|--------------:|------------:|
| ParquetNet_Write          |  1,183.8 ms |    86.84 ms |  13.44 ms |  1.00 |    0.01 |   22000.0000 |           - |         - | 1163114.66 KB |       1.000 |
| ParquetSharp_Column_Write |    807.9 ms |    10.26 ms |   1.59 ms |  0.68 |    0.01 |   19000.0000 |   1000.0000 |         - |  553726.17 KB |       0.476 |
| ParquetSharp_Arrow_Write  |    545.1 ms |    57.52 ms |  14.94 ms |  0.46 |    0.01 |    1000.0000 |   1000.0000 | 1000.0000 | 1028886.68 KB |       0.885 |
| DuckDb_Write              |    591.3 ms |    41.28 ms |   6.39 ms |  0.50 |    0.01 |    7000.0000 |           - |         - |   62734.97 KB |       0.054 |
| DuckDb_EfCore_Write       | 32,720.7 ms | 3,086.04 ms | 801.43 ms | 27.64 |    0.68 | 1011000.0000 | 276000.0000 | 2000.0000 | 8628485.67 KB |       7.418 |
| DuckDb_EfCore_BulkWrite   |    792.8 ms |   101.80 ms |  26.44 ms |  0.67 |    0.02 |            - |           - |         - |     495.44 KB |       0.000 |
