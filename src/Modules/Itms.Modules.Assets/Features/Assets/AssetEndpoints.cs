using Itms.Modules.Assets.Features.AssetHistory;
using Itms.Modules.Assets.Features.AssetHistory.ListAssetHistory;
using Itms.Modules.Assets.Features.Assets.AssignAsset;
using Itms.Modules.Assets.Features.Assets.CreateAsset;
using Itms.Modules.Assets.Features.Assets.GetAsset;
using Itms.Modules.Assets.Features.Assets.RetireAsset;
using Itms.Modules.Assets.Features.Assets.ReturnAssetToService;
using Itms.Modules.Assets.Features.Assets.SendAssetForRepair;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Itms.Platform.Security;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MinimalApi = Microsoft.AspNetCore.Http.Results;

namespace Itms.Modules.Assets.Features.Assets;

/// <summary>The asset endpoints, under <c>/api/v1/assets</c>.</summary>
/// <remarks>
/// <para>
/// Every route here is Technician-or-Admin. SPEC.md §14 puts the asset inventory on the
/// operational surface: a technician records, reads, issues and retires equipment, and an
/// end user has no business enumerating it. Unlike a ticket, an asset has no
/// requester-scoped view — the "my kit" reading an end user might want is WP-2.5's user
/// page, which answers a different question through a different route. Note that this is
/// about who may <em>perform</em> an assignment; equipment is issued <em>to</em> anybody,
/// which is why <c>AssignAssetHandler</c> asks Identity for no role.
/// </para>
/// <para>
/// <b>The list is WP-2.3</b>, with the filtering, search, sorting, and paging that package
/// names. The single-asset read exists so the create's <c>Location</c> header points
/// somewhere, and the history read exists because a timeline nothing can read cannot be
/// verified by a human.
/// </para>
/// <para>
/// <b>The four writes are the five lifecycle operations SPEC.md §3 names.</b> Assignment
/// and transfer share a route because they are the same fact — who holds this — with and
/// without a from-value. Each answers with the asset and an <c>ETag</c>, and each honours
/// an <c>If-Match</c>: two technicians looking at the same asset screen must not be able
/// to overwrite each other silently.
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

        MapReads(group);
        MapWrites(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        group
            .MapGet("/{id:guid}", async (
                Guid id,
                GetAssetHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return WithETag(result, context);
            })
            .RequireAuthorization(ItmsPolicies.Technician)
            .WithName("GetAsset")
            .WithSummary("Reads one asset.")
            .WithDescription(
                "Carries an ETag naming the asset's current version. Send it back as If-Match on a "
                + "lifecycle call to be told the asset has moved before the change is attempted.")
            .Produces<AssetResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group
            .MapGet("/{id:guid}/history", async (
                Guid id,
                ListAssetHistoryHandler handler,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(id, PageRequest.Of(page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToOk();
            })
            .RequireAuthorization(ItmsPolicies.Technician)
            .WithName("ListAssetHistory")
            .WithSummary("Reads an asset's history, newest first.")
            .WithDescription(
                "Entries sharing an occurredAt came from one operation — issuing equipment out of "
                + "stock moves the holder and the status — and are meant to be read together, in "
                + "sequence order.")
            .Produces<PagedResult<AssetHistoryEntryResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapWrites(RouteGroupBuilder group)
    {
        group
            .MapPost("/", async (
                CreateAssetRequest request,
                CreateAssetHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);

                if (result.IsFailure)
                {
                    return ProblemDetailsMapper.ToProblem(result.Error!);
                }

                SetETag(context, result.Value.Version);
                return MinimalApi.Created($"{RoutePrefix}/{result.Value.Response.Id}", result.Value.Response);
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

        group
            .MapPost("/{id:guid}/assignments", async (
                Guid id,
                AssignAssetRequest request,
                AssignAssetHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(id, request, AssetETag.PreconditionFrom(context.Request), cancellationToken)
                    .ConfigureAwait(false);

                return WithETag(result, context);
            })
            .RequireAuthorization(ItmsPolicies.Technician)
            .AddEndpointFilter<AntiforgeryFilter>()
            .WithValidation<AssignAssetRequest>()
            .WithName("AssignAsset")
            .WithSummary("Issues an asset to somebody, transfers it, or takes it back.")
            .WithDescription(
                "Naming a user issues or transfers the asset; omitting one takes it back. Issuing "
                + "an in-stock asset also deploys it, and returning a deployed one puts it back in "
                + "stock — a transfer between two people moves nobody's lifecycle status. Send the "
                + "asset's ETag as If-Match to be refused with 412 if it has moved since you read it.")
            .Produces<AssetResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        MapLifecycle<SendAssetForRepairHandler>(
            group,
            "/{id:guid}/repairs",
            "SendAssetForRepair",
            "Sends an asset away to be fixed.",
            "The holder is kept: a machine at the vendor is still issued to whoever had it, which "
                + "is what tells the return-to-service call where to put it back.",
            (handler, id, request, versions, token) => handler.HandleAsync(id, request, versions, token));

        MapLifecycle<ReturnAssetToServiceHandler>(
            group,
            "/{id:guid}/returns-to-service",
            "ReturnAssetToService",
            "Brings an asset back from repair.",
            "It goes back to deployed if somebody still holds it, and into stock if nobody does.",
            (handler, id, request, versions, token) => handler.HandleAsync(id, request, versions, token));

        MapLifecycle<RetireAssetHandler>(
            group,
            "/{id:guid}/retirements",
            "RetireAsset",
            "Takes an asset out of service and keeps it on the books.",
            "Retiring also releases whoever holds it, so a deployed asset records both the release "
                + "and the transition. Retired is terminal: no lifecycle call is accepted afterwards.",
            (handler, id, request, versions, token) => handler.HandleAsync(id, request, versions, token));
    }

    /// <summary>
    /// Maps one of the three lifecycle routes that name no other party.
    /// </summary>
    /// <remarks>
    /// They differ in their route, their name, their prose, and the handler they resolve —
    /// and in nothing else. Written out three times, the third copy is where a missing
    /// <c>AntiforgeryFilter</c> or a forgotten 412 goes unnoticed, which is precisely the
    /// kind of omission CONVENTIONS.md's security floor cannot afford.
    /// </remarks>
    private static void MapLifecycle<THandler>(
        RouteGroupBuilder group,
        string route,
        string name,
        string summary,
        string description,
        Func<THandler, Guid, AssetLifecycleRequest, IReadOnlySet<uint>?, CancellationToken, Task<Result<AssetDetail>>> handle)
        where THandler : notnull
    {
        group
            .MapPost(route, async (
                Guid id,
                AssetLifecycleRequest request,
                THandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handle(
                        handler,
                        id,
                        request,
                        AssetETag.PreconditionFrom(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false);

                return WithETag(result, context);
            })
            .RequireAuthorization(ItmsPolicies.Technician)
            .AddEndpointFilter<AntiforgeryFilter>()
            .WithValidation<AssetLifecycleRequest>()
            .WithName(name)
            .WithSummary(summary)
            .WithDescription(
                description
                + " Send the asset's ETag as If-Match to be refused with 412 if it has moved since "
                + "you read it.")
            .Produces<AssetResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
    }

    /// <summary>
    /// 200 with the asset and its <c>ETag</c>, or the mapped problem response.
    /// </summary>
    /// <remarks>
    /// Not <c>ToOk</c>, because the header has to be set on the way out and only a success
    /// has a version to set it from. A failure goes through exactly the same mapper every
    /// other endpoint uses. Every read and every write of an asset answers with a tag, so a
    /// client always leaves an exchange holding a precondition it can state on the next one.
    /// </remarks>
    /// <param name="result">What the handler returned.</param>
    /// <param name="context">The request, whose response headers the tag is set on.</param>
    /// <returns>The HTTP result.</returns>
    private static IResult WithETag(Result<AssetDetail> result, HttpContext context)
    {
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblem(result.Error!);
        }

        SetETag(context, result.Value.Version);

        return MinimalApi.Ok(result.Value.Response);
    }

    private static void SetETag(HttpContext context, uint version) =>
        context.Response.Headers.ETag = AssetETag.For(version);
}
