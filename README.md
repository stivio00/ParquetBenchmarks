# Parquet library benchmark: Parquet.Net vs ParquetSharp (row) vs ParquetSharp/Arrow vs DuckDB (ADO.NET) vs DuckDB+Dapper

## What this measures

- **WriteBenchmarks**: writes the same 1,000,000-row / 10-column dataset to a Parquet file using four different write paths.
- **ReadBenchmarks**: pre-writes each format's file once in `[GlobalSetup]` (not measured), then measures only the read + decode-into-`BenchRow[]` cost for five paths (DuckDB gets both a raw ADO.NET and a Dapper variant, since that's where the ergonomics/allocation difference actually shows up — a bulk loader has no equivalent "ORM" step on write).
- `[MemoryDiagnoser]` is enabled on both, so the BenchmarkDotNet report includes allocated bytes and Gen0/1/2 collections alongside timing.

Row schema (10 columns, matching the requested type spread): `Id` (long), `Name` (string), `Price` (float), `CreatedAt` (date), `CreatedAtText` (date-as-string), `IsActive` (bool), `Category` (enum-like string), `Rating` (double), `ExternalId` (GUID-as-string), `Description` (long free-text string, 80–250 chars, to stress string-heavy decoding).

## Why "Apache Arrow" is implemented via ParquetSharp

`Apache.Arrow` is a columnar in-memory format library — it doesn't read/write Parquet files by itself. The real Arrow-based Parquet path in .NET goes through ParquetSharp's `ParquetSharp.Arrow` API, which builds/consumes `RecordBatch`es of Arrow arrays. `ArrowParquetIO.cs` implements that path (column-oriented builders → `RecordBatch` → Parquet file, and the reverse on read), which is genuinely different code (and a different allocation profile) than ParquetSharp's row-oriented tuple API.

## Running it

```bash
dotnet run -c Release
```

BenchmarkDotNet requires Release mode and will refuse to run meaningfully in Debug. To run a subset instead of the full ~9-method suite:

```bash
dotnet run -c Release -- --filter *Write*
dotnet run -c Release -- --filter *ReadDecode*
dotnet run -c Release -- --filter *ParquetNet*
```

## Before your first run — things likely to need adjustment

I don't have NuGet/network access in the environment that generated this project, so **none of this has been compiled or run**. Two areas are the most likely to need a small fix against whatever exact package versions you restore:

1. **`ParquetSharp.RowOriented.ParquetFile.CreateRowWriter<T>` / `CreateRowReader<T>`** — the row-oriented tuple API's exact overload signatures have shifted across ParquetSharp releases. If it doesn't compile as-is, check the installed version's docs for the current `CreateRowWriter`/`CreateRowReader` signature and column-name-array placement.
2. **`ParquetSharp.Arrow.FileWriter` / `FileReader`** in `ArrowParquetIO.cs` — same caveat; the Arrow interop surface is the newest/least stable part of ParquetSharp's API.

Everything else (Parquet.Net's `ParquetSerializer`, DuckDB.NET's `Appender`/`DbDataReader`, and Dapper's `Query<T>`) is on more stable, longer-lived API surface and should compile cleanly against the pinned versions.

## Expect a long first run

1,000,000 rows × 5 iterations × 2 warmup runs × 9 benchmark methods adds up — expect the full suite to take a while (likely double-digit minutes, dominated by the write benchmarks and DuckDB's row-by-row Appender loop). To sanity-check the project compiles and runs before committing to the full suite, temporarily drop `RowCount` to something like `10_000` in both benchmark classes, or pass `--filter` to run one method at a time.

## A note on `DuckDb_Dapper_ReadDecode`

`Query<T>` on DuckDB.NET/Dapper is always **buffered** — it materializes fully into a `List<T>` before returning, there's no true streaming `QueryAsync` variant (Dapper can't safely hand back an open `DbDataReader` across an async boundary). At 1M rows this is still fine memory-wise, but it's why `DuckDb_AdoNet_ReadDecode` (raw `DbDataReader`, forward-only) is included side by side — that's your streaming baseline if you ever need to confirm Dapper isn't costing you extra retained memory at this row count.
