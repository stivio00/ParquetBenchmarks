// NOTE: ParquetSharp exposes three APIs: low-level column writer/reader, a
// row-oriented tuple API, and this Arrow-based API (ParquetSharp.Arrow), which
// interops with Apache.Arrow RecordBatch/arrays. This file is what stands in
// for "Apache Arrow" in the benchmark, since Apache.Arrow itself is a columnar
// in-memory format library, not a Parquet reader/writer on its own.
//
// The exact method names on FileWriter/FileReader (ParquetSharp.Arrow) and the
// Arrow builder APIs (Apache.Arrow) have shifted slightly across package
// versions. Verify signatures against the versions pinned in the .csproj
// before running — this is written against ParquetSharp 24.0.0 / Apache.Arrow
// 18.0.0 semantics but package updates may rename a method or two.

using Apache.Arrow;
using Apache.Arrow.Types;
using ParquetBenchmarks.Models;
using ParquetSharp.Arrow;

namespace ParquetBenchmarks;

public static class ArrowParquetIO
{
    public static Schema BuildSchema() =>
        new Schema.Builder()
            .Field(f => f.Name("Id").DataType(Int64Type.Default).Nullable(false))
            .Field(f => f.Name("Name").DataType(StringType.Default).Nullable(false))
            .Field(f => f.Name("Price").DataType(FloatType.Default).Nullable(false))
            .Field(f => f.Name("CreatedAt").DataType(TimestampType.Default).Nullable(false))
            .Field(f => f.Name("CreatedAtText").DataType(StringType.Default).Nullable(false))
            .Field(f => f.Name("IsActive").DataType(BooleanType.Default).Nullable(false))
            .Field(f => f.Name("Category").DataType(StringType.Default).Nullable(false))
            .Field(f => f.Name("Rating").DataType(DoubleType.Default).Nullable(false))
            .Field(f => f.Name("ExternalId").DataType(StringType.Default).Nullable(false))
            .Field(f => f.Name("Description").DataType(StringType.Default).Nullable(false))
            .Build();

    public static void Write(string path, BenchRow[] rows)
    {
        var schema = BuildSchema();

        var idBuilder = new Int64Array.Builder();
        var nameBuilder = new StringArray.Builder();
        var priceBuilder = new FloatArray.Builder();
        var createdBuilder = new TimestampArray.Builder();
        var createdTextBuilder = new StringArray.Builder();
        var activeBuilder = new BooleanArray.Builder();
        var categoryBuilder = new StringArray.Builder();
        var ratingBuilder = new DoubleArray.Builder();
        var externalIdBuilder = new StringArray.Builder();
        var descriptionBuilder = new StringArray.Builder();

        foreach (var r in rows)
        {
            idBuilder.Append(r.Id);
            nameBuilder.Append(r.Name);
            priceBuilder.Append(r.Price);
            createdBuilder.Append(new DateTimeOffset(DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)));
            createdTextBuilder.Append(r.CreatedAtText);
            activeBuilder.Append(r.IsActive);
            categoryBuilder.Append(r.Category);
            ratingBuilder.Append(r.Rating);
            externalIdBuilder.Append(r.ExternalId);
            descriptionBuilder.Append(r.Description);
        }

        var batch = new RecordBatch(schema, new IArrowArray[]
        {
            idBuilder.Build(), nameBuilder.Build(), priceBuilder.Build(), createdBuilder.Build(),
            createdTextBuilder.Build(), activeBuilder.Build(), categoryBuilder.Build(),
            ratingBuilder.Build(), externalIdBuilder.Build(), descriptionBuilder.Build()
        }, rows.Length);

        using var writer = new FileWriter(path, schema);
        writer.WriteRecordBatch(batch);
        writer.Close();
    }

    public static async Task<List<BenchRow>> ReadAsync(string path)
    {
        using var reader = new FileReader(path);
        var result = new List<BenchRow>(1_000_000);

        for (int rg = 0; rg < reader.NumRowGroups; rg++)
        {
            using var batchReader = reader.GetRecordBatchReader(rowGroups: new[] { rg });
            // IArrowArrayStream (Apache.Arrow) only exposes ReadNextRecordBatchAsync —
            // there is no synchronous ReadNextRecordBatch on this interface.
            RecordBatch? batch;
            while ((batch = await batchReader.ReadNextRecordBatchAsync()) is not null)
            {
                using var _ = batch;
                var ids = (Int64Array)batch.Column("Id");
                var names = (StringArray)batch.Column("Name");
                var prices = (FloatArray)batch.Column("Price");
                var createds = (TimestampArray)batch.Column("CreatedAt");
                var createdTexts = (StringArray)batch.Column("CreatedAtText");
                var actives = (BooleanArray)batch.Column("IsActive");
                var categories = (StringArray)batch.Column("Category");
                var ratings = (DoubleArray)batch.Column("Rating");
                var externalIds = (StringArray)batch.Column("ExternalId");
                var descriptions = (StringArray)batch.Column("Description");

                for (int i = 0; i < batch.Length; i++)
                {
                    result.Add(new BenchRow
                    {
                        Id = ids.GetValue(i) ?? 0,
                        Name = names.GetString(i),
                        Price = prices.GetValue(i) ?? 0f,
                        CreatedAt = createds.GetTimestamp(i)?.UtcDateTime ?? default,
                        CreatedAtText = createdTexts.GetString(i),
                        IsActive = actives.GetValue(i) ?? false,
                        Category = categories.GetString(i),
                        Rating = ratings.GetValue(i) ?? 0d,
                        ExternalId = externalIds.GetString(i),
                        Description = descriptions.GetString(i)
                    });
                }
            }
        }

        return result;
    }
}
