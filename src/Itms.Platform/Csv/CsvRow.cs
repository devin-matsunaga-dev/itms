namespace Itms.Platform.Csv;

/// <summary>
/// One parsed data row, addressable by column name. Import validation reports
/// problems against <see cref="RowNumber"/>, which is the row the human sees in
/// their spreadsheet — header included, 1-based.
/// </summary>
public sealed class CsvRow
{
    private readonly IReadOnlyDictionary<string, int> _columns;
    private readonly IReadOnlyList<string> _fields;

    internal CsvRow(int rowNumber, IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> columns)
    {
        RowNumber = rowNumber;
        _fields = fields;
        _columns = columns;
    }

    /// <summary>The 1-based line of the source file this row came from, counting the header.</summary>
    public int RowNumber { get; }

    /// <summary>The raw fields, in file order.</summary>
    public IReadOnlyList<string> Fields => _fields;

    /// <summary>
    /// The trimmed value of <paramref name="column"/>, or <see langword="null"/> when the
    /// column is absent or the cell is blank. Blank and absent collapse deliberately:
    /// an importer cares whether it has a value, and reports the missing column once
    /// against the header rather than once per row.
    /// </summary>
    public string? this[string column] =>
        _columns.TryGetValue(column, out var index) && index < _fields.Count
            ? _fields[index] is { Length: > 0 } value && value.Trim() is { Length: > 0 } trimmed ? trimmed : null
            : null;
}
