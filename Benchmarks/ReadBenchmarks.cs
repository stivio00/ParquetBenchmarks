using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Dapper;
using DuckDBWrapper = DuckDB.NET.Data;
using Parquet;
using Parquet.Serialization;
using ParquetBenchmarks.Models;

namespace ParquetBenchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]
public class ReadBenchmarks
{
    private const int RowCount = 1_000_000;

    private string _dir = default!;
    private string _parquetNetPath = default!;
    private string _parquetSharpColumnPath = default!;
    private string _parquetSharpArrowPath = default!;
    private string _duckDbPath = default!;

    [GlobalSetup]
    public async Task Setup()
    {
        var rows = DataGenerator.Generate(RowCount);
        _dir = Path.Combine(Path.GetTempPath(), "parquet-bench-read");
        Directory.CreateDirectory(_dir);

        // Each format's file is written exactly once here, outside the measured
        // benchmarks, so every [Benchmark] method below measures pure read+decode.

        _parquetNetPath = Path.Combine(_dir, "parquetnet.parquet");
        await using (var fs = File.Create(_parquetNetPath))
            await ParquetSerializer.SerializeAsync(rows, fs);

        _parquetSharpColumnPath = Path.Combine(_dir, "parquetsharp_column.parquet");
        ColumnParquetIO.Write(_parquetSharpColumnPath, rows);

        _parquetSharpArrowPath = Path.Combine(_dir, "parquetsharp_arrow.parquet");
        ArrowParquetIO.Write(_parquetSharpArrowPath, rows);

        _duckDbPath = Path.Combine(_dir, "duckdb.parquet").Replace("\\", "/");
        using (var connection = new DuckDBWrapper.DuckDBConnection("DataSource=:memory:"))
        {
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
            using (var appender = connection.CreateAppender("bench"))
            {
                foreach (var r in rows)
                {
                    appender.CreateRow()
                        .AppendValue(r.Id).AppendValue(r.Name).AppendValue(r.Price).AppendValue(r.CreatedAt)
                        .AppendValue(r.CreatedAtText).AppendValue(r.IsActive).AppendValue(r.Category)
                        .AppendValue(r.Rating).AppendValue(r.ExternalId).AppendValue(r.Description)
                        .EndRow();
                }
            }
            using var copy = connection.CreateCommand();
            copy.CommandText = $"COPY bench TO '{_duckDbPath}' (FORMAT parquet);";
            copy.ExecuteNonQuery();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private const string SelectSql =
        "SELECT Id, Name, Price, CreatedAt, CreatedAtText, IsActive, Category, Rating, ExternalId, Description FROM read_parquet(@path);";

    [Benchmark(Baseline = true)]
    public async Task<int> ParquetNet_ReadDecode()
    {
        await using var fs = File.OpenRead(_parquetNetPath);
        var rows = await ParquetSerializer.DeserializeAsync<BenchRow>(fs);
        return rows.Data.Count;
    }

    [Benchmark]
    public int ParquetSharp_Column_ReadDecode()
    {
        var rows = ColumnParquetIO.Read(_parquetSharpColumnPath);
        return rows.Count;
    }

    [Benchmark]
    public async Task<int> ParquetSharp_Arrow_ReadDecode()
    {
        var rows = await ArrowParquetIO.ReadAsync(_parquetSharpArrowPath);
        return rows.Count;
    }

    [Benchmark]
    public int DuckDb_AdoNet_ReadDecode()
    {
        using var connection = new DuckDBWrapper.DuckDBConnection("DataSource=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT Id, Name, Price, CreatedAt, CreatedAtText, IsActive, Category, Rating, ExternalId, Description FROM read_parquet('{_duckDbPath}');";
        using var reader = cmd.ExecuteReader();

        var list = new List<BenchRow>(RowCount);
        while (reader.Read())
        {
            list.Add(new BenchRow
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Price = reader.GetFloat(2),
                CreatedAt = reader.GetDateTime(3),
                CreatedAtText = reader.GetString(4),
                IsActive = reader.GetBoolean(5),
                Category = reader.GetString(6),
                Rating = reader.GetDouble(7),
                ExternalId = reader.GetString(8),
                Description = reader.GetString(9)
            });
        }
        return list.Count;
    }

    [Benchmark]
    public int DuckDb_Dapper_ReadDecode()
    {
        using var connection = new DuckDBWrapper.DuckDBConnection("DataSource=:memory:");
        connection.Open();

        var sql = $"SELECT Id, Name, Price, CreatedAt, CreatedAtText, IsActive, Category, Rating, ExternalId, Description FROM read_parquet('{_duckDbPath}');";
        var rows = connection.Query<BenchRow>(sql); // buffered: fully materializes into a List<T>
        return rows.AsList().Count;
    }
}
