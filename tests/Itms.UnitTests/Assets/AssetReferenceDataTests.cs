using Itms.Modules.Assets.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.Assets;

/// <summary>
/// The two reference-data entities. They own the normalisation the unique indexes rely on,
/// the retirement that stands in for a delete this module deliberately does not have, and —
/// for a status — the immutability of the code WP-2.2's lifecycle methods key off.
/// </summary>
public sealed class AssetReferenceDataTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_new_type_is_active_and_stamped()
    {
        var type = AssetType.Create("Laptop", "Portable workstations.", 20, _clock.UtcNow, Actor);

        type.Id.ShouldNotBe(Guid.Empty);
        type.IsActive.ShouldBeTrue();
        type.SortOrder.ShouldBe(20);
        type.CreatedAt.ShouldBe(_clock.UtcNow);
        type.CreatedBy.ShouldBe(Actor);
    }

    /// <summary>
    /// The normalised column is what the unique index sits on, so "laptop" must not be
    /// creatable alongside "Laptop".
    /// </summary>
    [Fact]
    public void A_type_name_is_trimmed_and_normalised()
    {
        var type = AssetType.Create("  laptop ", null, 0, _clock.UtcNow, Actor);

        type.Name.ShouldBe("laptop");
        type.NormalizedName.ShouldBe("LAPTOP");
    }

    [Fact]
    public void Renaming_a_type_moves_the_normalised_name_with_it()
    {
        var type = AssetType.Create("Laptop", null, 0, _clock.UtcNow, Actor);

        _clock.Advance(TimeSpan.FromMinutes(5));
        type.Rename("Notebook", _clock.UtcNow, Actor);

        type.Name.ShouldBe("Notebook");
        type.NormalizedName.ShouldBe("NOTEBOOK");
        type.UpdatedAt.ShouldBe(_clock.UtcNow);
    }

    [Fact]
    public void Retiring_a_type_deletes_nothing_and_is_reversible()
    {
        var type = AssetType.Create("Fax Machine", null, 0, _clock.UtcNow, Actor);

        type.Deactivate(_clock.UtcNow, Actor);
        type.IsActive.ShouldBeFalse();

        type.Reactivate(_clock.UtcNow, Actor);
        type.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void A_new_status_carries_its_code_and_is_active()
    {
        var status = AssetStatus.Create("in-stock", "In Stock", "Held and not issued.", 10, _clock.UtcNow, Actor);

        status.Code.ShouldBe(AssetStatusCode.InStock);
        status.Name.ShouldBe("In Stock");
        status.NormalizedName.ShouldBe("IN STOCK");
        status.IsActive.ShouldBeTrue();
    }

    /// <summary>
    /// The code is the key everything other than a person reads, so it is lower-cased on
    /// the way in and its shape is narrow enough to live in a URL or a CSS class name.
    /// </summary>
    [Fact]
    public void A_status_code_is_lower_cased_and_trimmed()
    {
        var status = AssetStatus.Create("  In-Stock  ", "In Stock", null, 0, _clock.UtcNow, Actor);

        status.Code.ShouldBe("in-stock");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1nvalid")]
    [InlineData("has space")]
    [InlineData("has_underscore")]
    [InlineData("-leading-hyphen")]
    public void A_malformed_status_code_is_refused(string code) =>
        Should.Throw<ArgumentException>(
            () => AssetStatus.Create(code, "Whatever", null, 0, _clock.UtcNow, Actor));

    /// <summary>
    /// The validator asks this so a malformed code comes back as a 400 with a field
    /// message rather than as an exception escaping the entity.
    /// </summary>
    [Theory]
    [InlineData("in-stock", true)]
    [InlineData("deployed", true)]
    [InlineData("Deployed", true)]
    [InlineData("1nvalid", false)]
    [InlineData("has space", false)]
    [InlineData(null, false)]
    public void The_status_code_shape_check_agrees_with_the_entity(string? code, bool expected) =>
        AssetStatusCode.IsWellFormed(code).ShouldBe(expected);

    /// <summary>
    /// A rename must not move the code: WP-2.2's lifecycle methods and DESIGN.md's colours
    /// both key off it, and neither can depend on a value an administrator can edit.
    /// </summary>
    [Fact]
    public void Renaming_a_status_leaves_its_code_alone()
    {
        var status = AssetStatus.Create("repair", "Repair", null, 30, _clock.UtcNow, Actor);

        status.Rename("Being Fixed", _clock.UtcNow, Actor);

        status.Name.ShouldBe("Being Fixed");
        status.Code.ShouldBe(AssetStatusCode.Repair);
    }

    /// <summary>
    /// There is no method that moves a code — the immutability is structural, the same way
    /// an asset tag's is.
    /// </summary>
    [Fact]
    public void A_status_code_cannot_be_changed_after_creation()
    {
        var code = typeof(AssetStatus).GetProperty(nameof(AssetStatus.Code))!;

        code.SetMethod!.IsPublic.ShouldBeFalse();

        // Declared on the type only, and not a property accessor: object.GetHashCode is a
        // public method whose name contains "Code", and get_Code is the property being
        // read rather than a way to change it. Neither is a mutator.
        typeof(AssetStatus)
            .GetMethods()
            .Where(method =>
                method.IsPublic
                && !method.IsStatic
                && !method.IsSpecialName
                && method.DeclaringType == typeof(AssetStatus))
            .Select(method => method.Name)
            .ShouldNotContain(name => name.Contains("Code", StringComparison.Ordinal));
    }

    [Fact]
    public void A_blank_reference_name_is_refused()
    {
        Should.Throw<ArgumentException>(() => AssetType.Create("  ", null, 0, _clock.UtcNow, Actor));
        Should.Throw<ArgumentException>(() => AssetStatus.Create("lost", "  ", null, 0, _clock.UtcNow, Actor));
    }

    [Fact]
    public void A_reference_name_longer_than_the_column_is_refused() =>
        Should.Throw<ArgumentException>(
            () => AssetType.Create(new string('A', AssetType.NameMaxLength + 1), null, 0, _clock.UtcNow, Actor));
}
