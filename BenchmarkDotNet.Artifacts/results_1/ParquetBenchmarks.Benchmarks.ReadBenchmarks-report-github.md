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
| ParquetNet_ReadDecode          | 1,738.0 ms | 127.52 ms | 33.12 ms |  1.00 |    0.02 | 254000.0000 | 133000.0000 | 6000.0000 |    2.7 GB |        1.00 |
| ParquetSharp_Column_ReadDecode |   884.4 ms |  20.74 ms |  5.39 ms |  0.51 |    0.01 | 129000.0000 |  69000.0000 | 5000.0000 |   1.06 GB |        0.39 |
| ParquetSharp_Arrow_ReadDecode  |   890.0 ms |  71.85 ms | 18.66 ms |  0.51 |    0.01 | 134000.0000 |  70000.0000 | 5000.0000 |   1.01 GB |        0.37 |
| DuckDb_AdoNet_ReadDecode       |   952.6 ms |  44.98 ms | 11.68 ms |  0.55 |    0.01 | 134000.0000 |  71000.0000 | 5000.0000 |   1.01 GB |        0.37 |
| DuckDb_Dapper_ReadDecode       | 1,101.7 ms | 186.38 ms | 48.40 ms |  0.63 |    0.03 | 149000.0000 |  77000.0000 | 6000.0000 |   1.13 GB |        0.42 |
