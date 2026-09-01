namespace Itms.Modules.Directory.Features.Usage;

/// <summary>One module's count of references to a directory entry, as the API renders it.</summary>
/// <param name="EntityName">What was counted, in lower-case plural — <c>assets</c>, <c>tickets</c>, <c>users</c>.</param>
/// <param name="Count">How many of them reference the entry.</param>
public sealed record UsageCountResponse(string EntityName, int Count);
