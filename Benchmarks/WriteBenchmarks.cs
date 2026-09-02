using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DuckDBWrapper = DuckDB.NET.Data;
using DuckDB.EFCoreProvider.Extensions;
using Microsoft.EntityFrameworkCore;
using Parquet;
using Parquet.Serialization;
using ParquetBenchmarks.Models;

namespace ParquetBenchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]
public class WriteBenchmarks
{
    private const int RowCount = 1_000_000;

    private BenchRow[] _rows = Array.Empty<BenchRow>();
    private string _dir = default!;
    private DbContextOptions<BenchEfWriteContext> _efWriteOptions = default!;

    [GlobalSetup]
    public void Setup()
    {
        // Generated once and reused across every [Benchmark] invocation so the
        // measured cost is purely the write path, not data generation.
        _rows = DataGenerator.Generate(RowCount);
        _dir = Path.Combine(Path.GetTempPath(), "parquet-bench-write");
        Directory.CreateDirectory(_dir);

        _efWriteOptions = new DbContextOptionsBuilder<BenchEfWriteContext>()
            .UseDuckDB("Data Source=:memory:", duckdb => duckdb.EnableBulkInsertBatching())
            .Options;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private string PathFor(string fileName) => Path.Combine(_dir, fileName);

    [Benchmark(Baseline = true)]
    public async Task ParquetNet_Write()
    {
        await using var fs = File.Create(PathFor("parquetnet.parquet"));
        await ParquetSerializer.SerializeAsync(_rows, fs);
    }

    [Benchmark]
    public void ParquetSharp_Column_Write()
    {
        ColumnParquetIo.Write(PathFor("parquetsharp_column.parquet"), _rows);
    }

    [Benchmark]
    public void ParquetSharp_Arrow_Write()
    {
        ArrowParquetIo.Write(PathFor("parquetsharp_arrow.parquet"), _rows);
    }

    [Benchmark]
    public void DuckDb_Write()
    {
        var outPath = PathFor("duckdb.parquet").Replace("\\", "/");

        using var connection = new DuckDBWrapper.DuckDBConnection("DataSource=:memory:");
        connection.Open();

        using (var create = connection.CreateCommand())
        {
            create.CommandText = @"
                CREATE TABLE bench (
                    Id BIGINT, Name VARCHAR, Price REAL, CreatedAt TIMESTAMP, CreatedAtText VARCHAR,
                    IsActive BOOLEAN, Category VARCHAR, Rating DOUBLE, ExternalId VARCHAR, Description VARCHAR
                );";
            create.ExecuteNonQuery();
        }

        // Appender is DuckDB's fast bulk-load path — the fair comparison point
        // against the other libraries' row-by-row write APIs.
        using (var appender = connection.CreateAppender("bench"))
        {
            foreach (var r in _rows)
            {
                appender.CreateRow()
                    .AppendValue(r.Id)
                    .AppendValue(r.Name)
                    .AppendValue(r.Price)
                    .AppendValue(r.CreatedAt)
                    .AppendValue(r.CreatedAtText)
                    .AppendValue(r.IsActive)
                    .AppendValue(r.Category)
                    .AppendValue(r.Rating)
                    .AppendValue(r.ExternalId)
                    .AppendValue(r.Description)
                    .EndRow();
            }
        }

        using var copy = connection.CreateCommand();
        copy.CommandText = $"COPY bench TO '{outPath}' (FORMAT parquet);";
        copy.ExecuteNonQuery();
    }

    [Benchmark]
    public void DuckDb_EfCore_Write()
    {
        var outPath = PathFor("duckdb_efcore.parquet").Replace("\\", "/");

        using var context = new BenchEfWriteContext(_efWriteOptions);
        // An in-memory DuckDB only lives as long as its connection and EF
        // opens/closes per command, so hold it open for the context's lifetime.
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        context.ChangeTracker.AutoDetectChangesEnabled = false;

        // The canonical EF Core write path: tracked entities + SaveChanges,
        // with the provider's insert batching merging rows into multi-row
        // INSERT statements (roughly an order of magnitude faster than the
        // default one-statement-per-row behaviour).
        context.Bench.AddRange(_rows);
        context.SaveChanges();

        context.Database.ExecuteSql($"COPY bench TO {outPath} (FORMAT parquet);");
    }

    [Benchmark]
    public void DuckDb_EfCore_BulkWrite()
    {
        var outPath = PathFor("duckdb_efcore_bulk.parquet").Replace("\\", "/");

        using var context = new BenchEfWriteContext(_efWriteOptions);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        // The provider's ETL fast path: appends straight through DuckDB's
        // columnar Appender and bypasses the change tracker entirely.
        context.BulkInsert(_rows);

        context.Database.ExecuteSql($"COPY bench TO {outPath} (FORMAT parquet);");
    }
}
