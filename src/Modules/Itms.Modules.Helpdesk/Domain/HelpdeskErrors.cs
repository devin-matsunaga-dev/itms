using Itms.Platform.Results;

namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// Every failure this module can return, written once.
/// </summary>
/// <remarks>
/// The codes are part of the API surface — clients switch on them — so they live in one
/// file where a reword is visible in review rather than being spelled out at each call
/// site that can produce them.
/// </remarks>
internal static class HelpdeskErrors
{
    public static Error CategoryNotFound() =>
        Error.NotFound("helpdesk.category_not_found", "No such ticket category.");

    public static Error DuplicateCategoryName(string name) =>
        Error.Conflict("helpdesk.duplicate_category_name", $"A ticket category named '{name}' already exists.");

    public static Error PriorityNotFound() =>
        Error.NotFound("helpdesk.priority_not_found", "No such ticket priority.");

    public static Error DuplicatePriorityName(string name) =>
        Error.Conflict("helpdesk.duplicate_priority_name", $"A ticket priority named '{name}' already exists.");

    /// <summary>
    /// The code is the key everything other than a person reads, so a second row claiming
    /// one is refused rather than disambiguated.
    /// </summary>
    public static Error DuplicatePriorityCode(string code) =>
        Error.Conflict("helpdesk.duplicate_priority_code", $"A ticket priority with the code '{code}' already exists.");
}
