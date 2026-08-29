using Itms.Platform.Csv;
using Itms.Platform.Results;

namespace Itms.UnitTests.Platform;

public sealed class CsvParserTests
{
    [Fact]
    public void A_simple_file_yields_a_header_and_rows()
    {
        var document = CsvParser.Parse("tag,serial\r\nA-001,SN1\r\nA-002,SN2\r\n").Value;

        document.Headers.ShouldBe(["tag", "serial"]);
        document.Rows.Count.ShouldBe(2);
        document.Rows[0]["tag"].ShouldBe("A-001");
        document.Rows[1]["serial"].ShouldBe("SN2");
    }

    [Fact]
    public void Lf_line_endings_and_a_missing_final_newline_both_parse()
    {
        var document = CsvParser.Parse("tag\nA-001\nA-002").Value;

        document.Rows.Count.ShouldBe(2);
    }

    [Fact]
    public void A_utf8_byte_order_mark_does_not_become_part_of_the_first_header()
    {
        var document = CsvParser.Parse("﻿tag,serial\r\nA-001,SN1").Value;

        document.Headers[0].ShouldBe("tag");
        document.HasColumn("tag").ShouldBeTrue();
    }

    [Fact]
    public void Quoted_fields_keep_commas_quotes_and_line_breaks()
    {
        var document = CsvParser.Parse("subject,note\r\n\"Printer, third floor\",\"Line one\r\nLine two \"\"quoted\"\"\"\r\n").Value;

        var row = document.Rows.ShouldHaveSingleItem();
        row["subject"].ShouldBe("Printer, third floor");
        row["note"].ShouldBe("Line one\r\nLine two \"quoted\"");
    }

    [Fact]
    public void Row_numbers_match_the_line_the_user_sees_in_their_spreadsheet()
    {
        var document = CsvParser.Parse("tag\r\nA-001\r\nA-002\r\n").Value;

        document.Rows[0].RowNumber.ShouldBe(2);
        document.Rows[1].RowNumber.ShouldBe(3);
    }

    [Fact]
    public void Header_matching_ignores_case_and_surrounding_space()
    {
        var document = CsvParser.Parse(" Asset Tag , Serial \r\nA-001,SN1").Value;

        document.HasColumn("asset tag").ShouldBeTrue();
        document.Rows[0]["ASSET TAG"].ShouldBe("A-001");
    }

    [Fact]
    public void A_blank_or_absent_cell_reads_as_null()
    {
        var document = CsvParser.Parse("tag,serial\r\nA-001,   \r\n").Value;

        document.Rows[0]["serial"].ShouldBeNull();
        document.Rows[0]["nope"].ShouldBeNull();
    }

    [Fact]
    public void Missing_required_columns_are_reported_once_for_the_whole_file()
    {
        var document = CsvParser.Parse("tag,serial\r\nA-001,SN1").Value;

        document.MissingColumns(["tag", "serial", "manufacturer", "model"])
            .ShouldBe(["manufacturer", "model"]);
    }

    [Fact]
    public void An_empty_file_is_a_validation_failure_not_an_exception()
    {
        var result = CsvParser.Parse(string.Empty);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe("csv.empty");
    }

    [Fact]
    public void A_ragged_row_is_rejected_and_names_the_row()
    {
        var result = CsvParser.Parse("tag,serial\r\nA-001\r\n");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("csv.row_length");
        result.Error.Message.ShouldContain("Row 2");
    }

    [Fact]
    public void A_duplicate_header_is_rejected()
    {
        var result = CsvParser.Parse("tag,Tag\r\nA-001,A-002");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("csv.duplicate_header");
    }

    [Fact]
    public void A_file_that_ends_inside_a_quoted_field_is_rejected()
    {
        var result = CsvParser.Parse("tag\r\n\"A-001");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("csv.unterminated_quote");
    }

    [Fact]
    public void Trailing_blank_lines_do_not_become_rows()
    {
        var document = CsvParser.Parse("tag\r\nA-001\r\n\r\n\r\n").Value;

        document.Rows.Count.ShouldBe(1);
    }
}
