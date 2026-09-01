using Itms.Modules.Assets.Features.Assets.CreateAsset;
using Itms.Modules.Assets.Features.Assets.GetAsset;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Security;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Assets.Features.Assets;

/// <summary>The asset endpoints, under <c>/api/v1/assets</c>.</summary>
/// <remarks>
/// <para>
/// Both routes are Technician-or-Admin. SPEC.md §14 puts the asset inventory on the
/// operational surface: a technician records and reads equipment, an end user has no
/// business enumerating it. Unlike a ticket, an asset has no requester-scoped view — the
/// "my kit" reading an end user might want is WP-2.5's user page, which answers a different
/// question through a different route.
/// </para>
/// <para>
/// <b>The list is WP-2.3</b>, with the filtering, search, sorting, and paging that package
/// names. The read below exists so the create's <c>Location</c> header points somewhere.
/// </para>
/// </remarks>
internal static class AssetEndpoints
{
    /// <summary>The route prefix these endpoints hang off.</summary>
    public const string RoutePrefix = "/api/v1/assets";

    /// <summary>Maps the asset endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    public static void MapAssets(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).WithTags("Assets");

        group
            .MapGet("/{id:guid}", async (
                Guid id,
                GetAssetHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .RequireAuthorization(ItmsPolicies.Technician)
            .WithName("GetAsset")
            .WithSummary("Reads one asset.")
            .Produces<AssetResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group
            .MapPost("/", async (
                CreateAssetRequest request,
                CreateAssetHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
                return result.ToCreated(asset => $"{RoutePrefix}/{asset.Id}");
            })
            .RequireAuthorization(ItmsPolicies.Technician)
            // Cookie auth plus a state-changing verb is exactly the shape CSRF exploits;
            // CONVENTIONS.md's security floor requires the check on every one of them.
            .AddEndpointFilter<AntiforgeryFilter>()
            .WithValidation<CreateAssetRequest>()
            .WithName("CreateAsset")
            .WithSummary("Records a new asset. The tag is unique and cannot be changed afterwards.")
            .Produces<AssetResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
