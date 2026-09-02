using DuckDB.EFCoreProvider.Extensions;
using Microsoft.EntityFrameworkCore;
using ParquetBenchmarks.Models;

namespace ParquetBenchmarks;

// BenchRow can only be mapped one way per EF model, so read and write get
// separate contexts: the read context maps the entity straight onto a parquet
// file (queries compile to read_parquet(...)), the write context maps it to a
// physical DuckDB table that is then COPY'd out to parquet, mirroring the
// plain ADO.NET DuckDb benchmarks.

public sealed class BenchEfReadContext : DbContext
{
    private readonly string _parquetPath;

    public BenchEfReadContext(DbContextOptions<BenchEfReadContext> options, string parquetPath)
        : base(options)
    {
        _parquetPath = parquetPath;
    }

    public DbSet<BenchRow> Bench => Set<BenchRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // EF caches the model per context type, so the path baked in by the
        // first instance is reused; the benchmark path is fixed per run.
        modelBuilder.Entity<BenchRow>().FromParquet(_parquetPath);
    }
}

public sealed class BenchEfWriteContext : DbContext
{
    public BenchEfWriteContext(DbContextOptions<BenchEfWriteContext> options)
        : base(options)
    {
    }

    public DbSet<BenchRow> Bench => Set<BenchRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BenchRow>().ToTable("bench").HasKey(b => b.Id);
    }
}
