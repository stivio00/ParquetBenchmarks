# Measured results

Full-suite runs from **2026-09-02** on the same machine, so the C# and Python
numbers are directly comparable where the work matches (see the cross-language
notes below). Rerun before quoting anything on other hardware — the ratios
are what transfer.

**Environment:** Apple M4 (10 cores) · Arm64 · macOS Tahoe 26.6.2 ·
.NET 10.0.5 / BenchmarkDotNet 0.15.8 · CPython 3.14.4 / pandas 3.0.5 /
polars 1.44.1 / pyarrow 25.0.1 / duckdb 1.5.5 (native, both suites) /
fastparquet 2026.5.0 · 1,000,000 rows × 10 columns · Snappy · 2 warmups,
5 iterations everywhere.

| File | Contents |
|---|---|
| `dotnet-read.md` | C# ReadBenchmarks (6 methods, per-library files) |
| `dotnet-write.md` | C# WriteBenchmarks (6 methods, incl. EF Core SaveChanges vs BulkInsert) |
| `dotnet-sharedfile.md` | C# SharedFileReadBenchmarks (4 methods, decoding `data/bench-1m.parquet`) |
| `python-suite.txt` | Full Python suite (14 methods, incl. the SharedFile group on the same file) |

## The only valid cross-language comparison

Both suites decode **the same `data/bench-1m.parquet` bytes** through the
**same native DuckDB 1.5.5 engine** and fully materialize 1M row objects:

| Benchmark | Mean | Materializes |
|---|---:|---|
| C# `SharedFile_DuckDb_AdoNet_ReadDecode` | 1,157 ms | 1M C# `BenchRow` POCOs |
| Python `SharedFile_DuckDb_FetchAll_ReadDecode` | 1,075 ms | 1M Python tuples |

**Dead heat.** When the file, the native engine and the work are identical,
the language on top stops mattering — the cost is the scan plus materializing
10M objects, and C# and Python land within a few percent of each other.

The numbers that made it look like "Python is faster than C#" are not the
same job: polars (246 ms) and pyarrow (126 ms) on the shared file stop at
native columnar structures (a Rust DataFrame / Arrow Table). The C# suite has
no "stop at columnar" benchmark — every C# read materializes 1M POCOs, which
is strictly more work. Put the same requirement on Python (`fetchall`) and it
costs 1,075 ms.

One more lesson visible in the data: **row-group layout is a performance
knob.** The shared file is a single 1M-row row group; polars reads its own
multi-row-group file in 43.7 ms but the single-group shared file in 245.6 ms —
parallel readers need multiple row groups to spread work across cores.

## Headline numbers (full tables in the files above)

- **C# reads (per-library files):** ParquetSharp column/Arrow ~0.51×,
  DuckDB ADO 0.58×, Dapper 0.64×, EF Core 0.69× vs the Parquet.Net baseline
  (1,694 ms). Parquet.Net allocates 2.7 GB per million rows, the rest ~1 GB.
- **C# writes:** ParquetSharp Arrow 0.46×, DuckDB 0.52× (62 MB alloc — the
  appender path was leaner this run), ParquetSharp column 0.70×,
  EF Core `BulkInsert` 0.68× at 495 KB allocated, EF Core `SaveChanges`
  **29.6× at 8.6 GB** — the ORM bulk-write cautionary tale.
- **Python:** polars dominates its own file reads (43.7 ms, 0.33×) and writes
  (158 ms, 0.28×); pandas≈pyarrow; fastparquet 1.68×/2.53×; duckdb fetchall
  7.03× (object materialization, not scan cost).

## Reproducing

```bash
# C# (all 16 methods; ~12 min, dominated by DuckDb_EfCore_Write)
dotnet run -c Release -- --filter "*"

# Python (all 14 methods; ~5 min)
cd python && uv sync && uv run python benchmark.py

# Regenerate the shared reference file (deterministic, seed 42)
cd python && uv run python make_reference.py
```
