using Itms.Modules.Assets.Domain;

namespace Itms.UnitTests.Assets;

/// <summary>
/// The lifecycle table. It is enumerable on purpose, so every ordered pair of the six
/// codes SPEC.md §3 names is asserted here rather than the handful somebody remembered to
/// write a test for — the same reason WP-1.3 asserted all forty-nine ticket pairs.
/// </summary>
public sealed class AssetLifecycleTests
{
    /// <summary>The six codes SPEC.md §3 names, in lifecycle order.</summary>
    private static readonly string[] Codes =
    [
        AssetStatusCode.InStock,
        AssetStatusCode.Deployed,
        AssetStatusCode.Repair,
        AssetStatusCode.Retired,
        AssetStatusCode.Lost,
        AssetStatusCode.Disposed,
    ];

    /// <summary>
    /// Exactly the edges the five operations need, written out as data so the assertion and
    /// the table cannot drift into agreeing with each other by construction.
    /// </summary>
    private static readonly HashSet<(string From, string To)> Legal =
    [
        (AssetStatusCode.InStock, AssetStatusCode.Deployed),
        (AssetStatusCode.InStock, AssetStatusCode.Repair),
        (AssetStatusCode.InStock, AssetStatusCode.Retired),
        (AssetStatusCode.Deployed, AssetStatusCode.InStock),
        (AssetStatusCode.Deployed, AssetStatusCode.Repair),
        (AssetStatusCode.Deployed, AssetStatusCode.Retired),
        (AssetStatusCode.Repair, AssetStatusCode.Deployed),
        (AssetStatusCode.Repair, AssetStatusCode.InStock),
        (AssetStatusCode.Repair, AssetStatusCode.Retired),
    ];

    [Fact]
    public void Every_ordered_pair_of_codes_answers_the_table()
    {
        foreach (var from in Codes)
        {
            foreach (var to in Codes)
            {
                AssetLifecycle
                    .CanTransition(from, to)
                    .ShouldBe(Legal.Contains((from, to)), $"{from} -> {to}");
            }
        }
    }

    /// <summary>
    /// A move to the status the asset is already in is refused. It is not a no-op: it would
    /// raise <c>AssetStatusChanged</c> and write a history line saying an asset went from
    /// Repair to Repair.
    /// </summary>
    [Fact]
    public void A_status_cannot_transition_to_itself()
    {
        foreach (var code in Codes)
        {
            AssetLifecycle.CanTransition(code, code).ShouldBeFalse(code);
        }
    }

    [Theory]
    [InlineData(AssetStatusCode.Retired)]
    [InlineData(AssetStatusCode.Lost)]
    [InlineData(AssetStatusCode.Disposed)]
    public void The_three_end_of_life_statuses_are_terminal(string code)
    {
        AssetLifecycle.IsTerminal(code).ShouldBeTrue();
        AssetLifecycle.DestinationsFrom(code).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(AssetStatusCode.InStock)]
    [InlineData(AssetStatusCode.Deployed)]
    [InlineData(AssetStatusCode.Repair)]
    public void The_three_working_statuses_are_not_terminal(string code)
    {
        AssetLifecycle.IsTerminal(code).ShouldBeFalse();
        AssetLifecycle.DestinationsFrom(code).ShouldNotBeEmpty();
    }

    /// <summary>
    /// The half of the unknown-code rule that keeps a custom status usable. An
    /// administrator can add "On Loan"; the equipment in it must still be issuable, because
    /// assignment is governed by <see cref="AssetLifecycle.IsTerminal"/> and not by the
    /// transition table.
    /// </summary>
    [Fact]
    public void A_status_this_table_does_not_know_is_not_terminal()
    {
        AssetLifecycle.IsTerminal("on-loan").ShouldBeFalse();
    }

    /// <summary>
    /// The other half. Nothing here can describe a lifecycle move out of a status invented
    /// after the table was written, so the move is refused rather than waved through.
    /// </summary>
    [Fact]
    public void A_status_this_table_does_not_know_has_no_legal_destinations()
    {
        AssetLifecycle.DestinationsFrom("on-loan").ShouldBeEmpty();

        foreach (var to in Codes)
        {
            AssetLifecycle.CanTransition("on-loan", to).ShouldBeFalse(to);
            AssetLifecycle.CanTransition(to, "on-loan").ShouldBeFalse(to);
        }
    }

    /// <summary>
    /// The codes are compared exactly. <c>AssetStatusCode.Normalize</c> lower-cases on the
    /// way in, so a stored code is always lower case and a case-insensitive comparison here
    /// would only ever hide a bug somewhere else.
    /// </summary>
    [Fact]
    public void Codes_are_matched_case_sensitively()
    {
        AssetLifecycle.CanTransition("IN-STOCK", AssetStatusCode.Deployed).ShouldBeFalse();
    }
}
