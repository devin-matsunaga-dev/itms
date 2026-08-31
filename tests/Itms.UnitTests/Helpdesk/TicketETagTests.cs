using Itms.Modules.Helpdesk.Features.Tickets;
using Microsoft.AspNetCore.Http;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// The entity-tag arithmetic behind ARCHITECTURE.md §6's optimistic concurrency.
/// </summary>
/// <remarks>
/// Pure header parsing, so it is asserted here rather than by starting a server. The
/// behaviour it decides is not cosmetic: getting <c>null</c> and "an empty set" the wrong
/// way round would turn every stale <c>If-Match</c> into a request that proceeds.
/// </remarks>
public sealed class TicketETagTests
{
    [Fact]
    public void A_tag_is_the_version_in_quotes()
    {
        TicketETag.For(3820471).ShouldBe("\"3820471\"");
    }

    [Fact]
    public void A_tag_is_strong_rather_than_weak()
    {
        // If-Match ignores a weak tag by definition, so a W/ prefix here would make the
        // whole precondition silently inert.
        TicketETag.For(1).ShouldNotStartWith("W/");
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(uint.MaxValue)]
    public void Every_version_round_trips_through_a_tag(uint version)
    {
        var request = RequestWith(TicketETag.For(version));

        var precondition = TicketETag.PreconditionFrom(request);

        precondition.ShouldNotBeNull();
        precondition.ShouldContain(version);
    }

    /// <summary>
    /// No header is no precondition, which is what keeps WP-1.3's callers working
    /// unchanged.
    /// </summary>
    [Fact]
    public void An_absent_header_states_no_precondition()
    {
        TicketETag.PreconditionFrom(new DefaultHttpContext().Request).ShouldBeNull();
    }

    /// <summary>
    /// "*" matches any existing representation. The handler has already found the row by
    /// the time it asks, so this is null — the same "no precondition to fail" answer.
    /// </summary>
    [Fact]
    public void A_wildcard_matches_anything_that_exists()
    {
        TicketETag.PreconditionFrom(RequestWith("*")).ShouldBeNull();
    }

    [Fact]
    public void A_list_of_tags_yields_every_version_in_it()
    {
        var request = RequestWith("\"1\", \"2\", \"3\"");

        var precondition = TicketETag.PreconditionFrom(request);

        precondition.ShouldNotBeNull();
        precondition.ShouldBe(new HashSet<uint> { 1, 2, 3 });
    }

    /// <summary>
    /// Present but unmatchable is an empty set, never null: null means "proceed", and a
    /// caller that sent a tag this endpoint cannot have issued must not proceed.
    /// </summary>
    [Theory]
    [InlineData("\"not-a-version\"")]
    [InlineData("\"-1\"")]
    [InlineData("\"99999999999999999999\"")]
    [InlineData("garbage")]
    public void A_tag_this_endpoint_could_not_have_issued_fails_the_precondition(string header)
    {
        var precondition = TicketETag.PreconditionFrom(RequestWith(header));

        precondition.ShouldNotBeNull();
        precondition.ShouldBeEmpty();
    }

    /// <summary>
    /// A weak tag cannot satisfy <c>If-Match</c>, so it must not be read as though it
    /// could.
    /// </summary>
    [Fact]
    public void A_weak_tag_does_not_satisfy_the_precondition()
    {
        var precondition = TicketETag.PreconditionFrom(RequestWith("W/\"7\""));

        precondition.ShouldNotBeNull();
        precondition.ShouldNotContain(7u);
    }

    private static HttpRequest RequestWith(string ifMatch)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = ifMatch;
        return context.Request;
    }
}
