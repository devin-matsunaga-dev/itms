using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Features.Locations.CreateLocation;
using Itms.Modules.Directory.Features.Locations.DeleteLocation;
using Itms.Modules.Directory.Features.Locations.GetLocation;
using Itms.Modules.Directory.Features.Locations.GetLocationAncestors;
using Itms.Modules.Directory.Features.Locations.GetLocationUsage;
using Itms.Modules.Directory.Features.Locations.ListLocations;
using Itms.Modules.Directory.Features.Locations.ListRootLocations;
using Itms.Modules.Directory.Features.Locations.MoveLocation;
using Itms.Modules.Directory.Features.Locations.UpdateLocation;
using Itms.Modules.Directory.Features.Usage;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Security;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Directory.Features.Locations;

/// <summary>The location endpoints, under <c>/api/v1/locations</c>.</summary>
/// <remarks>
/// Reads are open to any signed-in account, because an end user filing a ticket has to
/// say where they are. Writes are Admin only, per SPEC.md §13.
/// </remarks>
internal static class LocationEndpoints
{
    /// <summary>The route prefix these endpoints hang off.</summary>
    public const string RoutePrefix = "/api/v1/locations";

    /// <summary>Maps the location endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    public static void MapLocations(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).WithTags("Locations");

        MapReads(group);
        MapPickerReads(group);
        MapAdminReads(group);
        MapWrites(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        var reads = group.MapGroup(string.Empty).RequireAuthorization(ItmsPolicies.Authenticated);

        reads
            .MapGet("/", async (
                ListLocationsHandler handler,
                string? search,
                Guid? parentId,
                Guid? rootId,
                LocationKind? kind,
                LocationKind? adoptableFor,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(search, parentId, rootId, kind, adoptableFor, PageRequest.Of(page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToOk();
            })
            .WithName("ListLocations")
            .WithSummary("Lists locations in path order, optionally within one parent or one subtree.")
            .WithDescription(
                "A cascading picker uses this with ?parentId to read one level at a time, and with " +
                "?adoptableFor to see only the nodes that could legally hold what is being placed. " +
                "?parentId is a filter, not a null check: omitting it lists the whole tree rather " +
                "than the roots, which GET /api/v1/locations/roots returns.")
            .Produces<PagedResult<LocationResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        reads
            .MapGet("/{id:guid}", async (
                Guid id,
                GetLocationHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithName("GetLocation")
            .WithSummary("Reads one location, including its full path.")
            .Produces<LocationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// The two reads a cascading picker needs beyond the flat list: the top level, and
    /// the chain down to a node it was opened on.
    /// </summary>
    /// <remarks>
    /// Both sit under the same Authenticated policy as the rest of the reads. An end user
    /// filing a ticket has to say which room they are in, and a picker that cannot read
    /// its own first level would leave them typing a GUID.
    /// </remarks>
    private static void MapPickerReads(RouteGroupBuilder group)
    {
        var reads = group.MapGroup(string.Empty).RequireAuthorization(ItmsPolicies.Authenticated);

        reads
            .MapGet("/roots", async (
                ListRootLocationsHandler handler,
                string? search,
                LocationKind? adoptableFor,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(search, adoptableFor, PageRequest.Of(page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToOk();
            })
            .WithName("ListRootLocations")
            .WithSummary("Lists the top level of the tree — the first select of a cascading picker.")
            .Produces<PagedResult<LocationResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        reads
            .MapGet("/{id:guid}/ancestors", async (
                Guid id,
                GetLocationAncestorsHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithName("GetLocationAncestors")
            .WithSummary("Reads the root-to-node chain, so a picker can preselect every level in one call.")
            .WithDescription(
                "Ordered root first and including the node itself, so the chain of a root node is " +
                "that node alone and is never empty.")
            .Produces<IReadOnlyList<LocationResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// The usage read, which is Admin rather than Authenticated.
    /// </summary>
    /// <remarks>
    /// It exists to inform the delete, and the delete is Admin. It also reports how many
    /// assets and people are in a given room, which is inventory and staffing detail — a
    /// different thing from the room's name, and not something an end user filing a ticket
    /// has any reason to be able to enumerate.
    /// </remarks>
    private static void MapAdminReads(RouteGroupBuilder group)
    {
        group
            .MapGroup(string.Empty)
            .RequireAuthorization(ItmsPolicies.Admin)
            .MapGet("/{id:guid}/usage", async (
                Guid id,
                GetLocationUsageHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithName("GetLocationUsage")
            .WithSummary("Reports what a location still holds, before a delete is offered.")
            .WithDescription(
                "canDelete is advisory: DELETE re-checks both the child count and the references, " +
                "because either can change between the two calls.")
            .Produces<LocationUsageResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
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
                CreateLocationRequest request,
                CreateLocationHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
                return result.ToCreated(location => $"{RoutePrefix}/{location.Id}");
            })
            .WithValidation<CreateLocationRequest>()
            .WithName("CreateLocation")
            .WithSummary("Creates a location under a parent, or a root organisation.")
            .Produces<LocationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        writes
            .MapPut("/{id:guid}", async (
                Guid id,
                UpdateLocationRequest request,
                UpdateLocationHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, request, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithValidation<UpdateLocationRequest>()
            .WithName("UpdateLocation")
            .WithSummary("Renames a location and rewrites the paths of everything beneath it.")
            .Produces<LocationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        writes
            .MapPost("/{id:guid}/move", async (
                Guid id,
                MoveLocationRequest request,
                MoveLocationHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, request, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithName("MoveLocation")
            .WithSummary("Reparents a location, carrying its subtree with it.")
            .Produces<LocationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        writes
            .MapDelete("/{id:guid}", async (
                Guid id,
                DeleteLocationHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return result.ToNoContent();
            })
            .WithName("DeleteLocation")
            .WithSummary("Deletes a location that has no children and that nothing references.")
            .WithDescription(
                "Refused with 409 in two cases: directory.location_has_children when the node still " +
                "has a subtree, and directory.location_in_use when assets, tickets, or users still " +
                "reference it. GET /api/v1/locations/{id}/usage reports both ahead of the click.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
