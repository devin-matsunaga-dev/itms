using System.Collections.Frozen;

namespace Itms.Modules.Assets.Domain;

/// <summary>
/// Which move between asset lifecycle statuses is legal, written once.
/// </summary>
/// <remarks>
/// <para>
/// <b>Keyed on the code, because a status is a configurable row.</b> WP-2.1 made asset
/// statuses reference data an administrator can add to, rename, and retire, so there is no
/// enum to build a table over — but it also gave every status an immutable
/// <see cref="AssetStatusCode"/>, precisely so this table would have something stable to
/// reason about. A rename does not touch these rules; a new custom status is not in them
/// at all, which is the next paragraph.
/// </para>
/// <para>
/// <b>An unknown code has no legal destinations and is not terminal, and both halves
/// matter.</b> Nothing here can name a status somebody invents after this table was
/// written, so a lifecycle transition out of a custom status is refused with a message
/// naming it rather than being waved through into a state machine that does not describe
/// it. But <see cref="IsTerminal"/> answers <see langword="false"/> for the same code,
/// which keeps <em>assignment</em> — a different fact, governed by
/// <see cref="Asset.AssignTo"/> and not by this table — working for an asset in a custom
/// status. Otherwise adding a status would quietly make the equipment in it unissuable.
/// </para>
/// <para>
/// <b>The three terminal statuses are terminal at the human's direction</b> (WP-2.2):
/// <see cref="AssetStatusCode.Retired"/>, <see cref="AssetStatusCode.Lost"/> and
/// <see cref="AssetStatusCode.Disposed"/> have no way out, which is the reading WP-1.3
/// took of SPEC.md's silence for a cancelled ticket. The cost is that an asset retired by
/// mistake has no correction path in this package; putting one in would mean inventing
/// recovery semantics SPEC.md §3 does not define, and an explicit correction workflow can
/// add one deliberately later.
/// </para>
/// <para>
/// <b>This type decides nothing about who may make a move, or what else the move
/// writes.</b> It is a pure lookup over the codes. <see cref="Asset"/> owns the field
/// writes and the invariants that go with them; the endpoint owns the policy.
/// </para>
/// </remarks>
public static class AssetLifecycle
{
    /// <summary>
    /// Every legal destination, keyed by origin. Frozen because it is read on every
    /// transition and never rebuilt.
    /// </summary>
    /// <remarks>
    /// The edges are exactly the ones WP-2.2's five operations need, and no more.
    /// Assignment walks <c>in-stock → deployed</c> and a return walks it back;
    /// <see cref="Asset.SendForRepair"/> walks into <see cref="AssetStatusCode.Repair"/>
    /// from either working state; <see cref="Asset.ReturnToService"/> walks out of it to
    /// whichever state the asset's holder implies; <see cref="Asset.Retire"/> walks into
    /// <see cref="AssetStatusCode.Retired"/> from any of the three. <c>lost</c> and
    /// <c>disposed</c> are present as origins so <see cref="IsTerminal"/> knows them —
    /// nothing in this package moves an asset <em>into</em> either, because WP-2.2 names
    /// no operation that would.
    /// </remarks>
    private static readonly FrozenDictionary<string, FrozenSet<string>> LegalDestinations =
        new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [AssetStatusCode.InStock] = Set(AssetStatusCode.Deployed, AssetStatusCode.Repair, AssetStatusCode.Retired),
            [AssetStatusCode.Deployed] = Set(AssetStatusCode.InStock, AssetStatusCode.Repair, AssetStatusCode.Retired),
            [AssetStatusCode.Repair] = Set(AssetStatusCode.Deployed, AssetStatusCode.InStock, AssetStatusCode.Retired),
            [AssetStatusCode.Retired] = FrozenSet<string>.Empty,
            [AssetStatusCode.Lost] = FrozenSet<string>.Empty,
            [AssetStatusCode.Disposed] = FrozenSet<string>.Empty,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Whether an asset in <paramref name="from"/> may move to <paramref name="to"/>.
    /// </summary>
    /// <remarks>
    /// A move to the status the asset is already in is <em>not</em> legal. It is not a
    /// no-op: it would raise <c>AssetStatusChanged</c> and write a history entry saying an
    /// asset went from Repair to Repair. The caller asked for something that cannot happen,
    /// and a conflict is the honest answer — the same call WP-1.3 made for a ticket.
    /// </remarks>
    /// <param name="from">The code the asset currently carries.</param>
    /// <param name="to">The code being asked for.</param>
    /// <returns><see langword="true"/> when the move is one this table allows.</returns>
    public static bool CanTransition(string from, string to) =>
        LegalDestinations.TryGetValue(from, out var destinations) && destinations.Contains(to);

    /// <summary>
    /// Whether an asset in <paramref name="code"/> has reached the end of its life.
    /// </summary>
    /// <remarks>
    /// A code this table does not know is <see langword="false"/>, deliberately — see the
    /// remarks on this class. Only the three statuses named as terminal are terminal.
    /// </remarks>
    /// <param name="code">The status code to ask about.</param>
    /// <returns><see langword="true"/> for <c>retired</c>, <c>lost</c>, and <c>disposed</c>.</returns>
    public static bool IsTerminal(string code) =>
        LegalDestinations.TryGetValue(code, out var destinations) && destinations.Count == 0;

    /// <summary>
    /// Every destination legal from <paramref name="from"/>, for a caller that wants to
    /// offer them.
    /// </summary>
    /// <remarks>
    /// WP-2.6 renders the lifecycle actions and its done-criterion says an illegal one is
    /// absent rather than disabled in place. It should read this rather than restate the
    /// table in TypeScript.
    /// </remarks>
    /// <param name="from">The code the asset currently carries.</param>
    /// <returns>The legal destination codes, empty from a terminal or unknown status.</returns>
    public static IReadOnlyCollection<string> DestinationsFrom(string from) =>
        LegalDestinations.TryGetValue(from, out var destinations) ? destinations : FrozenSet<string>.Empty;

    private static FrozenSet<string> Set(params string[] destinations) =>
        destinations.ToFrozenSet(StringComparer.Ordinal);
}
