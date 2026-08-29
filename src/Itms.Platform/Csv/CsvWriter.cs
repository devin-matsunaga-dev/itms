using System.Globalization;
using System.Text;

namespace Itms.Platform.Csv;

/// <summary>
/// Writes RFC 4180 CSV. SPEC.md puts CSV export on every report and on the major
/// tables; this is the one implementation, so quoting and injection defence are
/// decided once instead of per report.
/// </summary>
public static class CsvWriter
{
    // Excel and LibreOffice evaluate a cell that opens with one of these as a formula.
    // An exported ticket subject is attacker-controlled text, so the export is where
    // that gets defused.
    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

    /// <summary>Renders a header row and data rows as a CSV document.</summary>
    /// <param name="headers">Column headers, written as the first row.</param>
    /// <param name="rows">Data rows. Each must have the same length as <paramref name="headers"/>.</param>
    /// <exception cref="ArgumentException">A row's field count does not match the header count.</exception>
    public static string Write(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        var builder = new StringBuilder();
        AppendRow(builder, headers);

        foreach (var row in rows)
        {
            if (row.Count != headers.Count)
            {
                throw new ArgumentException(
                    $"Row has {row.Count} fields but the header declares {headers.Count}.", nameof(rows));
            }

            AppendRow(builder, row);
        }

        return builder.ToString();
    }

    /// <summary>Renders a header row and projects each item into a data row.</summary>
    public static string Write<T>(IReadOnlyList<string> headers, IEnumerable<T> items, Func<T, IReadOnlyList<string?>> project)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(project);
        return Write(headers, items.Select(project));
    }

    /// <summary>
    /// Escapes a single field: quotes it when it contains a delimiter, a quote, or a
    /// line break, and neutralises a leading formula trigger with a single quote.
    /// </summary>
    public static string EscapeField(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var field = Array.IndexOf(FormulaTriggers, value[0]) >= 0 ? "'" + value : value;

        return field.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? string.Concat("\"", field.Replace("\"", "\"\"", StringComparison.Ordinal), "\"")
            : field;
    }

    /// <summary>Formats an instant for export. All exported times are UTC and ISO 8601 (ARCHITECTURE.md §11).</summary>
    public static string Field(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static void AppendRow(StringBuilder builder, IReadOnlyList<string?> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(EscapeField(fields[i]));
        }

        // CRLF per RFC 4180; it is what Excel expects and what every other reader tolerates.
        builder.Append("\r\n");
    }
}
