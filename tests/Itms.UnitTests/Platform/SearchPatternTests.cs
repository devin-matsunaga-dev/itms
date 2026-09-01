using Itms.Platform.Data;

namespace Itms.UnitTests.Platform;

/// <summary>
/// The <c>LIKE</c>/<c>ILIKE</c> escaping three modules now depend on.
/// </summary>
/// <remarks>
/// It had no direct test while it lived in Directory as <c>LikePattern</c> — it was proved
/// only through the endpoints that used it. Hoisting it into the shared kernel at WP-1.12
/// changed that: Directory's filters, Identity's user picker, and the ticket queue's
/// search all reach it now, and getting the escaping wrong is silent. The query still
/// runs; it just returns rows nobody asked for, or scans a table it should have indexed
/// into.
/// </remarks>
public sealed class SearchPatternTests
{
    [Fact]
    public void A_plain_term_is_wrapped_in_wildcards()
    {
        SearchPattern.Containing("printer").ShouldBe("%printer%");
    }

    [Fact]
    public void Surrounding_whitespace_is_dropped_before_the_wildcards_go_on()
    {
        // Otherwise "%  printer  %" matches nothing a person expects.
        SearchPattern.Containing("  printer  ").ShouldBe("%printer%");
    }

    [Fact]
    public void A_percent_in_the_term_is_escaped_rather_than_left_as_a_wildcard()
    {
        // Unescaped, "%" would match every row in the table.
        SearchPattern.Containing("50%").ShouldBe(@"%50\%%");
    }

    [Fact]
    public void An_underscore_is_escaped_too()
    {
        // "_" is LIKE's single-character wildcard, which is the easier one to forget.
        SearchPattern.Containing("a_b").ShouldBe(@"%a\_b%");
    }

    [Fact]
    public void A_backslash_is_escaped_first_so_the_others_are_not_doubled()
    {
        // Order matters: escaping the backslash last would also escape the backslashes
        // this method had just introduced, and the pattern would stop matching.
        SearchPattern.Containing(@"a\b").ShouldBe(@"%a\\b%");
    }

    [Fact]
    public void A_term_of_nothing_but_wildcards_matches_them_literally()
    {
        SearchPattern.Containing("%_").ShouldBe(@"%\%\_%");
    }

    [Fact]
    public void An_empty_term_is_the_pattern_that_matches_anything()
    {
        // The caller decides whether an empty search is a filter at all; this only says
        // what the pattern would be. Every caller in the system checks first.
        SearchPattern.Containing(string.Empty).ShouldBe("%%");
    }

    [Fact]
    public void A_prefix_pattern_is_anchored_at_the_start()
    {
        SearchPattern.StartingWith("/1/2/").ShouldBe("/1/2/%");
    }

    [Fact]
    public void A_prefix_pattern_escapes_wildcards_but_does_not_trim()
    {
        // Unlike a search term, a path is a stored value: trimming it would silently
        // select a different subtree.
        SearchPattern.StartingWith(" a_b").ShouldBe(@" a\_b%");
    }

    [Fact]
    public void The_escape_character_is_the_one_the_patterns_are_written_with()
    {
        SearchPattern.Escape.ShouldBe("\\");
    }

    [Fact]
    public void Neither_builder_accepts_null()
    {
        Should.Throw<ArgumentNullException>(() => SearchPattern.Containing(null!));
        Should.Throw<ArgumentNullException>(() => SearchPattern.StartingWith(null!));
    }
}
