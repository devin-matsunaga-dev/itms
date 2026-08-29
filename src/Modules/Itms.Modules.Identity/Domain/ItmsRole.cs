using Microsoft.AspNetCore.Identity;

namespace Itms.Modules.Identity.Domain;

/// <summary>
/// One of the three roles the system has (ARCHITECTURE.md §7, SPEC.md §14). Roles are
/// seeded, not created at runtime: granular RBAC is in the Defer column of SPEC.md, and
/// an endpoint that could mint a role would be the first step toward it.
/// </summary>
public sealed class ItmsRole : IdentityRole<Guid>
{
    private ItmsRole()
    {
        Description = null!;
    }

    /// <summary>What the role is allowed to do, in one sentence, for the admin screen.</summary>
    public string Description { get; private set; }

    /// <summary>When the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Who created it, or <see langword="null"/> when the system did.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>When the row was last changed (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Who last changed it, or <see langword="null"/> when the system did.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Creates a role.</summary>
    /// <param name="name">One of the constants on <c>ItmsRoles</c>.</param>
    /// <param name="description">What the role is allowed to do.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The new role, not yet persisted.</returns>
    public static ItmsRole Create(string name, string description, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new ItmsRole
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Description = description,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
