using Itms.Modules.Assets.Features.AssetTypes.CreateAssetType;
using Itms.Modules.Assets.Features.AssetTypes.GetAssetType;
using Itms.Modules.Assets.Features.AssetTypes.ListAssetTypes;
using Itms.Modules.Assets.Features.AssetTypes.SetAssetTypeActivation;
using Itms.Modules.Assets.Features.AssetTypes.UpdateAssetType;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Security;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Assets.Features.AssetTypes;

/// <summary>The asset-type endpoints, under <c>/api/v1/asset-types</c>.</summary>
/// <remarks>
/// Reads are open to any signed-in account, because the type appears wherever an asset is
/// displayed. Writes are Admin only: SPEC.md §13 puts "asset types and statuses" under
/// administration.
/// <para>
/// There is no <c>DELETE</c>. Retirement is the removal path — see
/// <c>SetAssetTypeActivationHandler</c>.
/// </para>
/// </remarks>
internal static class AssetTypeEndpoints
{
    /// <summary>The route prefix these endpoints hang off.</summary>
    public const string RoutePrefix = "/api/v1/asset-types";

    /// <summary>Maps the asset-type endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    public static void MapAssetTypes(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).WithTags("Asset types");

        MapReads(group);
        MapWrites(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        var reads = group.MapGroup(string.Empty).RequireAuthorization(ItmsPolicies.Authenticated);

        reads
            .MapGet("/", async (
                ListAssetTypesHandler handler,
                bool? includeInactive,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(includeInactive ?? false, PageRequest.Of(page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToOk();
            })
            .WithName("ListAssetTypes")
            .WithSummary("Lists asset types in picker order, optionally including retired ones.")
            .Produces<PagedResult<AssetTypeResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        reads
            .MapGet("/{id:guid}", async (
                Guid id,
                GetAssetTypeHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithName("GetAssetType")
            .WithSummary("Reads one asset type.")
            .Produces<AssetTypeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapWrites(RouteGroupBuilder group)
    {
        var writes = group
            .MapGroup(string.Empty)
            .RequireAuthorization(ItmsPolicies.Admin)
            // Cookie auth plus a state-changing verb is exactly the shape CSRF exploits;
            // CONVENTIONS.md's security floor requires the check on every one of them.
            .AddEndpointFilter<AntiforgeryFilter>();

        writes
            .MapPost("/", async (
                CreateAssetTypeRequest request,
                CreateAssetTypeHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
                return result.ToCreated(type => $"{RoutePrefix}/{type.Id}");
            })
            .WithValidation<CreateAssetTypeRequest>()
            .WithName("CreateAssetType")
            .WithSummary("Creates an asset type.")
            .Produces<AssetTypeResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        writes
            .MapPut("/{id:guid}", async (
                Guid id,
                UpdateAssetTypeRequest request,
                UpdateAssetTypeHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, request, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithValidation<UpdateAssetTypeRequest>()
            .WithName("UpdateAssetType")
            .WithSummary("Replaces an asset type's name, description, and order. Existing assets follow the rename.")
            .Produces<AssetTypeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        writes
            .MapPost("/{id:guid}/deactivate", async (
                Guid id,
                SetAssetTypeActivationHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, isActive: false, cancellationToken).ConfigureAwait(false);
                return result.ToNoContent();
            })
            .WithName("DeactivateAssetType")
            .WithSummary("Retires an asset type. Existing assets keep resolving it.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        writes
            .MapPost("/{id:guid}/reactivate", async (
                Guid id,
                SetAssetTypeActivationHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, isActive: true, cancellationToken).ConfigureAwait(false);
                return result.ToNoContent();
            })
            .WithName("ReactivateAssetType")
            .WithSummary("Brings a retired asset type back into use.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
