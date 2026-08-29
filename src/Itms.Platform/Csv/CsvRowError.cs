namespace Itms.Platform.Csv;

/// <summary>
/// One problem found in one cell of an import file. Imports accumulate these rather
/// than stopping at the first bad row — a user fixing a 300-row spreadsheet needs the
/// whole list, not one error at a time.
/// </summary>
/// <param name="RowNumber">The 1-based source line, counting the header, so it matches what the user sees.</param>
/// <param name="Column">The column header the problem belongs to, or <see langword="null"/> for a whole-row problem.</param>
/// <param name="Message">What is wrong, phrased for the person who has to fix the file.</param>
public sealed record CsvRowError(int RowNumber, string? Column, string Message);
