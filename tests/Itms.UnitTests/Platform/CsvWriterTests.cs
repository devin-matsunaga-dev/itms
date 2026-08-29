using Itms.Platform.Csv;

namespace Itms.UnitTests.Platform;

public sealed class CsvWriterTests
{
    [Fact]
    public void A_plain_document_is_header_then_rows_separated_by_crlf()
    {
        var csv = CsvWriter.Write(["tag", "status"], [["A-001", "InService"], ["A-002", "Retired"]]);

        csv.ShouldBe("tag,status\r\nA-001,InService\r\nA-002,Retired\r\n");
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("has,comma", "\"has,comma\"")]
    [InlineData("has \"quote\"", "\"has \"\"quote\"\"\"")]
    [InlineData("has\nnewline", "\"has\nnewline\"")]
    public void Fields_are_quoted_only_when_they_have_to_be(string? value, string expected)
    {
        CsvWriter.EscapeField(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+44 7700 900000")]
    [InlineData("-2")]
    [InlineData("@SUM(A1)")]
    public void A_field_that_a_spreadsheet_would_evaluate_is_neutralised(string value)
    {
        var escaped = CsvWriter.EscapeField(value);

        escaped.TrimStart('"').ShouldStartWith("'");
    }

    [Fact]
    public void Projected_rows_are_written_in_column_order()
    {
        (string Tag, int Age)[] assets = [("A-001", 3), ("A-002", 5)];

        var csv = CsvWriter.Write(
            ["tag", "age"],
            assets,
            a => [a.Tag, a.Age.ToString(System.Globalization.CultureInfo.InvariantCulture)]);

        csv.ShouldBe("tag,age\r\nA-001,3\r\nA-002,5\r\n");
    }

    [Fact]
    public void Times_are_exported_as_utc_iso_8601()
    {
        CsvWriter.Field(new DateTimeOffset(2026, 8, 29, 14, 30, 0, TimeSpan.FromHours(2)))
            .ShouldBe("2026-08-29T12:30:00Z");
    }

    [Fact]
    public void A_row_that_does_not_match_the_header_is_a_programming_error()
    {
        Should.Throw<ArgumentException>(() => CsvWriter.Write(["a", "b"], [["only-one"]]));
    }

    [Fact]
    public void A_written_document_parses_back_to_the_same_values()
    {
        var csv = CsvWriter.Write(["subject", "note"], [["Laptop won't boot, again", "He said \"it sparked\"\nthen died"]]);

        var parsed = CsvParser.Parse(csv);

        parsed.IsSuccess.ShouldBeTrue();
        var row = parsed.Value.Rows.ShouldHaveSingleItem();
        row["subject"].ShouldBe("Laptop won't boot, again");
        row["note"].ShouldBe("He said \"it sparked\"\nthen died");
    }
}
