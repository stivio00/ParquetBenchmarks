using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DuckDBWrapper = DuckDB.NET.Data;
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

    [GlobalSetup]
    public void Setup()
    {
        // Generated once and reused across every [Benchmark] invocation so the
        // measured cost is purely the write path, not data generation.
        _rows = DataGenerator.Generate(RowCount);
        _dir = Path.Combine(Path.GetTempPath(), "parquet-bench-write");
        Directory.CreateDirectory(_dir);
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
        ColumnParquetIO.Write(PathFor("parquetsharp_column.parquet"), _rows);
    }

    [Benchmark]
    public void ParquetSharp_Arrow_Write()
    {
        ArrowParquetIO.Write(PathFor("parquetsharp_arrow.parquet"), _rows);
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
}
