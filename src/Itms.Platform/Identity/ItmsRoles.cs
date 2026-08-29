namespace Itms.Platform.Identity;

/// <summary>
/// The three roles the system has, and the only three it will have in V1
/// (ARCHITECTURE.md §7). Named here so no module spells a role as a string literal
/// and no typo can silently widen access.
/// </summary>
public static class ItmsRoles
{
    /// <summary>Full administrative access, including configuration and user management.</summary>
    public const string Admin = "Admin";

    /// <summary>Helpdesk and asset operations across all tickets and devices.</summary>
    public const string Technician = "Technician";

    /// <summary>An end user: their own tickets and nothing else.</summary>
    public const string User = "User";

    /// <summary>All roles, for iteration in seeding and tests.</summary>
    public static IReadOnlyList<string> All { get; } = [Admin, Technician, User];
}
