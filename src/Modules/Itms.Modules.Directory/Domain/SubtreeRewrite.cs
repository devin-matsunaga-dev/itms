namespace Itms.Modules.Directory.Domain;

/// <summary>
/// What a rename or a move did to a node, and therefore what has to happen to every
/// node beneath it.
/// </summary>
/// <remarks>
/// The entity changes itself and describes the consequence; the handler applies that
/// consequence to the subtree in one <c>UPDATE</c>. Keeping the description pure is
/// what lets the path arithmetic be unit-tested without a database, and keeping the
/// application in the handler is what stops a rename becoming one query per descendant.
/// </remarks>
/// <param name="OldPath">The node's materialised id path before the change. Every descendant's path starts with it.</param>
/// <param name="NewPath">Its id path after the change. Unchanged by a rename.</param>
/// <param name="OldFullPath">The node's display path before the change. Every descendant's display path starts with it.</param>
/// <param name="NewFullPath">Its display path after the change.</param>
/// <param name="DepthShift">How far the subtree moved up or down. Zero for a rename.</param>
public readonly record struct SubtreeRewrite(
    string OldPath,
    string NewPath,
    string OldFullPath,
    string NewFullPath,
    int DepthShift)
{
    /// <summary>True when nothing beneath the node needs rewriting.</summary>
    public bool IsNoop =>
        string.Equals(OldPath, NewPath, StringComparison.Ordinal)
        && string.Equals(OldFullPath, NewFullPath, StringComparison.Ordinal)
        && DepthShift == 0;
}
