namespace ParquetBenchmarks.Models;

/// <summary>
/// 10 columns spanning the types requested: integer id, string, float, date,
/// date-as-string, bool, plus a few creative "other type" columns (enum-like
/// category, a double rating, a GUID identifier, and a long free-text
/// description column to stress string-heavy decoding).
/// </summary>
public class BenchRow
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;
    public float Price { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedAtText { get; set; } = default!;
    public bool IsActive { get; set; }
    public string Category { get; set; } = default!;
    public double Rating { get; set; }
    public string ExternalId { get; set; } = default!;
    public string Description { get; set; } = default!;
}
