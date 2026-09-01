namespace Itms.Modules.Assets.Domain;

/// <summary>
/// One asset status, reduced to the three things a lifecycle transition needs to know
/// about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the entity is handed these rather than reading the rows itself.</b> An asset
/// status is configurable reference data (WP-2.1), so <see cref="Asset"/> holds only an
/// <see cref="Asset.AssetStatusId"/> and cannot see its own code or name. A lifecycle
/// method needs all three: the <see cref="Code"/> to ask <see cref="AssetLifecycle"/>
/// whether the move is legal, the <see cref="Id"/> to write, and the <see cref="Name"/>
/// to put in the history entry as the display text it read at the time. The handler
/// resolves the rows and passes them in, which keeps the entity free of a database and
/// lets the unit suite exhaust it.
/// </para>
/// <para>
/// Every lifecycle method takes the asset's <em>current</em> status as one of these and
/// checks it against <see cref="Asset.AssetStatusId"/>. A mismatch is a programming error
/// — the caller resolved the wrong row — and throws rather than returning a failure.
/// </para>
/// </remarks>
/// <param name="Id">The status row's id.</param>
/// <param name="Code">Its immutable machine identifier. What <see cref="AssetLifecycle"/> reasons about.</param>
/// <param name="Name">Its display name right now, which is what a history entry records.</param>
public readonly record struct AssetStatusRef(Guid Id, string Code, string Name)
{
    /// <summary>Reduces a status row to the reference a transition needs.</summary>
    /// <param name="status">The status row.</param>
    /// <returns>The reference.</returns>
    public static AssetStatusRef Of(AssetStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return new AssetStatusRef(status.Id, status.Code, status.Name);
    }
}
