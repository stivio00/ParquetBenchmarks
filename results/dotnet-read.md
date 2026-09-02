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
| ParquetNet_ReadDecode          | 1,693.9 ms | 323.52 ms | 50.07 ms |  1.00 |    0.04 | 254000.0000 | 134000.0000 | 6000.0000 |    2.7 GB |        1.00 |
| ParquetSharp_Column_ReadDecode |   864.7 ms |  78.48 ms | 20.38 ms |  0.51 |    0.02 | 129000.0000 |  69000.0000 | 5000.0000 |   1.06 GB |        0.39 |
| ParquetSharp_Arrow_ReadDecode  |   870.9 ms |  45.21 ms | 11.74 ms |  0.51 |    0.02 | 134000.0000 |  70000.0000 | 5000.0000 |   1.01 GB |        0.37 |
| DuckDb_AdoNet_ReadDecode       |   974.4 ms | 178.77 ms | 46.43 ms |  0.58 |    0.03 | 134000.0000 |  71000.0000 | 5000.0000 |   1.01 GB |        0.37 |
| DuckDb_Dapper_ReadDecode       | 1,083.1 ms | 117.79 ms | 30.59 ms |  0.64 |    0.02 | 149000.0000 |  77000.0000 | 6000.0000 |   1.13 GB |        0.42 |
| DuckDb_EfCore_ReadDecode       | 1,175.4 ms | 200.69 ms | 52.12 ms |  0.69 |    0.03 | 158000.0000 |  83000.0000 | 6000.0000 |    1.2 GB |        0.44 |
