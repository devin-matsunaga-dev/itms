using Itms.Modules.Assets.Features.AssetStatuses.CreateAssetStatus;
using Itms.Modules.Assets.Features.AssetStatuses.GetAssetStatus;
using Itms.Modules.Assets.Features.AssetStatuses.ListAssetStatuses;
using Itms.Modules.Assets.Features.AssetStatuses.SetAssetStatusActivation;
using Itms.Modules.Assets.Features.AssetStatuses.UpdateAssetStatus;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Security;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Assets.Features.AssetStatuses;

/// <summary>The asset-status endpoints, under <c>/api/v1/asset-statuses</c>.</summary>
/// <remarks>
/// Reads are open to any signed-in account; writes are Admin only, because SPEC.md §13 puts
/// "asset types and statuses" under administration. There is no <c>DELETE</c>.
/// </remarks>
internal static class AssetStatusEndpoints
{
    /// <summary>The route prefix these endpoints hang off.</summary>
    public const string RoutePrefix = "/api/v1/asset-statuses";

    /// <summary>Maps the asset-status endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    public static void MapAssetStatuses(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).WithTags("Asset statuses");

        MapReads(group);
        MapWrites(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        var reads = group.MapGroup(string.Empty).RequireAuthorization(ItmsPolicies.Authenticated);

        reads
            .MapGet("/", async (
                ListAssetStatusesHandler handler,
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
            .WithName("ListAssetStatuses")
            .WithSummary("Lists asset statuses in picker order, optionally including retired ones.")
            .Produces<PagedResult<AssetStatusResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        reads
            .MapGet("/{id:guid}", async (
                Guid id,
                GetAssetStatusHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithName("GetAssetStatus")
            .WithSummary("Reads one asset status.")
            .Produces<AssetStatusResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapWrites(RouteGroupBuilder group)
    {
        var writes = group
            .MapGroup(string.Empty)
            .RequireAuthorization(ItmsPolicies.Admin)
            .AddEndpointFilter<AntiforgeryFilter>();

        writes
            .MapPost("/", async (
                CreateAssetStatusRequest request,
                CreateAssetStatusHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
                return result.ToCreated(status => $"{RoutePrefix}/{status.Id}");
            })
            .WithValidation<CreateAssetStatusRequest>()
            .WithName("CreateAssetStatus")
            .WithSummary("Creates an asset status.")
            .Produces<AssetStatusResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        writes
            .MapPut("/{id:guid}", async (
                Guid id,
                UpdateAssetStatusRequest request,
                UpdateAssetStatusHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, request, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithValidation<UpdateAssetStatusRequest>()
            .WithName("UpdateAssetStatus")
            .WithSummary("Replaces an asset status's name, description, and order. The code is immutable.")
            .Produces<AssetStatusResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        writes
            .MapPost("/{id:guid}/deactivate", async (
                Guid id,
                SetAssetStatusActivationHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, isActive: false, cancellationToken).ConfigureAwait(false);
                return result.ToNoContent();
            })
            .WithName("DeactivateAssetStatus")
            .WithSummary("Retires an asset status. Existing assets keep resolving it.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        writes
            .MapPost("/{id:guid}/reactivate", async (
                Guid id,
                SetAssetStatusActivationHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, isActive: true, cancellationToken).ConfigureAwait(false);
                return result.ToNoContent();
            })
            .WithName("ReactivateAssetStatus")
            .WithSummary("Brings a retired asset status back into use.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
