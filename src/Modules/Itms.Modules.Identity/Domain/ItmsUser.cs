using Microsoft.AspNetCore.Identity;

namespace Itms.Modules.Identity.Domain;

/// <summary>
/// A person who can sign in. Identity owns this row and no other module reads it
/// directly — cross-module reads go through <c>IUserLookup</c>, which carries no
/// credential state of any kind.
/// </summary>
/// <remarks>
/// The base class contributes the credential fields (password hash, security stamp,
/// lockout) and their setters have to stay public because <c>UserManager</c> writes
/// them. Everything this system adds keeps a private setter and is changed through an
/// intent-named method, per CONVENTIONS.md.
/// </remarks>
public sealed class ItmsUser : IdentityUser<Guid>
{
    private ItmsUser()
    {
        // EF Core and UserManager materialisation. DisplayName is non-null in the
        // database; the null-forgiving assignment is what tells the compiler that.
        DisplayName = null!;
    }

    /// <summary>The name shown on a ticket, a comment, or an asset history row.</summary>
    public string DisplayName { get; private set; }

    /// <summary>
    /// False once deactivated. A deactivated user keeps every ticket, comment, and
    /// asset history row they own (ARCHITECTURE.md §11 invariant 9) and simply stops
    /// being able to sign in — the cookie validator rejects them on their next request.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Their department, if set. A plain identifier rather than a foreign key: the
    /// Directory module owns that table, and §3 rule 6 forbids a foreign key across a
    /// module boundary.
    /// </summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>Their location, if set. A plain identifier, for the same reason as <see cref="DepartmentId"/>.</summary>
    public Guid? LocationId { get; private set; }

    /// <summary>When the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Who created it, or <see langword="null"/> when the system did — seeding, for instance.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>When the row was last changed (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Who last changed it, or <see langword="null"/> when the system did.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Creates an active user. The password is set separately, by <c>UserManager</c>.</summary>
    /// <param name="userName">The sign-in name. Unique.</param>
    /// <param name="email">The address notifications go to. Unique.</param>
    /// <param name="displayName">The name shown throughout the product.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is creating the user, or <see langword="null"/> for the system.</param>
    /// <returns>The new user, not yet persisted.</returns>
    public static ItmsUser Create(string userName, string email, string displayName, DateTimeOffset now, Guid? actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new ItmsUser
        {
            // v7 so the primary key is time-ordered and its index does not fragment.
            Id = Guid.CreateVersion7(),
            UserName = userName,
            Email = email,
            DisplayName = displayName,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = actor,
            UpdatedAt = now,
            UpdatedBy = actor,
        };
    }

    /// <summary>Changes the name shown throughout the product.</summary>
    /// <param name="displayName">The new display name.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is making the change.</param>
    public void Rename(string displayName, DateTimeOffset now, Guid? actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName;
        Touch(now, actor);
    }

    /// <summary>Records where the user sits. Both identifiers are optional until the directory is populated.</summary>
    /// <param name="departmentId">Their department, or <see langword="null"/>.</param>
    /// <param name="locationId">Their location, or <see langword="null"/>.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is making the change.</param>
    public void PlaceIn(Guid? departmentId, Guid? locationId, DateTimeOffset now, Guid? actor)
    {
        DepartmentId = departmentId;
        LocationId = locationId;
        Touch(now, actor);
    }

    /// <summary>Stops the user signing in. Deletes nothing.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is deactivating them.</param>
    public void Deactivate(DateTimeOffset now, Guid? actor)
    {
        IsActive = false;
        Touch(now, actor);
    }

    /// <summary>Lets the user sign in again.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is reactivating them.</param>
    public void Reactivate(DateTimeOffset now, Guid? actor)
    {
        IsActive = true;
        Touch(now, actor);
    }

    private void Touch(DateTimeOffset now, Guid? actor)
    {
        UpdatedAt = now;
        UpdatedBy = actor;
    }
}
