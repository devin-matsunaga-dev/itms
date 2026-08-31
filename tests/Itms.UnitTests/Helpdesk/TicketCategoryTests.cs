using Itms.Modules.Helpdesk.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// The ticket-category entity. It owns the normalisation the unique index relies on, and
/// the retirement that stands in for a delete this module deliberately does not have.
/// </summary>
public sealed class TicketCategoryTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_new_category_is_active_and_stamped()
    {
        var category = TicketCategory.Create("Network", "Connectivity and VPN.", 30, _clock.UtcNow, Actor);

        category.Id.ShouldNotBe(Guid.Empty);
        category.IsActive.ShouldBeTrue();
        category.SortOrder.ShouldBe(30);
        category.CreatedAt.ShouldBe(_clock.UtcNow);
        category.CreatedBy.ShouldBe(Actor);
        category.UpdatedAt.ShouldBe(_clock.UtcNow);
        category.UpdatedBy.ShouldBe(Actor);
    }

    /// <summary>
    /// The normalised column is what the unique index sits on, so "network" must not be
    /// creatable alongside "Network".
    /// </summary>
    [Fact]
    public void The_name_is_trimmed_and_normalised()
    {
        var category = TicketCategory.Create("  network ", null, 0, _clock.UtcNow, Actor);

        category.Name.ShouldBe("network");
        category.NormalizedName.ShouldBe("NETWORK");
    }

    [Fact]
    public void A_blank_description_is_stored_as_null() =>
        TicketCategory.Create("Network", "   ", 0, _clock.UtcNow, Actor).Description.ShouldBeNull();

    [Fact]
    public void A_blank_name_is_refused() =>
        Should.Throw<ArgumentException>(() => TicketCategory.Create("  ", null, 0, _clock.UtcNow, Actor));

    [Fact]
    public void An_over_long_name_is_refused() =>
        Should.Throw<ArgumentException>(() =>
            TicketCategory.Create(new string('x', TicketCategory.NameMaxLength + 1), null, 0, _clock.UtcNow, Actor));

    [Fact]
    public void An_over_long_description_is_refused() =>
        Should.Throw<ArgumentException>(() =>
            TicketCategory.Create("Network", new string('x', TicketCategory.DescriptionMaxLength + 1), 0, _clock.UtcNow, Actor));

    /// <summary>
    /// The id is what a ticket stores, so a rename must leave it alone — that identity is
    /// the whole mechanism by which existing tickets follow the new name.
    /// </summary>
    [Fact]
    public void A_rename_changes_the_name_and_never_the_id()
    {
        var category = TicketCategory.Create("Network", null, 30, _clock.UtcNow, Actor);
        var id = category.Id;

        _clock.Advance(TimeSpan.FromHours(1));
        category.Rename("  Networking  ", _clock.UtcNow, Actor);

        category.Id.ShouldBe(id);
        category.Name.ShouldBe("Networking");
        category.NormalizedName.ShouldBe("NETWORKING");
        category.UpdatedAt.ShouldBe(_clock.UtcNow);
    }

    [Fact]
    public void Retiring_and_reinstating_flips_the_flag_and_touches_the_row()
    {
        var category = TicketCategory.Create("Printer", null, 60, _clock.UtcNow, Actor);

        _clock.Advance(TimeSpan.FromMinutes(5));
        category.Deactivate(_clock.UtcNow, Actor);
        category.IsActive.ShouldBeFalse();
        category.UpdatedAt.ShouldBe(_clock.UtcNow);

        _clock.Advance(TimeSpan.FromMinutes(5));
        category.Reactivate(_clock.UtcNow, Actor);
        category.IsActive.ShouldBeTrue();
        category.UpdatedAt.ShouldBe(_clock.UtcNow);
    }

    /// <summary>
    /// WP-1.1's "deleting one in use is blocked", in its strongest form: there is nothing
    /// on the entity that removes it, so no handler can offer one.
    /// </summary>
    [Fact]
    public void The_entity_exposes_no_way_to_delete_itself() =>
        TicketPriorityTests.DeclaredMethodNames<TicketCategory>()
            .ShouldNotContain(name =>
                name.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Remove", StringComparison.OrdinalIgnoreCase));
}
