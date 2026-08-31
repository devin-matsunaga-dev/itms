using Itms.Modules.Helpdesk.Domain;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// The shape of a ticket number. People quote these on the phone and paste them into
/// mail, so the rendering is a promise rather than a formatting detail.
/// </summary>
public sealed class TicketNumberTests
{
    [Fact]
    public void The_first_ticket_of_an_installation_is_TKT_0001() =>
        TicketNumber.Format(TicketNumber.FirstValue).ShouldBe("TKT-0001");

    [Theory]
    [InlineData(1, "TKT-0001")]
    [InlineData(42, "TKT-0042")]
    [InlineData(1052, "TKT-1052")]
    [InlineData(9999, "TKT-9999")]
    public void Numbers_are_padded_to_four_digits(long value, string expected) =>
        TicketNumber.Format(value).ShouldBe(expected);

    /// <summary>
    /// The padding is a floor, not a ceiling. A helpdesk that files its ten-thousandth
    /// ticket gets a wider number, not a wrapped or rejected one.
    /// </summary>
    [Theory]
    [InlineData(10_000, "TKT-10000")]
    [InlineData(1_234_567, "TKT-1234567")]
    public void Numbers_past_four_digits_simply_grow(long value, string expected) =>
        TicketNumber.Format(value).ShouldBe(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_counter_value_below_the_first_is_refused(long value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => TicketNumber.Format(value));

    [Theory]
    [InlineData("TKT-0001")]
    [InlineData("TKT-1")]
    [InlineData("TKT-1234567")]
    public void A_number_this_type_rendered_is_recognised(string value) =>
        TicketNumber.IsWellFormed(value).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("TKT-")]
    [InlineData("0001")]
    [InlineData("INC-0001")]
    [InlineData("tkt-0001")]
    [InlineData("TKT-00A1")]
    [InlineData("TKT-0001 ")]
    [InlineData(" TKT-0001")]
    public void Anything_else_is_not_a_ticket_number(string? value) =>
        TicketNumber.IsWellFormed(value).ShouldBeFalse();

    /// <summary>
    /// The column is <c>TicketNumber.MaxLength</c> wide, so a value that would not fit is
    /// not a number this system could have issued.
    /// </summary>
    [Fact]
    public void A_number_too_long_for_its_column_is_refused() =>
        TicketNumber.IsWellFormed(TicketNumber.Prefix + new string('9', TicketNumber.MaxLength))
            .ShouldBeFalse();
}
