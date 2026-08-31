using Itms.Modules.Helpdesk.Domain;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// The code rule, asserted through the predicate the validator calls as well as the
/// normaliser the entity calls — the two must agree, or a request passes validation and
/// then throws inside the handler.
/// </summary>
public sealed class PriorityCodeTests
{
    [Theory]
    [InlineData("critical")]
    [InlineData("high")]
    [InlineData("very-high")]
    [InlineData("p1")]
    [InlineData("  CRITICAL  ")]
    public void A_well_formed_code_passes_both_the_predicate_and_the_normaliser(string code)
    {
        PriorityCode.IsWellFormed(code).ShouldBeTrue();
        PriorityCode.Normalize(code, nameof(code)).ShouldBe(code.Trim().ToLowerInvariant());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-leading")]
    [InlineData("1numeric")]
    [InlineData("has space")]
    [InlineData("has_underscore")]
    [InlineData("has.dot")]
    [InlineData("héllo")]
    public void A_malformed_code_is_rejected_by_both(string? code)
    {
        PriorityCode.IsWellFormed(code).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => PriorityCode.Normalize(code!, nameof(code)));
    }

    [Fact]
    public void An_over_long_code_is_rejected_by_both()
    {
        var code = new string('a', PriorityCode.MaxLength + 1);

        PriorityCode.IsWellFormed(code).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => PriorityCode.Normalize(code, nameof(code)));
    }

    [Fact]
    public void A_code_at_the_length_limit_is_accepted() =>
        PriorityCode.IsWellFormed(new string('a', PriorityCode.MaxLength)).ShouldBeTrue();
}
