# Python parquet benchmarks (uv project)

The same benchmark design as the C# suite in the repo root, implemented
against the Python ecosystem. Five engines, ten benchmark methods
(5 write + 5 read), one fixed dataset: 1,000,000 rows × 10 columns, Snappy
compression pinned for every writer.

| # | Engine | What it is | Read | Write |
|---|---|---|:---:|:---:|
| 1 | pandas + pyarrow engine | DataFrame ⇄ parquet via Arrow | ✅ | ✅ |
| 2 | pandas + fastparquet engine | DataFrame ⇄ parquet via fastparquet/numba | ✅ | ✅ |
| 3 | polars | Rust DataFrames, native parquet | ✅ | ✅ |
| 4 | pyarrow | Arrow C++ `ParquetFile`/`ParquetWriter` directly | ✅ | ✅ |
| 5 | duckdb | SQL engine, `read_parquet` + `COPY` | ✅ | ✅ |

## Running

Requires [uv](https://docs.astral.sh/uv/). From this folder:

```bash
uv sync                        # create .venv and install locked deps
uv run python benchmark.py     # full suite: 1M rows, 2 warmups, 5 iterations
uv run python benchmark.py --quick          # smoke run: 100k rows
uv run python benchmark.py --filter polars  # substring filter on names
uv run python benchmark.py --list           # list benchmark names
uv run python make_reference.py             # regenerate data/bench-1m.parquet
```

Options: `--rows`, `--warmup`, `--iterations`, `--seed`, `--filter`,
`--quick`, `--list`. Expect a few minutes for the full suite at 1M rows
(dataset generation alone is a pure-Python loop).

## The shared reference file (cross-language comparison)

`make_reference.py` writes `../data/bench-1m.parquet` — the canonical 1M-row
dataset, checked into the repo so this suite and the C# suite
(`SharedFileReadBenchmarks`) decode **identical bytes**. When the file is
present, the `SharedFile_*` benchmarks run automatically against it. Its
schema is chosen for maximum compatibility: string columns are OPTIONAL
(Parquet.Net's POCO reader rejects required string columns), value columns
are REQUIRED, timestamps are micros. The matched cross-language pair —
same file, same native DuckDB engine, same full row materialization — is
C# `SharedFile_DuckDb_AdoNet_ReadDecode` (1,157 ms into POCOs) vs Python
`SharedFile_DuckDb_FetchAll_ReadDecode` (1,075 ms into tuples): a dead heat.

## How it's constructed (and where it deliberately differs from C#)

- **Dataset** (`generate_dataset`): mirrors the C# `DataGenerator` — same
  columns, pools, ranges and string patterns (see the root README's table).
  The RNG differs (C# `Random` vs Python's Mersenne Twister), so the data is
  shape-identical, not byte-identical, to the C# run — cross-language
  comparisons are about engine/API cost, not identical bytes.
- **Canonical form**: the dataset is generated once as a pandas DataFrame
  (Python's lingua franca); the polars DataFrame and Arrow Table are derived
  once in setup, unmeasured, via near-zero-copy conversions. Each writer then
  starts from its native representation. Note that pandas itself still pays
  DataFrame→Arrow conversion inside `to_parquet` — that is its real cost.
- **Write benchmarks** write to per-engine files in a temp dir; measured work
  = encode + compress + file output. `DuckDb_Write` mirrors the C# version:
  register source → `CREATE TABLE bench AS SELECT ...` → `COPY ... TO
  (FORMAT parquet)`.
- **Read benchmarks**: each engine's file is written once in setup
  (unmeasured); each benchmark measures full read + decode into that engine's
  natural materialization — DataFrame (pandas/polars), Table (pyarrow), or a
  list of 1M Python tuples (duckdb `fetchall()`, the closest mirror of the C#
  ADO.NET row-by-row materialization).
- **Timing**: `time.perf_counter` around each iteration, `gc.collect()`
  between iterations (outside the timed region), mean/min/max/stddev and a
  ratio vs the pandas+pyarrow baseline (the "everyman" library, same role as
  Parquet.Net in the C# suite). There is no `[MemoryDiagnoser]` equivalent:
  the fast engines do their allocating in native code, which Python-side
  memory profiling cannot see — so this suite reports timing only.

## What to expect

The same qualitative story as the C# suite, with the roles shifted by
ecosystem: the native engines (polars, pyarrow) at the front, pandas close
behind via its pyarrow engine, fastparquet slower for this string-heavy
profile, and duckdb's `fetchall()` paying full price for materializing 1M
Python tuples — while being untouchable the moment the query is
filter/aggregate shaped, because nothing needs to be materialized at all:

```python
con = duckdb.connect()
con.execute("SELECT Category, avg(Rating) FROM read_parquet('polars.parquet') GROUP BY Category").df()
```

## Sample results

Full 1M-row suite on the same Apple M4 / .NET machine as the C# results,
Python 3.14, pandas 3.0.5, polars 1.44.1, pyarrow 25.0.1, duckdb 1.5.5,
fastparquet 2026.5.0 (2 warmups, 5 iterations). The verbatim output of this
run is committed at [../results/python-suite.txt](../results/python-suite.txt);
the C# counterpart lives in [../results/](../results/). Re-run before
quoting; ratios are what transfer.

Write 1,000,000 rows (each engine's own file):

| Benchmark | Mean | Ratio |
|---|---:|---:|
| `Pandas_PyArrow_Write` (baseline) | 571 ms | 1.00× |
| `Pandas_Fastparquet_Write` | 962 ms | 1.68× |
| `Polars_Write` | **158 ms** | **0.28×** |
| `PyArrow_Write` | 564 ms | 0.99× |
| `DuckDb_Write` | 334 ms | 0.58× |

Read + decode (each engine's own file):

| Benchmark | Mean | Ratio |
|---|---:|---:|
| `Pandas_PyArrow_ReadDecode` (baseline) | 133 ms | 1.00× |
| `Pandas_Fastparquet_ReadDecode` | 335 ms | 2.53× |
| `Polars_ReadDecode` | **44 ms** | **0.33×** |
| `PyArrow_ReadDecode` | 128 ms | 0.96× |
| `DuckDb_FetchAll_ReadDecode` | 933 ms | 7.03× |

Read + decode the shared reference file (single 1M-row row group):

| Benchmark | Mean | Ratio |
|---|---:|---:|
| `SharedFile_Polars_ReadDecode` | 246 ms | 1.88× |
| `SharedFile_PyArrow_ReadDecode` | 126 ms | 0.97× |
| `SharedFile_Pandas_ReadDecode` (baseline) | 131 ms | 1.00× |
| `SharedFile_DuckDb_FetchAll_ReadDecode` | 1,075 ms | 8.21× |

Reading of these numbers:

- **Polars is the outlier on its own files** — ~3× faster than everything
  else in both directions. Its multithreaded Rust reader/writer and string
  handling are simply a different class. But note the shared-file result:
  the reference file is one big row group, and polars drops to 246 ms there
  (vs 44 ms on its own multi-row-group file) — parallel readers need
  multiple row groups to use more than one core.
- **pandas ≈ pyarrow on read** (133 vs 128 ms): the Arrow→DataFrame
  conversion is nearly free next to decode. On write, pandas' df→Arrow
  conversion is likewise not the bottleneck — string encoding dominates.
- **fastparquet is ~1.7× slower to write and ~2.5× slower to read** than
  the pyarrow engine on this string-heavy profile. Its niche (no Arrow
  dependency) isn't performance at this scale.
- **duckdb's `fetchall()` costs 7×** — that is the price of turning 1M
  rows × 10 columns into 10M Python objects, not the price of reading
  parquet. The matched C# comparison (same file, same engine, 1M POCOs)
  lands at 1,157 ms vs 1,075 ms here: **a dead heat, not "Python is
  faster"** — see the shared-file section above.
- **Don't compare across languages except where the materialization
  matches** (as above). Polars' 44 ms reads land in a polars DataFrame
  with its own string pool; C# reads land in 1M heap-allocated POCO
  objects — different work, different runtimes, different GCs. Compare
  engines within a suite; compare suites only on shape.

## Reading parquet directly from S3 with duckdb (httpfs)

DuckDB can read *and write* parquet on S3/GCS/Azure natively via its
`httpfs` extension — no download step, no .NET/Python I/O code, with
projection and filter pushdown into the object store (only the needed byte
ranges are fetched, row groups can be skipped via zonemaps):

```python
import duckdb

con = duckdb.connect()
con.execute("INSTALL httpfs")
con.execute("LOAD httpfs")

# credentials: explicit, ambient AWS chain, or a presigned URL
con.execute("CREATE SECRET (TYPE S3, KEY_ID 'AKIA...', SECRET '...', REGION 'eu-west-1')")
con.execute("CREATE SECRET (TYPE S3, PROVIDER credential_chain)")          # env/profile/instance role

df = con.execute("""
    SELECT Category, avg(Rating)
    FROM read_parquet('s3://bucket/data/*.parquet', hive_partitioning = true)
    WHERE CreatedAt >= TIMESTAMP '2025-01-01'
    GROUP BY Category
""").df()

con.execute("COPY (SELECT * FROM bench) TO 's3://bucket/out.parquet' (FORMAT parquet)")
```

The managed libraries can also reach S3, each in its own way — polars
`scan_parquet("s3://...")` (with `storage_options` for credentials) and
pyarrow `pq.read_table("s3://...", filesystem=S3FileSystem)` — but none of
them push filters down into the object store the way duckdb does. The full
comparison (AWS SDK vs native duckdb reader, including a seekable range-GET
stream for ParquetSharp-style partial reads) is in
[../docs/streams-and-s3.md](../docs/streams-and-s3.md); the C# suite's
docs/interpretation.md covers the methodology caveats that apply here too.

## Reading from in-memory bytes (no file)?

- pandas / pyarrow / polars / fastparquet: all accept Python file-like
  objects (`BytesIO`) for **both** reads and writes — e.g.
  `pq.read_table(buffer)`, `pl.read_parquet(buffer)`,
  `pd.read_parquet(buffer)`.
- duckdb: same as the C# suite — path/URL only. Buffer the bytes to a temp
  file, or point `read_parquet` straight at `s3://` / `https://` via httpfs.
