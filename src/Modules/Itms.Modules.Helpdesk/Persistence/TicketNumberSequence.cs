namespace Itms.Modules.Helpdesk.Persistence;

/// <summary>
/// The counter <c>TicketNumberGenerator</c> claims numbers from. One row, one column
/// that moves.
/// </summary>
/// <remarks>
/// <para>
/// A table rather than a PostgreSQL sequence, and that is the whole reason it exists. A
/// sequence never rolls back, so a creation that fails after taking a number burns it and
/// the numbering grows holes. WP-1.2 requires numbers with <em>no gaps</em>, which means
/// the claim has to be an ordinary row update inside the caller's transaction: it rolls
/// back with everything else the failed creation did.
/// </para>
/// <para>
/// The cost is stated rather than hidden. The claim takes a row lock held to commit, so
/// ticket creations serialise on this row. At helpdesk volume that is free; a system
/// needing thousands of tickets a second would want a sequence and would have to accept
/// gaps to get one.
/// </para>
/// <para>
/// It is keyed by name and not modelled as a <c>DbSet</c> on purpose: the generator's
/// single statement is the only way the counter moves, and there is no tracked entity for
/// anything else in the module to nudge.
/// </para>
/// </remarks>
internal sealed class TicketNumberSequence
{
    /// <summary>The table this maps to, unqualified.</summary>
    public const string TableName = "ticket_number_sequences";

    /// <summary>The table, schema-qualified, for the generator's statement.</summary>
    public const string QualifiedTableName = HelpdeskDbContext.SchemaName + "." + TableName;

    /// <summary>The counter tickets are numbered from. V1 has exactly one.</summary>
    public const string TicketSequence = "ticket";

    /// <summary>The longest a counter name may be.</summary>
    public const int NameMaxLength = 32;

    private TicketNumberSequence()
    {
        // EF Core materialisation; the column is non-null in the database.
        Name = null!;
    }

    /// <summary>Which counter this row is. The primary key.</summary>
    public string Name { get; private set; }

    /// <summary>The number most recently issued. The generator increments and returns it.</summary>
    public long NextValue { get; private set; }
}
