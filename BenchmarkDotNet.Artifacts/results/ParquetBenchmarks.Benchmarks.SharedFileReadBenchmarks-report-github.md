```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a
  Job-OIFEUM : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a

Runtime=.NET 10.0  IterationCount=5  WarmupCount=2  

```
| Method                                    | Mean       | Error    | StdDev   | Ratio | Gen0        | Gen1        | Gen2      | Allocated | Alloc Ratio |
|------------------------------------------ |-----------:|---------:|---------:|------:|------------:|------------:|----------:|----------:|------------:|
| SharedFile_ParquetNet_ReadDecode          | 1,403.0 ms | 27.35 ms |  4.23 ms |  1.00 | 250000.0000 | 133000.0000 | 5000.0000 |   2.62 GB |        1.00 |
| SharedFile_ParquetSharp_Column_ReadDecode |   880.1 ms | 37.59 ms |  5.82 ms |  0.63 | 130000.0000 |  70000.0000 | 6000.0000 |   1.06 GB |        0.40 |
| SharedFile_ParquetSharp_Arrow_ReadDecode  |   953.4 ms | 66.19 ms | 10.24 ms |  0.68 | 135000.0000 |  72000.0000 | 6000.0000 |   1.01 GB |        0.39 |
| SharedFile_DuckDb_AdoNet_ReadDecode       | 1,091.0 ms | 73.27 ms | 11.34 ms |  0.78 | 135000.0000 |  72000.0000 | 6000.0000 |   1.01 GB |        0.39 |
