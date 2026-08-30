using Itms.Modules.Directory.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.DirectoryModule;

/// <summary>
/// The department entity. Small, but it owns the normalisation the unique indexes rely
/// on and the retirement that stands in for a delete.
/// </summary>
public sealed class DepartmentTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_new_department_is_active_and_stamped()
    {
        var department = Department.Create("Information Technology", "IT", "Runs the helpdesk.", _clock.UtcNow, Actor);

        department.Id.ShouldNotBe(Guid.Empty);
        department.IsActive.ShouldBeTrue();
        department.CreatedAt.ShouldBe(_clock.UtcNow);
        department.CreatedBy.ShouldBe(Actor);
        department.UpdatedAt.ShouldBe(_clock.UtcNow);
        department.UpdatedBy.ShouldBe(Actor);
    }

    /// <summary>
    /// The normalised columns are what the unique indexes sit on, so "finance" must not
    /// be creatable alongside "Finance".
    /// </summary>
    [Fact]
    public void Name_and_code_are_trimmed_and_normalised()
    {
        var department = Department.Create("  finance ", " fin ", null, _clock.UtcNow, Actor);

        department.Name.ShouldBe("finance");
        department.NormalizedName.ShouldBe("FINANCE");
        department.Code.ShouldBe("fin");
        department.NormalizedCode.ShouldBe("FIN");
    }

    [Fact]
    public void A_blank_code_or_description_is_stored_as_null()
    {
        var department = Department.Create("Finance", "   ", "  ", _clock.UtcNow, Actor);

        department.Code.ShouldBeNull();
        department.NormalizedCode.ShouldBeNull();
        department.Description.ShouldBeNull();
    }

    [Fact]
    public void A_blank_name_is_refused() =>
        Should.Throw<ArgumentException>(() => Department.Create("  ", null, null, _clock.UtcNow, Actor));

    [Theory]
    [InlineData(Department.NameMaxLength + 1)]
    public void An_over_long_name_is_refused(int length) =>
        Should.Throw<ArgumentException>(() =>
            Department.Create(new string('x', length), null, null, _clock.UtcNow, Actor));

    [Fact]
    public void Renaming_updates_the_normalised_name_and_the_stamp()
    {
        var department = Department.Create("Finance", null, null, _clock.UtcNow, Actor);
        var editor = Guid.CreateVersion7();

        _clock.Advance(TimeSpan.FromHours(2));
        department.Rename("  Finance & Procurement ", _clock.UtcNow, editor);

        department.Name.ShouldBe("Finance & Procurement");
        department.NormalizedName.ShouldBe("FINANCE & PROCUREMENT");
        department.UpdatedAt.ShouldBe(_clock.UtcNow);
        department.UpdatedBy.ShouldBe(editor);
        department.CreatedBy.ShouldBe(Actor);
    }

    [Fact]
    public void Setting_a_null_code_clears_both_columns()
    {
        var department = Department.Create("Finance", "FIN", null, _clock.UtcNow, Actor);

        department.SetCode(null, _clock.UtcNow, Actor);

        department.Code.ShouldBeNull();
        department.NormalizedCode.ShouldBeNull();
    }

    /// <summary>
    /// Retirement is what stands in for a delete, so it must leave every field a
    /// historical reference needs intact.
    /// </summary>
    [Fact]
    public void Retiring_flips_the_flag_and_keeps_everything_else()
    {
        var department = Department.Create("Finance", "FIN", "Accounting.", _clock.UtcNow, Actor);

        _clock.Advance(TimeSpan.FromDays(400));
        department.Deactivate(_clock.UtcNow, Actor);

        department.IsActive.ShouldBeFalse();
        department.Name.ShouldBe("Finance");
        department.Code.ShouldBe("FIN");
        department.Description.ShouldBe("Accounting.");
        department.UpdatedAt.ShouldBe(_clock.UtcNow);

        department.Reactivate(_clock.UtcNow, Actor);
        department.IsActive.ShouldBeTrue();
    }
}
