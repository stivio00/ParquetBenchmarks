```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a
  Job-OIFEUM : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a

Runtime=.NET 10.0  IterationCount=5  WarmupCount=2  

```
| Method                                    | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0        | Gen1        | Gen2      | Allocated | Alloc Ratio |
|------------------------------------------ |-----------:|----------:|---------:|------:|--------:|------------:|------------:|----------:|----------:|------------:|
| SharedFile_ParquetNet_ReadDecode          | 1,494.0 ms |  15.59 ms |  2.41 ms |  1.00 |    0.00 | 250000.0000 | 131000.0000 | 5000.0000 |   2.63 GB |        1.00 |
| SharedFile_ParquetSharp_Column_ReadDecode |   925.7 ms | 129.71 ms | 20.07 ms |  0.62 |    0.01 | 130000.0000 |  69000.0000 | 6000.0000 |   1.06 GB |        0.40 |
| SharedFile_ParquetSharp_Arrow_ReadDecode  | 1,007.4 ms |  94.39 ms | 24.51 ms |  0.67 |    0.02 | 135000.0000 |  72000.0000 | 6000.0000 |   1.01 GB |        0.39 |
| SharedFile_DuckDb_AdoNet_ReadDecode       | 1,157.4 ms |  67.33 ms | 17.49 ms |  0.77 |    0.01 | 135000.0000 |  72000.0000 | 6000.0000 |   1.01 GB |        0.39 |
