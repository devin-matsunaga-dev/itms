using Itms.Platform.Paging;

namespace Itms.UnitTests.Platform;

public sealed class PagingTests
{
    [Fact]
    public void Defaults_are_the_first_page_at_the_default_size()
    {
        var request = PageRequest.Of(null, null);

        request.Page.ShouldBe(1);
        request.PageSize.ShouldBe(PageRequest.DefaultPageSize);
        request.Skip.ShouldBe(0);
        request.Take.ShouldBe(PageRequest.DefaultPageSize);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void Page_is_clamped_to_at_least_one(int requested, int expected)
    {
        PageRequest.Of(requested, 10).Page.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(50, 50)]
    [InlineData(1_000_000, PageRequest.MaxPageSize)]
    public void Page_size_is_clamped_to_the_documented_maximum(int requested, int expected)
    {
        PageRequest.Of(1, requested).PageSize.ShouldBe(expected);
    }

    [Fact]
    public void Skip_reflects_the_clamped_page_and_size()
    {
        var request = PageRequest.Of(3, 500);

        request.PageSize.ShouldBe(PageRequest.MaxPageSize);
        request.Skip.ShouldBe(400);
    }

    [Fact]
    public void The_envelope_reports_the_size_that_was_applied_not_the_one_requested()
    {
        var request = PageRequest.Of(1, 5_000);

        var page = PagedResult.From<string>(["a"], total: 1, request);

        page.PageSize.ShouldBe(PageRequest.MaxPageSize);
        page.Page.ShouldBe(1);
        page.Total.ShouldBe(1);
        page.Items.ShouldBe(["a"]);
    }

    [Theory]
    [InlineData(0, 25, 0)]
    [InlineData(25, 25, 1)]
    [InlineData(26, 25, 2)]
    [InlineData(51, 25, 3)]
    public void Total_pages_rounds_up(int total, int pageSize, int expected)
    {
        PagedResult.From<int>([], total, PageRequest.Of(1, pageSize)).TotalPages.ShouldBe(expected);
    }

    [Fact]
    public void HasNextPage_is_false_on_the_last_page()
    {
        PagedResult.From<int>([], total: 30, PageRequest.Of(1, 25)).HasNextPage.ShouldBeTrue();
        PagedResult.From<int>([], total: 30, PageRequest.Of(2, 25)).HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void An_empty_page_still_reports_the_requested_position()
    {
        var page = PagedResult.Empty<string>(PageRequest.Of(4, 10));

        page.Items.ShouldBeEmpty();
        page.Total.ShouldBe(0);
        page.Page.ShouldBe(4);
        page.PageSize.ShouldBe(10);
        page.HasNextPage.ShouldBeFalse();
    }
}
