# .NET Parquet read/write benchmarks

Six libraries and access layers, twelve benchmark methods, one fixed dataset:
1,000,000 rows × 10 columns, BenchmarkDotNet + `[MemoryDiagnoser]` on .NET 10.

| # | Library (pinned version) | API exercised | Read | Write |
|---|---|---|:---:|:---:|
| 1 | Parquet.Net 6.1.0 | `ParquetSerializer` — POCO (de)serialization | ✅ | ✅ |
| 2 | ParquetSharp 24.0.0 | low-level column writer/reader | ✅ | ✅ |
| 3 | ParquetSharp.Arrow 24.0.0 | `RecordBatch` interop with Apache.Arrow | ✅ | ✅ |
| 4 | DuckDB.NET (ADO.NET) | `read_parquet` / `Appender` + `COPY` | ✅ | ✅ |
| 5 | Dapper 2.1.79 | `Query<T>` hydration over DuckDB | ✅ | — |
| 6 | DuckDB.EFCoreProvider 1.24.0 | LINQ / `FromParquet` / `SaveChanges` / `BulkInsert` | ✅ | ✅ (two paths) |

Further docs:

- [docs/libraries.md](docs/libraries.md) — how to use each API for read and write, with pros and cons
- [docs/interpretation.md](docs/interpretation.md) — what the measured numbers actually mean
- [docs/streams-and-s3.md](docs/streams-and-s3.md) — MemoryStream/stream support per library, and reading parquet from S3 (AWS SDK vs DuckDB's native remote reader)

## How the benchmarks are constructed

### Dataset

`DataGenerator.cs` generates a fixed, seed-42 dataset of 1M `BenchRow` records.
The 10 columns span the type range with a deliberate string bias (6 of 10
columns are strings) to stress decode and allocation:

| Column | Type | Content |
|---|---|---|
| `Id` | `long` | 1…N |
| `Name` | `string` | `"Adjective Noun #i"` (short) |
| `Price` | `float` | random, 2 decimals |
| `CreatedAt` | `DateTime` | 2020–2025, minute resolution |
| `CreatedAtText` | `string` | same date as ISO-8601 text |
| `IsActive` | `bool` | random |
| `Category` | `string` | 8 repeating values (low-cardinality, dictionary-encoded) |
| `Rating` | `double` | random, 3 decimals |
| `ExternalId` | `string` | GUID per row (unique, hard to dictionary-encode) |
| `Description` | `string` | 80–250 chars of free text (the "long column") |

### Write benchmarks (`Benchmarks/WriteBenchmarks.cs`)

- The rows are generated **once** in `[GlobalSetup]` and reused by every
  benchmark, so only the write path is measured.
- Each method writes its own parquet file into a temp directory. The measured
  work therefore includes **row → columnar transformation + encoding +
  compression + file output**, because that transformation is part of using
  each API (Arrow builders, the column-array extraction loop in
  `ColumnParquetIO.Write`, EF `AddRange`, the row-by-row `Appender` loop).
- Every library writes with its default codec, which is Snappy across the
  board here. Row-group layout differs per library (each one's default) —
  files are byte-different but row-identical.

### Read benchmarks (`Benchmarks/ReadBenchmarks.cs`)

- Each library's parquet file is written **once** in `[GlobalSetup]`
  (unmeasured); every benchmark then measures pure
  **read + decode + materialization into 1M `BenchRow` POCOs**.
- Full materialization is the point: string allocation dominates, which is
  exactly what you pay in real row-oriented consumption. (If you only need
  aggregates, use DuckDB SQL and never materialize — see
  [docs/streams-and-s3.md](docs/streams-and-s3.md) on pushdown.)
- Each library reads **its own** file. The three DuckDB access layers
  (ADO.NET, Dapper, EF Core) share the file written by DuckDB's `COPY`.
- The DuckDB variants open a fresh in-memory DuckDB per iteration and query
  `read_parquet(...)` directly — no import into a table is measured.

### EF Core specifics (`EfCoreDuckDb.cs`)

- One CLR type can only be mapped one way per EF model, so there are two
  contexts: the **read** context maps `BenchRow` straight onto the parquet
  file via `FromParquet` (queries compile to `read_parquet(...)`); the
  **write** context maps it to a physical `bench` table that is `COPY`'d out
  to parquet afterwards, mirroring the plain ADO.NET write benchmark.
- An in-memory DuckDB only lives as long as its connection, and EF opens and
  closes connections per command — so the benchmarks hold
  `context.Database.OpenConnection()` open for the context's lifetime.
- `DuckDb_EfCore_Write` is the canonical tracked path: `AddRange` +
  `SaveChanges` with `AutoDetectChangesEnabled = false` and the provider's
  `EnableBulkInsertBatching()` (merges inserts into multi-row statements,
  ~10× faster than the default one-statement-per-row behaviour).
- `DuckDb_EfCore_BulkWrite` is the provider's ETL fast path: appender-backed
  `BulkInsert` that bypasses the change tracker entirely.

### Job and measurement configuration

Both classes use
`[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]`
and `[MemoryDiagnoser]`, so each reported number is the mean of 5 measured
iterations after 2 warmups, with allocated bytes and Gen0/1/2 counts. Five
iterations keeps the suite quick — expect a few percent of noise, and much
wider relative error on the slowest method (`DuckDb_EfCore_Write`). The
`Failed to set up high priority (Permission denied)` line is a benign macOS
warning (raising process priority needs elevated rights); it does not affect
results.

## Running it

```bash
dotnet run -c Release                          # everything (~10 min, see below)
dotnet run -c Release -- --filter *Write*      # writes only
dotnet run -c Release -- --filter *ReadDecode* # reads only
dotnet run -c Release -- --filter *EfCore*     # the three EF Core benchmarks
dotnet run -c Release -- --filter *ParquetNet* # a single method
```

BenchmarkDotNet requires Release mode and will refuse to run meaningfully in
Debug. The full suite is dominated by `DuckDb_EfCore_Write` (~35 s per
iteration × 7); everything else is roughly 0.5–2 s per iteration. To
sanity-check quickly, drop `RowCount` to `10_000` in the benchmark classes.

## Why "Apache Arrow" is implemented via ParquetSharp

`Apache.Arrow` is a columnar in-memory format library — it does not read or
write Parquet files by itself. The real Arrow-based Parquet path in .NET goes
through ParquetSharp's `ParquetSharp.Arrow` API, which consumes and produces
`RecordBatch`es of Arrow arrays. `ArrowParquetIO.cs` implements that path,
which is genuinely different code (and has a different allocation profile)
from ParquetSharp's low-level column API in `ColumnParquetIO.cs`.

## Repo notes — two things that were originally broken

1. **ParquetSharp's row-oriented API has a hard 7-element tuple limit.** The
   original code used `ParquetFile.CreateRowWriter<(...10 columns...)>`, which
   throws `ArgumentException` for tuples beyond 7 elements — and because that
   happened in `[GlobalSetup]`, it killed *every* benchmark in the class. The
   ParquetSharp benchmarks now use the low-level column API
   (`ColumnParquetIO.cs`), which is also the idiomatic way to handle wide
   schemas.
2. **`DuckDB.NET.Data` ships no native library.** `DuckDB.NET.Bindings`
   (its dependency) is managed-only, so every DuckDB benchmark died with
   `DllNotFoundException: duckdb`. The fix is a `*.Full` package that bundles
   the native `libduckdb` — currently `Skuirrels.DuckDB.NET.Data.Full`
   (the EF Core provider's pinned re-publish of the same DuckDB.NET 1.5.5
   provider; the plain upstream `DuckDB.NET.Data.Full` would conflict with it).

## A note on `DuckDb_Dapper_ReadDecode`

`Query<T>` on DuckDB.NET/Dapper is always **buffered** — it materializes
fully into a `List<T>` before returning; there is no true streaming variant
that hands back an open `DbDataReader` across an async boundary. At 1M rows
this is fine memory-wise, but it is why `DuckDb_AdoNet_ReadDecode` (raw
`DbDataReader`, forward-only) sits next to it — that one is your streaming
baseline.
