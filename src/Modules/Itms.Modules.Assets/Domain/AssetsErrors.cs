using Itms.Platform.Results;

namespace Itms.Modules.Assets.Domain;

/// <summary>
/// Every failure this module can return, written once.
/// </summary>
/// <remarks>
/// The codes are part of the API surface — clients switch on them — so they live in one
/// file where a reword is visible in review rather than being spelled out at each call
/// site that can produce them.
/// </remarks>
internal static class AssetsErrors
{
    public static Error AssetTypeNotFound() =>
        Error.NotFound("assets.asset_type_not_found", "No such asset type.");

    public static Error DuplicateAssetTypeName(string name) =>
        Error.Conflict("assets.duplicate_asset_type_name", $"An asset type named '{name}' already exists.");

    public static Error AssetStatusNotFound() =>
        Error.NotFound("assets.asset_status_not_found", "No such asset status.");

    public static Error DuplicateAssetStatusName(string name) =>
        Error.Conflict("assets.duplicate_asset_status_name", $"An asset status named '{name}' already exists.");

    /// <summary>
    /// The code is the key everything other than a person reads, so a second row claiming
    /// one is refused rather than disambiguated.
    /// </summary>
    public static Error DuplicateAssetStatusCode(string code) =>
        Error.Conflict("assets.duplicate_asset_status_code", $"An asset status with the code '{code}' already exists.");

    /// <summary>The asset does not exist or has been soft-deleted.</summary>
    public static Error AssetNotFound() =>
        Error.NotFound("assets.asset_not_found", "No such asset.");

    /// <summary>
    /// Another asset already carries this tag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The 409 WP-2.1's done-criterion names. A conflict rather than a validation failure
    /// because the request is well formed and the tag is a perfectly good tag — it is the
    /// state of the world that refuses it, which is what <see cref="ErrorKind.Conflict"/>
    /// means.
    /// </para>
    /// <para>
    /// The message names the tag back, because the person hitting this is typically working
    /// through a box of equipment and needs to know <em>which</em> one of the ten they just
    /// entered collided.
    /// </para>
    /// </remarks>
    public static Error DuplicateAssetTag(string assetTag) =>
        Error.Conflict(
            "assets.duplicate_asset_tag",
            $"An asset with the tag '{assetTag}' already exists. An asset tag cannot be reused.");

    /// <summary>
    /// Another asset from the same manufacturer already carries this serial number.
    /// </summary>
    /// <remarks>
    /// Per manufacturer, not globally: two vendors numbering their products from 1 is
    /// ordinary, and refusing the second would be wrong. SPEC.md §3 and WP-2.1 both say
    /// "unique per manufacturer where present", so an asset with no serial, or a serial and
    /// no manufacturer, collides with nothing.
    /// </remarks>
    public static Error DuplicateSerialNumber(string manufacturer, string serialNumber) =>
        Error.Conflict(
            "assets.duplicate_serial_number",
            $"{manufacturer} already has an asset with the serial number '{serialNumber}'.");

    /// <summary>The asset type exists but has been retired, so no new asset may be classified as one.</summary>
    public static Error AssetTypeRetired() =>
        Error.Validation(
            "assets.asset_type_retired",
            "That asset type has been retired. Choose another.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["assetTypeId"] = ["That asset type has been retired. Choose another."],
            });

    /// <summary>The asset status exists but has been retired.</summary>
    public static Error AssetStatusRetired() =>
        Error.Validation(
            "assets.asset_status_retired",
            "That asset status has been retired. Choose another.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["assetStatusId"] = ["That asset status has been retired. Choose another."],
            });

    /// <summary>
    /// The request named no status and the deployment has no <c>in-stock</c> row to fall
    /// back to.
    /// </summary>
    /// <remarks>
    /// Reachable by a deployment whose seeder never ran — the gap recorded against WP-6.6 —
    /// or by an administrator who retired the seeded In Stock status. Worth its own message
    /// rather than a null-reference, because the fix is an administrative one and the
    /// person hitting it can act on it.
    /// </remarks>
    public static Error NoDefaultAssetStatus() =>
        Error.Validation(
            "assets.no_default_asset_status",
            "Choose a status: this deployment has no active 'In Stock' status to default to.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["assetStatusId"] = ["Choose a status: this deployment has no active 'In Stock' status to default to."],
            });

    /// <summary>No such department, as far as Directory is concerned.</summary>
    public static Error DepartmentNotFound() =>
        Error.Validation(
            "assets.department_not_found",
            "No such department.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["departmentId"] = ["No such department."],
            });

    /// <summary>No such location, as far as Directory is concerned.</summary>
    public static Error LocationNotFound() =>
        Error.Validation(
            "assets.location_not_found",
            "No such location.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["locationId"] = ["No such location."],
            });

    /// <summary>
    /// Somebody else changed the asset between this request reading it and writing it.
    /// The <c>xmin</c> token is what notices.
    /// </summary>
    public static Error AssetChangedConcurrently() =>
        Error.Conflict(
            "assets.asset_conflict",
            "The asset was changed by somebody else. Reload it and try again.");
}
