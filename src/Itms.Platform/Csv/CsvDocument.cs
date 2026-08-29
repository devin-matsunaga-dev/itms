namespace Itms.Platform.Csv;

/// <summary>A parsed CSV file: its header row and its data rows.</summary>
public sealed class CsvDocument
{
    internal CsvDocument(IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
    {
        Headers = headers;
        Rows = rows;
    }

    /// <summary>The column headers, trimmed, in file order.</summary>
    public IReadOnlyList<string> Headers { get; }

    /// <summary>The data rows, in file order.</summary>
    public IReadOnlyList<CsvRow> Rows { get; }

    /// <summary>
    /// True when <paramref name="column"/> is present. Header matching is
    /// case-insensitive: an import file typed by hand should not fail because someone
    /// wrote "Asset Tag" where the template says "Asset tag".
    /// </summary>
    public bool HasColumn(string column) =>
        Headers.Any(h => string.Equals(h, column, StringComparison.OrdinalIgnoreCase));

    /// <summary>The required columns that are missing, for a single up-front error instead of one per row.</summary>
    public IReadOnlyList<string> MissingColumns(IEnumerable<string> required)
    {
        ArgumentNullException.ThrowIfNull(required);
        return [.. required.Where(column => !HasColumn(column))];
    }
}
