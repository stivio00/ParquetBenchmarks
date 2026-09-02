// ParquetSharp's low-level column API: ParquetFileWriter/ParquetFileReader with
// logical column writers/readers. The row-oriented tuple API would be the more
// direct counterpart to Parquet.Net's ParquetSerializer, but it only supports
// tuples of up to 7 elements and BenchRow has 10 columns.

using ParquetBenchmarks.Models;
using ParquetSharp;

namespace ParquetBenchmarks;

public static class ColumnParquetIO
{
    private static readonly Column[] Columns =
    {
        new Column<long>("Id"),
        new Column<string>("Name"),
        new Column<float>("Price"),
        new Column<DateTime>("CreatedAt"),
        new Column<string>("CreatedAtText"),
        new Column<bool>("IsActive"),
        new Column<string>("Category"),
        new Column<double>("Rating"),
        new Column<string>("ExternalId"),
        new Column<string>("Description")
    };

    public static void Write(string path, BenchRow[] rows)
    {
        var count = rows.Length;
        var ids = new long[count];
        var names = new string[count];
        var prices = new float[count];
        var createdAts = new DateTime[count];
        var createdAtTexts = new string[count];
        var actives = new bool[count];
        var categories = new string[count];
        var ratings = new double[count];
        var externalIds = new string[count];
        var descriptions = new string[count];

        for (int i = 0; i < count; i++)
        {
            var r = rows[i];
            ids[i] = r.Id;
            names[i] = r.Name;
            prices[i] = r.Price;
            createdAts[i] = r.CreatedAt;
            createdAtTexts[i] = r.CreatedAtText;
            actives[i] = r.IsActive;
            categories[i] = r.Category;
            ratings[i] = r.Rating;
            externalIds[i] = r.ExternalId;
            descriptions[i] = r.Description;
        }

        using var writer = new ParquetFileWriter(path, Columns);
        using var rowGroup = writer.AppendRowGroup();

        using (var column = rowGroup.NextColumn().LogicalWriter<long>()) column.WriteBatch(ids);
        using (var column = rowGroup.NextColumn().LogicalWriter<string>()) column.WriteBatch(names);
        using (var column = rowGroup.NextColumn().LogicalWriter<float>()) column.WriteBatch(prices);
        using (var column = rowGroup.NextColumn().LogicalWriter<DateTime>()) column.WriteBatch(createdAts);
        using (var column = rowGroup.NextColumn().LogicalWriter<string>()) column.WriteBatch(createdAtTexts);
        using (var column = rowGroup.NextColumn().LogicalWriter<bool>()) column.WriteBatch(actives);
        using (var column = rowGroup.NextColumn().LogicalWriter<string>()) column.WriteBatch(categories);
        using (var column = rowGroup.NextColumn().LogicalWriter<double>()) column.WriteBatch(ratings);
        using (var column = rowGroup.NextColumn().LogicalWriter<string>()) column.WriteBatch(externalIds);
        using (var column = rowGroup.NextColumn().LogicalWriter<string>()) column.WriteBatch(descriptions);

        writer.Close();
    }

    public static List<BenchRow> Read(string path)
    {
        using var reader = new ParquetFileReader(path);
        var result = new List<BenchRow>((int)reader.FileMetaData.NumRows);

        for (int rg = 0; rg < reader.FileMetaData.NumRowGroups; rg++)
        {
            using var rowGroup = reader.RowGroup(rg);
            var numRows = (int)rowGroup.MetaData.NumRows;

            long[] ids;
            string[] names;
            float[] prices;
            DateTime[] createdAts;
            string[] createdAtTexts;
            bool[] actives;
            string[] categories;
            double[] ratings;
            string[] externalIds;
            string[] descriptions;

            using (var column = rowGroup.Column(0).LogicalReader<long>()) ids = column.ReadAll(numRows);
            using (var column = rowGroup.Column(1).LogicalReader<string>()) names = column.ReadAll(numRows);
            using (var column = rowGroup.Column(2).LogicalReader<float>()) prices = column.ReadAll(numRows);
            using (var column = rowGroup.Column(3).LogicalReader<DateTime>()) createdAts = column.ReadAll(numRows);
            using (var column = rowGroup.Column(4).LogicalReader<string>()) createdAtTexts = column.ReadAll(numRows);
            using (var column = rowGroup.Column(5).LogicalReader<bool>()) actives = column.ReadAll(numRows);
            using (var column = rowGroup.Column(6).LogicalReader<string>()) categories = column.ReadAll(numRows);
            using (var column = rowGroup.Column(7).LogicalReader<double>()) ratings = column.ReadAll(numRows);
            using (var column = rowGroup.Column(8).LogicalReader<string>()) externalIds = column.ReadAll(numRows);
            using (var column = rowGroup.Column(9).LogicalReader<string>()) descriptions = column.ReadAll(numRows);

            for (int i = 0; i < numRows; i++)
            {
                result.Add(new BenchRow
                {
                    Id = ids[i],
                    Name = names[i],
                    Price = prices[i],
                    CreatedAt = createdAts[i],
                    CreatedAtText = createdAtTexts[i],
                    IsActive = actives[i],
                    Category = categories[i],
                    Rating = ratings[i],
                    ExternalId = externalIds[i],
                    Description = descriptions[i]
                });
            }
        }

        return result;
    }
}
