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
| ParquetNet_Write          |  1,139.0 ms |    40.48 ms |  10.51 ms |  1.00 |    0.01 |   22000.0000 |           - |         - |  1163386.7 KB |       1.000 |
| ParquetSharp_Column_Write |    794.7 ms |     6.51 ms |   1.01 ms |  0.70 |    0.01 |   19000.0000 |   1000.0000 |         - |  553726.17 KB |       0.476 |
| ParquetSharp_Arrow_Write  |    519.0 ms |    31.01 ms |   8.05 ms |  0.46 |    0.01 |    1000.0000 |   1000.0000 | 1000.0000 | 1028886.68 KB |       0.884 |
| DuckDb_Write              |    588.0 ms |    19.41 ms |   3.00 ms |  0.52 |    0.00 |    7000.0000 |           - |         - |   62734.97 KB |       0.054 |
| DuckDb_EfCore_Write       | 33,759.7 ms | 2,873.18 ms | 746.16 ms | 29.64 |    0.65 | 1016000.0000 | 274000.0000 | 7000.0000 | 8628507.84 KB |       7.417 |
| DuckDb_EfCore_BulkWrite   |    772.9 ms |    18.93 ms |   2.93 ms |  0.68 |    0.01 |            - |           - |         - |     495.44 KB |       0.000 |
