```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a
  Job-OIFEUM : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a

Runtime=.NET 10.0  IterationCount=5  WarmupCount=2  

```
| Method                         | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0        | Gen1        | Gen2      | Allocated | Alloc Ratio |
|------------------------------- |-----------:|----------:|---------:|------:|--------:|------------:|------------:|----------:|----------:|------------:|
| ParquetNet_ReadDecode          | 1,634.4 ms | 133.20 ms | 20.61 ms |  1.00 |    0.02 | 254000.0000 | 134000.0000 | 6000.0000 |    2.7 GB |        1.00 |
| ParquetSharp_Column_ReadDecode |   833.8 ms |  49.92 ms | 12.96 ms |  0.51 |    0.01 | 129000.0000 |  68000.0000 | 5000.0000 |   1.06 GB |        0.39 |
| ParquetSharp_Arrow_ReadDecode  |   842.4 ms |  46.70 ms | 12.13 ms |  0.52 |    0.01 | 134000.0000 |  70000.0000 | 5000.0000 |   1.01 GB |        0.37 |
| DuckDb_AdoNet_ReadDecode       |   892.0 ms | 130.14 ms | 20.14 ms |  0.55 |    0.01 | 134000.0000 |  71000.0000 | 5000.0000 |   1.01 GB |        0.37 |
| DuckDb_Dapper_ReadDecode       | 1,015.4 ms | 107.19 ms | 27.84 ms |  0.62 |    0.02 | 149000.0000 |  77000.0000 | 6000.0000 |   1.13 GB |        0.42 |
| DuckDb_EfCore_ReadDecode       | 1,078.1 ms | 481.69 ms | 74.54 ms |  0.66 |    0.04 | 158000.0000 |  83000.0000 | 6000.0000 |    1.2 GB |        0.44 |
