# Streams, MemoryStream and reading parquet from S3

## TL;DR — do the benchmarked readers need a file?

**No for three of the four libraries, yes for DuckDB.**

| API | Read from any .NET `Stream` | Write to any .NET `Stream` | Entry points (verified against pinned versions) |
|---|:---:|:---:|---|
| Parquet.Net | ✅ | ✅ | `ParquetSerializer.DeserializeAsync<T>(stream)` / `SerializeAsync(rows, stream)`; lower-level `ParquetReader.CreateAsync(stream)` |
| ParquetSharp column | ✅ | ✅ | `new ParquetFileReader(new IO.ManagedRandomAccessFile(stream))` / `new ParquetFileWriter(new IO.ManagedOutputStream(stream), columns)` |
| ParquetSharp.Arrow | ✅ | ✅ | `new Arrow.FileReader(stream)` / `new Arrow.FileWriter(stream, schema)` |
| DuckDB (ADO.NET / Dapper / EF Core) | ❌ | ❌ | `read_parquet('<path>')` and `COPY ... TO '<path>'` are path/URL based only |

So `MemoryStream` works directly with Parquet.Net and both ParquetSharp APIs —
the benchmark code happens to use file paths, but swapping in a stream is a
one-line change. DuckDB has **no .NET Stream entry point**: in-memory bytes
must be landed in a temp file first, or fetched remotely by DuckDB itself via
its `httpfs` extension (below).

One physical caveat: **parquet requires random access.** The footer (schema +
column-chunk offsets) sits at the *end* of the file, and readers then seek to
individual column chunks. So "any Stream" really means *any seekable stream* —
`MemoryStream`, `FileStream`, or a network stream wrapped with range-request
random access (pattern A2 below). A raw forward-only HTTP response stream
cannot be handed to any parquet reader without buffering it fully.

---

## Reading from S3

Two fundamentally different strategies:

- **A. AWS SDK for .NET fetches the bytes, a managed parquet library decodes
  them.** You control credentials and networking with standard AWS tooling.
- **B. DuckDB's native `httpfs` reader fetches the bytes itself.** Zero .NET
  I/O code, and DuckDB can push projections/filters down so it only downloads
  the byte ranges it actually needs.

### A1 — Whole-object download → `MemoryStream` → Parquet.Net / ParquetSharp

The simple pattern. Fine up to a few hundred MB; costs a full object download
no matter how few rows/columns you need.

```csharp
using Amazon.S3;
using Amazon.S3.Model;

using var s3 = new AmazonS3Client();   // standard credential chain: IAM role, env, profile...

var head = await s3.GetObjectMetadataAsync(bucket, key);   // cheap HEAD: get the size
if (head.ContentLength is > int.MaxValue)
    throw new NotSupportedException("Too big for MemoryStream; use a temp file or A2/A3.");

await using var ms = new MemoryStream(checked((int)head.ContentLength));
using (var resp = await s3.GetObjectAsync(bucket, key))
    await resp.ResponseStream.CopyToAsync(ms);
ms.Position = 0;

// then any of:
var result = await ParquetSerializer.DeserializeAsync<BenchRow>(ms);
// or: new ParquetFileReader(new ParquetSharp.IO.ManagedRandomAccessFile(ms))
// or: new ParquetSharp.Arrow.FileReader(ms)
```

### A2 — Seekable S3 stream (range GETs) → ParquetSharp

This is where parquet's design pays off: a reader only *needs* the footer and
the column chunks you actually read, which can be kilobytes out of a
gigabyte. Implement a `Stream` that turns `Seek`/`Read` into HTTP range
requests, and hand it to ParquetSharp via `ManagedRandomAccessFile`:

```csharp
public sealed class SeekableS3Stream : Stream
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket, _key;
    private readonly long _length;
    private long _position;

    public SeekableS3Stream(IAmazonS3 s3, string bucket, string key, long length)
        => (_s3, _bucket, _key, _length) = (s3, bucket, key, length);

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override long Length => _length;
    public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _length) return 0;
        long end = Math.Min(_position + count, _length) - 1;

        var request = new GetObjectRequest
        {
            BucketName = _bucket,
            Key = _key,
            RequestByteRange = $"bytes={_position}-{end}"   // one ranged GET per Read
        };
        using var resp = _s3.GetObject(request);

        int total = 0, n;
        while (total < count && (n = resp.ResponseStream.Read(buffer, offset + total, count - total)) > 0)
            total += n;
        _position += total;
        return total;
    }

    public override long Seek(long offset, SeekOrigin origin) => _position = origin switch
    {
        SeekOrigin.Begin => offset,
        SeekOrigin.Current => _position + offset,
        SeekOrigin.End => _length + offset,
        _ => throw new ArgumentOutOfRangeException(nameof(origin))
    };

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
```

```csharp
var head = await s3.GetObjectMetadataAsync(bucket, key);
using var input = new ParquetSharp.IO.ManagedRandomAccessFile(
    new SeekableS3Stream(s3, bucket, key, head.ContentLength));
using var reader = new ParquetSharp.ParquetFileReader(input);
// read only the columns you need: reader.RowGroup(0).Column(i)...
```

**Before using this in production, harden it.** The sketch above makes one
HTTP request per `Read` call; parquet readers issue many small reads (footer,
then each column chunk). At minimum:

- **Buffer aligned windows** (e.g. 1–8 MB) so one range GET serves many
  reads — this is what makes A2 genuinely cheap.
- **Pin the object version** (pass the `ETag`/version you saw in the HEAD as
  a conditional on every GET) — otherwise a replace mid-read gives you a
  Frankenstein file.
- Add retries/backoff and request timeouts; disable connection pooling only
  if you know why.

### A3 — Temp file + DuckDB

When you want DuckDB's SQL anyway and the bytes are already in .NET (e.g.
downloaded, received over a socket):

```csharp
using Amazon.S3.Transfer;
await new TransferUtility(s3).DownloadAsync(tempPath, bucket, key);

// then: SELECT * FROM read_parquet('<tempPath>'); via DuckDBConnection
```

This is the only option for *stream-fed* DuckDB — it has no `Stream` entry
point (see matrix above).

### B — DuckDB `httpfs`: the native remote reader

DuckDB fetches S3/GCS/Azure objects itself, in parallel, with projection and
filter pushdown into the scan (zonemap pruning — it can skip row groups and
even column chunks server-side). No temp files, no .NET I/O code:

```sql
INSTALL httpfs;   -- downloaded once; DuckDB usually auto-installs on first use
LOAD httpfs;

-- static credentials (avoid if you can):
CREATE SECRET (TYPE S3, KEY_ID 'AKIA...', SECRET '...', REGION 'eu-west-1');

-- or pick up the ambient AWS credential chain (env vars, config file, instance profile):
CREATE SECRET (TYPE S3, PROVIDER credential_chain);

-- or no secret at all: a presigned URL
SELECT * FROM read_parquet('https://bucket.s3.eu-west-1.amazonaws.com/key.parquet?X-Amz-Signature=...');
```

```sql
-- projection + filter pushdown, glob over many files, hive partitioning:
SELECT Category, avg(Rating)
FROM read_parquet('s3://bucket/data/*.parquet', hive_partitioning = true)
WHERE CreatedAt >= TIMESTAMP '2025-01-01'
GROUP BY Category;
```

With the EF Core provider used in this repo, load the extension per
connection and keep using LINQ — the `FromParquet` mapping accepts `s3://`
URLs too:

```csharp
options.UseDuckDB("Data Source=:memory:", duckdb => duckdb.LoadExtension("httpfs"));

// in the read context's model:
modelBuilder.Entity<BenchRow>().FromParquet("s3://bucket/data/bench.parquet");

var rows = context.Bench.AsNoTracking().Where(b => b.Category == "Books").ToList();
```

### A vs B — which one?

| | A1 full download | A2 range stream | A3 temp file | B httpfs |
|---|---|---|---|---|
| Bytes transferred | whole object | only what's read | whole object | only what's read |
| .NET code | ~10 lines | custom stream class | ~5 lines | none (SQL) |
| Filter/projection pushdown | ❌ | column-level | ❌ | ✅ row groups + columns |
| Parallel fetch | ❌ (1 stream) | ❌ (1 stream) | ✅ (multipart) | ✅ (parallel ranges) |
| Credentials | AWS SDK chain | AWS SDK chain | AWS SDK chain | DuckDB secret / `credential_chain` / presigned |
| Best for | small objects, full materialization | few columns of big objects, ParquetSharp users | "I need SQL on bytes I already have" | analytics, globs, remote datasets |

Rule of thumb: **need all rows as POCOs** → A1 (or A2 when the object is much
bigger than the columns you want); **need aggregates/filters/globs** → B, and
often by a wide margin, because most of the data never leaves S3.

## Writing to S3

- **DuckDB**: `COPY (SELECT ...) TO 's3://bucket/out.parquet' (FORMAT parquet);`
  with the same `httpfs` secret — writes remotely, supports zstd/row-group
  options, partitioned output.
- **Parquet.Net**: serialize into a `MemoryStream`, rewind, upload — S3's
  plain `PutObject` accepts a stream with a known length, which a
  `MemoryStream` provides:

  ```csharp
  await using var ms = new MemoryStream();
  await ParquetSerializer.SerializeAsync(rows, ms);
  ms.Position = 0;
  await s3.PutObjectAsync(new PutObjectRequest
      { BucketName = bucket, Key = key, InputStream = ms });
  ```

  For large payloads use `TransferUtility` (multipart upload) on a temp file
  or seekable buffer instead.
- **ParquetSharp**: can write to any `Stream` via
  `new ParquetFileWriter(new IO.ManagedOutputStream(stream), columns)`, but a
  plain S3 PUT needs the length up front — so in practice buffer to a
  `MemoryStream`/temp file and upload, exactly like Parquet.Net.
