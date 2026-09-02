# Library guide: read & write with each API

A cheat sheet for the six access layers benchmarked here — minimal working
code for both directions, then the trade-offs. All snippets match the pinned
versions in `ParquetBenchmarks.csproj` and the full implementations in this
repo.

| Library | Abstraction level | Native binary | Any .NET Stream | SQL pushdown |
|---|---|---|:---:|:---:|
| Parquet.Net (`ParquetSerializer`) | POCO | none (pure managed) | ✅ | — |
| ParquetSharp column API | typed column batches | `ParquetSharpNative` | ✅ | — |
| ParquetSharp.Arrow | Arrow `RecordBatch` | `ParquetSharpNative` | ✅ | — |
| DuckDB ADO.NET | SQL engine | `libduckdb` | ❌ paths only | ✅ |
| Dapper + DuckDB | SQL + reflection hydration | `libduckdb` | ❌ paths only | ✅ |
| EF Core + DuckDB | LINQ / ORM | `libduckdb` | ❌ paths only | ✅ |

---

## 1. Parquet.Net — `ParquetSerializer`

**Write** (`ParquetSerializer.SerializeAsync`):

```csharp
await using var fs = File.Create(path);
await ParquetSerializer.SerializeAsync(rows, fs);   // rows : BenchRow[]
```

**Read** (`ParquetSerializer.DeserializeAsync`):

```csharp
await using var fs = File.OpenRead(path);
var result = await ParquetSerializer.DeserializeAsync<BenchRow>(fs);
List<BenchRow> rows = result.Data;
```

There is also a lower-level `ParquetReader.CreateAsync(stream)` /
`OpenRowGroupReader(i)` API when you need column-level access without the
POCO mapping.

**Pros**

- Pure managed — no native binaries to ship, runs anywhere .NET runs
  (containers, Alpine, lambda-style environments) with zero friction.
- Genuinely async, works on any (seekable) `Stream`: `MemoryStream`,
  `FileStream`, network-backed streams.
- One line per direction; reflection-based POCO mapping; no schema to
  maintain by hand.

**Cons**

- The slowest and most allocation-heavy path measured here (~2× the time,
  ~2.5× the bytes of the native readers at 1M rows).
- Less control: row-group layout, encodings and column types are mostly
  decided for you.
- Reflection cost per row type (first use also pays schema-build).

---

## 2. ParquetSharp — low-level column API

**Write** (`ParquetFileWriter` + logical column writers; full version in
`ColumnParquetIO.cs`):

```csharp
var columns = new Column[]
{
    new Column<long>("Id"),
    new Column<string>("Name"),
    // ... 8 more
};

using var writer = new ParquetFileWriter(path, columns);   // Snappy by default
using var rowGroup = writer.AppendRowGroup();

using (var c = rowGroup.NextColumn().LogicalWriter<long>())   c.WriteBatch(ids);
using (var c = rowGroup.NextColumn().LogicalWriter<string>()) c.WriteBatch(names);
// ... one batch per column, in schema order

writer.Close();   // explicit Close is the recommended pattern
```

**Read** (`ParquetFileReader` + logical column readers):

```csharp
using var reader = new ParquetFileReader(path);
for (int rg = 0; rg < reader.FileMetaData.NumRowGroups; rg++)
{
    using var rowGroup = reader.RowGroup(rg);
    int numRows = (int)rowGroup.MetaData.NumRows;

    long[]   ids   = rowGroup.Column(0).LogicalReader<long>().ReadAll(numRows);
    string[] names = rowGroup.Column(1).LogicalReader<string>().ReadAll(numRows);
    // ... or ReadBatch(Span<T>) to fill reused buffers
}
```

**Pros**

- The fastest pure decode path in the suite (~2× Parquet.Net) with the
  leanest write allocations of the non-EF-bulk paths.
- Explicit schema control (types, logical types, nullability, compression),
  batch `Span`-based read/write into buffers you own.
- Stream-capable in both directions (see
  [streams-and-s3.md](streams-and-s3.md)) and Arrow interop via the same
  package.

**Cons**

- Verbose: you do the row ↔ column transformation yourself, per column, in
  schema order (that loop is part of what the write benchmark measures).
- Ships a native binary per platform (`ParquetSharpNative`).
- The nicer-looking row-oriented tuple API exists but caps at **7-element
  tuples** — unusable for wide schemas like this repo's 10-column model.

---

## 3. ParquetSharp.Arrow — `RecordBatch` interop

**Write** (Apache.Arrow builders → `RecordBatch` → `FileWriter`; full version
in `ArrowParquetIO.cs`):

```csharp
var schema = new Schema.Builder()
    .Field(f => f.Name("Id").DataType(Int64Type.Default).Nullable(false))
    // ... one Field per column
    .Build();

var idBuilder = new Int64Array.Builder();
var nameBuilder = new StringArray.Builder();
// ... append per row, then Build()

var batch = new RecordBatch(schema, new IArrowArray[] { idBuilder.Build(), /* ... */ }, rowCount);

using var writer = new ParquetSharp.Arrow.FileWriter(path, schema);
writer.WriteRecordBatch(batch);
writer.Close();
```

**Read** (via `GetRecordBatchReader`, then typed Arrow arrays):

```csharp
using var reader = new ParquetSharp.Arrow.FileReader(path);
for (int rg = 0; rg < reader.NumRowGroups; rg++)
using (var batchReader = reader.GetRecordBatchReader(new[] { rg }))
while (await batchReader.ReadNextRecordBatchAsync() is { } batch)
{
    using var _ = batch;
    var ids = (Int64Array)batch.Column("Id");
    var names = (StringArray)batch.Column("Name");
    // ids.GetValue(i), names.GetString(i), ...
}
```

**Pros**

- The fastest write path measured (~0.5× Parquet.Net's time).
- Natural fit for analytics: Arrow arrays are the lingua franca of columnar
  .NET data processing, and batches can flow to/from other Arrow systems with
  little copying.
- Stream-capable in both directions.

**Cons**

- Highest abstraction cost: three concepts (schema, builders, batches) before
  any data moves; the most code of the six for row-shaped input/output.
- Builds the whole batch in managed memory first → ~1 GB transient
  allocations at 1M rows (visible in `[MemoryDiagnoser]`).
- Native binary per platform, same as ParquetSharp.

---

## 4. DuckDB via ADO.NET

**Write** (appender → `COPY` out to parquet; see `WriteBenchmarks.DuckDb_Write`):

```csharp
using var connection = new DuckDBConnection("DataSource=:memory:");
connection.Open();

using (var create = connection.CreateCommand())
{
    create.CommandText = "CREATE TABLE bench (Id BIGINT, Name VARCHAR, /* ... */);";
    create.ExecuteNonQuery();
}

using (var appender = connection.CreateAppender("bench"))
    foreach (var r in rows)
        appender.CreateRow()
            .AppendValue(r.Id).AppendValue(r.Name) /* ... */ .EndRow();

using var copy = connection.CreateCommand();
copy.CommandText = $"COPY bench TO '{path}' (FORMAT parquet);";   // Snappy default
copy.ExecuteNonQuery();
```

**Read** (`read_parquet` through a plain `DbDataReader`):

```csharp
using var connection = new DuckDBConnection("DataSource=:memory:");
connection.Open();

using var cmd = connection.CreateCommand();
cmd.CommandText = $"SELECT * FROM read_parquet('{path}');";
using var reader = cmd.ExecuteReader();
while (reader.Read()) { /* reader.GetInt64(0), reader.GetString(1), ... */ }
```

**Pros**

- It's a SQL engine: projection and filter pushdown into the parquet scan
  (with zonemap pruning), aggregates without materializing rows, glob
  patterns (`'data/*.parquet'`), and CSV/JSON with the same ergonomics.
- `read_parquet` needs no import step, works on files and (with `httpfs`)
  remote S3/GCS/Azure URLs.
- Forward-only `DbDataReader` is a true streaming read baseline.

**Cons**

- Writing parquet is two-step: land rows in a table (appender or `INSERT`),
  then `COPY` out — there's no direct "write this row sequence to a parquet
  stream" API.
- Row-by-row materialization through ADO.NET has a per-row cost; boxing in
  `AppendValue(object)` shows up in the allocations.
- Path-based only: no .NET `Stream` input, so in-memory bytes must be landed
  in a temp file first (see [streams-and-s3.md](streams-and-s3.md)).
- Native `libduckdb` binary (big-ish), and you must pick a `*.Full` package
  to actually get it.

---

## 5. Dapper over DuckDB

**Read** (same SQL, reflection-based hydration):

```csharp
using var connection = new DuckDBConnection("DataSource=:memory:");
connection.Open();

var rows = connection.Query<BenchRow>(
    "SELECT Id, Name, Price, CreatedAt, CreatedAtText, IsActive, Category, Rating, ExternalId, Description " +
    "FROM read_parquet(@path);",
    new { path });
```

**Write**: not benchmarked — once you're in Dapper you'd use the same
`DuckDBConnection` for the appender/`COPY` steps as #4, so a separate write
variant would measure nothing new.

**Pros**

- One line from SQL to typed POCOs; near-zero setup; the least ceremony of
  any path here.
- Overhead over raw ADO.NET is modest (~15% time at 1M rows in this suite).
- Parameters, multi-mapping, etc. — all the usual Dapper ergonomics apply.

**Cons**

- Always buffered on DuckDB.NET: `Query<T>` fully materializes a `List<T>`
  before returning — no streaming `QueryAsync` over an open reader.
- Reflection per row (cached, but not free) — visible in the allocation delta
  vs raw ADO.NET.
- You still write SQL by hand; no compile-time checking of columns/types.

---

## 6. EF Core via `DuckDB.EFCoreProvider`

**Read** (map an entity directly onto a parquet file — no table needed):

```csharp
public class BenchEfReadContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder mb)
        => mb.Entity<BenchRow>().FromParquet("/data/bench.parquet");   // or a glob
}

var rows = context.Bench.AsNoTracking().ToList();
// LINQ composes: context.Bench.Where(b => b.Category == "Books").Select(...)
```

**Write, tracked path** (`SaveChanges` — the canonical EF experience):

```csharp
var options = new DbContextOptionsBuilder<BenchEfWriteContext>()
    .UseDuckDB("Data Source=:memory:", d => d.EnableBulkInsertBatching())
    .Options;

using var context = new BenchEfWriteContext(options);
context.Database.OpenConnection();        // keep the in-memory DB alive
context.Database.EnsureCreated();
context.ChangeTracker.AutoDetectChangesEnabled = false;

context.Bench.AddRange(rows);
context.SaveChanges();

context.Database.ExecuteSql($"COPY bench TO {outPath} (FORMAT parquet)");
```

**Write, bulk path** (appender-backed, bypasses the change tracker):

```csharp
context.BulkInsert(rows);                 // ~appender speed, near-zero managed alloc
```

**Pros**

- Type-safe LINQ with SQL translation and pushdown; `FromParquet` gives you
  "query the file" with zero boilerplate, globs included.
- Change tracking, transactions, optimistic concurrency, migrations when you
  *want* ORM semantics.
- `BulkInsert` is the best of both worlds: ORM ergonomics, appender speed
  (~same time as raw DuckDB write, ~500 KB managed allocations at 1M rows).
- Provider extras: parquet export of a LINQ query, tiered hot-table →
  parquet-archive storage, `httpfs` extension loading for S3 reads.

**Cons**

- `SaveChanges` at bulk scale is catastrophic here: ~30× slower than every
  other write path and ~8 GB of allocations for 1M rows (snapshotting tracked
  entities + statement generation). It's an OLTP tool, not an ETL tool.
- The most machinery: contexts, options, model caching (the model bakes in
  the first `FromParquet` path it sees), in-memory-DB lifetime rules
  (connection must stay open).
- Young provider from a small team; pins its own fork of the DuckDB.NET
  ADO.NET package (`Skuirrels.DuckDB.NET.Data.Full`), which constrains your
  DuckDB.NET version choice.

---

## Picking between them

- **Simple POCO (de)serialization, no native deps, streams:** Parquet.Net.
- **Throughput-critical decode, schema control, wide schemas:** ParquetSharp
  column API.
- **Analytics pipelines / Arrow ecosystems:** ParquetSharp.Arrow.
- **Anything SQL-shaped: filters, aggregates, globs, remote files:** DuckDB
  (raw ADO.NET for streaming, Dapper for convenience, EF Core for typed LINQ).
- **Bulk ETL through an ORM-flavored API:** EF Core + `BulkInsert` — and never
  `SaveChanges` at this scale.
