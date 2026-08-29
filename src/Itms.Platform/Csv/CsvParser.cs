using Itms.Platform.Results;

namespace Itms.Platform.Csv;

/// <summary>
/// Reads RFC 4180 CSV. Written by hand rather than taken from a package because the
/// import surface is small (SPEC.md §12: asset and user imports), the rules are
/// fixed, and a malformed upload has to come back as a <see cref="Result"/> the
/// endpoint can render — not as an exception from someone else's parser.
/// </summary>
public static class CsvParser
{
    /// <summary>
    /// Parses <paramref name="content"/> into a header row and data rows. Quoted fields,
    /// doubled quotes, and embedded line breaks are all handled; a ragged row or an
    /// unterminated quote is a failure, because silently padding a short row is how an
    /// import assigns an asset to the wrong person.
    /// </summary>
    public static Result<CsvDocument> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var lines = SplitRecords(content.TrimStart('﻿'));
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        var records = lines.Value;
        if (records.Count == 0)
        {
            return Error.Validation("csv.empty", "The file contains no rows.");
        }

        var headers = records[0].Select(h => h.Trim()).ToArray();
        if (headers.Length == 0 || headers.All(string.IsNullOrEmpty))
        {
            return Error.Validation("csv.no_header", "The first row must contain column headers.");
        }

        var duplicate = headers
            .Where(h => h.Length > 0)
            .GroupBy(h => h, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return Error.Validation("csv.duplicate_header", $"The column '{duplicate.Key}' appears more than once.");
        }

        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            if (headers[i].Length > 0)
            {
                columns[headers[i]] = i;
            }
        }

        var rows = new List<CsvRow>(records.Count - 1);
        for (var i = 1; i < records.Count; i++)
        {
            var fields = records[i];
            if (fields.Count != headers.Length)
            {
                // Row numbers are 1-based and count the header, so they match the line
                // number the user sees in their spreadsheet.
                return Error.Validation(
                    "csv.row_length",
                    $"Row {i + 1} has {fields.Count} fields but the header declares {headers.Length}.");
            }

            rows.Add(new CsvRow(i + 1, fields, columns));
        }

        return new CsvDocument(headers, rows);
    }

    private static Result<IReadOnlyList<IReadOnlyList<string>>> SplitRecords(string content)
    {
        var records = new List<IReadOnlyList<string>>();
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;
        var fieldStarted = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];

            if (inQuotes)
            {
                if (c != '"')
                {
                    field.Append(c);
                }
                else if (i + 1 < content.Length && content[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    fieldStarted = true;
                    break;

                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                    break;

                case '\r' or '\n':
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                    {
                        i++;
                    }

                    EndRecord(records, fields, field, fieldStarted);
                    fieldStarted = false;
                    break;

                default:
                    field.Append(c);
                    fieldStarted = true;
                    break;
            }
        }

        if (inQuotes)
        {
            return Error.Validation("csv.unterminated_quote", "The file ends inside a quoted field.");
        }

        EndRecord(records, fields, field, fieldStarted);
        return records;
    }

    private static void EndRecord(
        List<IReadOnlyList<string>> records,
        List<string> fields,
        System.Text.StringBuilder field,
        bool fieldStarted)
    {
        if (fields.Count == 0 && !fieldStarted && field.Length == 0)
        {
            // A blank line — including the newline that ends the last real row — is not a
            // record. Trailing newlines are normal and must not produce a ragged row.
            return;
        }

        fields.Add(field.ToString());
        field.Clear();
        records.Add(fields.ToArray());
        fields.Clear();
    }
}
