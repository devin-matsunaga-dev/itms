using Itms.Modules.Directory.Features.Usage;

namespace Itms.UnitTests.DirectoryModule;

/// <summary>
/// The sentence an administrator reads when a delete is refused. It is the only thing
/// that tells them where to go and look, so a zero that leaks into it, or a "1 users",
/// is a defect in the message rather than a typo.
/// </summary>
public sealed class UsageBreakdownTests
{
    [Fact]
    public void Nothing_referenced_renders_as_nothing() =>
        UsageBreakdown.Describe([new UsageCountResponse("assets", 0), new UsageCountResponse("users", 0)])
            .ShouldBeEmpty();

    [Fact]
    public void An_empty_breakdown_renders_as_nothing() =>
        UsageBreakdown.Describe([]).ShouldBeEmpty();

    [Fact]
    public void One_reference_renders_without_a_conjunction() =>
        UsageBreakdown.Describe([new UsageCountResponse("assets", 3)]).ShouldBe("3 assets");

    /// <summary>
    /// A module reporting zero is dropped. Reading "0 tickets" beside a real count sends
    /// an administrator to look in the module that is not the problem.
    /// </summary>
    [Fact]
    public void A_module_reporting_zero_is_left_out()
    {
        UsageBreakdown.Describe([
            new UsageCountResponse("assets", 3),
            new UsageCountResponse("tickets", 0),
            new UsageCountResponse("users", 1),
        ]).ShouldBe("3 assets and 1 user");
    }

    [Fact]
    public void Three_references_are_comma_separated_with_a_final_and()
    {
        UsageBreakdown.Describe([
            new UsageCountResponse("assets", 3),
            new UsageCountResponse("tickets", 2),
            new UsageCountResponse("users", 7),
        ]).ShouldBe("3 assets, 2 tickets and 7 users");
    }

    [Theory]
    [InlineData(1, "1 user")]
    [InlineData(2, "2 users")]
    public void A_count_of_one_is_singular(int count, string expected) =>
        UsageBreakdown.Describe([new UsageCountResponse("users", count)]).ShouldBe(expected);

    /// <summary>
    /// A name that is already singular is left alone rather than having a letter removed.
    /// Nothing registers one today; this is the guard for the counter that does.
    /// </summary>
    [Fact]
    public void A_name_that_does_not_end_in_s_is_not_trimmed() =>
        UsageBreakdown.Describe([new UsageCountResponse("equipment", 1)]).ShouldBe("1 equipment");
}
