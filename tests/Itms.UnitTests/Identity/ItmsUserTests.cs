using Itms.Modules.Identity.Domain;

namespace Itms.UnitTests.Identity;

/// <summary>The user entity's own rules, which no database is needed to check.</summary>
public sealed class ItmsUserTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_user_is_active_and_stamped()
    {
        var actor = Guid.CreateVersion7();

        var user = ItmsUser.Create("tech", "tech@itms.local", "Toni Technician", Now, actor);

        user.IsActive.ShouldBeTrue();
        user.DisplayName.ShouldBe("Toni Technician");
        user.CreatedAt.ShouldBe(Now);
        user.CreatedBy.ShouldBe(actor);
        user.UpdatedAt.ShouldBe(Now);
        user.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Deactivating_and_reactivating_only_moves_the_flag()
    {
        var user = ItmsUser.Create("user", "user@itms.local", "Uma User", Now, actor: null);
        var actor = Guid.CreateVersion7();

        user.Deactivate(Now.AddDays(1), actor);

        user.IsActive.ShouldBeFalse();
        user.UpdatedAt.ShouldBe(Now.AddDays(1));
        user.UpdatedBy.ShouldBe(actor);
        // Invariant 9: deactivation removes nothing.
        user.DisplayName.ShouldBe("Uma User");
        user.Email.ShouldBe("user@itms.local");

        user.Reactivate(Now.AddDays(2), actor);
        user.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Placing_a_user_records_the_directory_ids_without_a_foreign_key()
    {
        var user = ItmsUser.Create("user", "user@itms.local", "Uma User", Now, actor: null);
        var department = Guid.CreateVersion7();
        var location = Guid.CreateVersion7();

        user.PlaceIn(department, location, Now.AddHours(1), actor: null);

        user.DepartmentId.ShouldBe(department);
        user.LocationId.ShouldBe(location);
    }

    [Theory]
    [InlineData("", "a@b.c", "Name")]
    [InlineData("user", "", "Name")]
    [InlineData("user", "a@b.c", " ")]
    public void Creating_a_user_without_the_required_fields_throws(string userName, string email, string displayName) =>
        Should.Throw<ArgumentException>(() => ItmsUser.Create(userName, email, displayName, Now, actor: null));
}
