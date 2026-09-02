using System.Text.Json.Serialization;

namespace Itms.Modules.Identity.Features.Users.ListUsers;

/// <summary>What the user directory is ordered by.</summary>
/// <remarks>
/// <para>
/// A closed set rather than a free-text column name, for the reason WP-1.5 and WP-2.3 each
/// wrote into their own sort enums: a sort that reaches the database as a string is either
/// a scan of an unindexed column or an injection question nobody wants to have to answer.
/// An unrecognised value is a 400 from model binding, not a silent fallback.
/// </para>
/// <para>
/// <b>There is deliberately no "department" or "location" ordering.</b> A user carries
/// those as bare identifiers — §3 rule 6 forbids the foreign key that would let this
/// module join Directory's tables — so ordering by the *name* of either is a question
/// Identity cannot answer and must not pretend to. The screen resolves both names from the
/// directory reads it already holds, and offers them as filters rather than as sorts.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<UserSort>))]
public enum UserSort
{
    /// <summary>
    /// The name shown throughout the product. The default, ascending — a directory is read
    /// alphabetically, which is also the order every picker in the system already lists
    /// people in.
    /// </summary>
    DisplayName,

    /// <summary>The address, for an administrator reconciling accounts against a mail domain.</summary>
    Email,

    /// <summary>When the account was created. Newest first by default: "who joined recently".</summary>
    CreatedAt,
}
