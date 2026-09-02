using ParquetBenchmarks.Models;

namespace ParquetBenchmarks;

public static class DataGenerator
{
    private static readonly string[] Categories =
        { "Electronics", "Groceries", "Apparel", "Automotive", "Books", "Toys", "Garden", "Sports" };

    private static readonly string[] Adjectives =
        { "Rapid", "Silent", "Golden", "Crimson", "Frozen", "Ancient", "Electric", "Hollow", "Vivid", "Quiet" };

    private static readonly string[] Nouns =
        { "Falcon", "Reactor", "Meadow", "Circuit", "Harbor", "Lantern", "Comet", "Anvil", "Glacier", "Orchid" };

    /// <summary>Generates a fixed, seeded dataset so every library reads/writes identical data.</summary>
    public static BenchRow[] Generate(int count, int seed = 42)
    {
        var random = new Random(seed);
        var rows = new BenchRow[count];
        var baseDate = new DateTime(2020, 1, 1);

        for (int i = 0; i < count; i++)
        {
            var createdAt = baseDate.AddMinutes(random.Next(0, 60 * 24 * 365 * 5));

            rows[i] = new BenchRow
            {
                Id = i + 1,
                Name = $"{Pick(random, Adjectives)} {Pick(random, Nouns)} #{i}",
                Price = (float)Math.Round(random.NextDouble() * 999.99, 2),
                CreatedAt = createdAt,
                CreatedAtText = createdAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                IsActive = random.Next(0, 2) == 1,
                Category = Pick(random, Categories),
                Rating = Math.Round(random.NextDouble() * 5.0, 3),
                ExternalId = Guid.NewGuid().ToString(),
                Description = GenerateDescription(random, i)
            };
        }

        return rows;
    }

    private static string Pick(Random random, string[] values) => values[random.Next(values.Length)];

    // Simulates a "long column": variable-length free text, roughly 80-250 chars,
    // to stress string allocation/decoding the way a real payload/notes field would.
    private static string GenerateDescription(Random random, int i)
    {
        int wordCount = random.Next(12, 36);
        var sb = new System.Text.StringBuilder();
        sb.Append("Record ").Append(i).Append(": ");

        for (int w = 0; w < wordCount; w++)
        {
            sb.Append(Pick(random, Adjectives)).Append(' ').Append(Pick(random, Nouns)).Append(' ');
        }

        return sb.ToString().TrimEnd();
    }
}
