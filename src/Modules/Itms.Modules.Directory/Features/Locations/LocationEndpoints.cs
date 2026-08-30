using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Features.Locations.CreateLocation;
using Itms.Modules.Directory.Features.Locations.DeleteLocation;
using Itms.Modules.Directory.Features.Locations.GetLocation;
using Itms.Modules.Directory.Features.Locations.ListLocations;
using Itms.Modules.Directory.Features.Locations.MoveLocation;
using Itms.Modules.Directory.Features.Locations.UpdateLocation;
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
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(search, parentId, rootId, kind, PageRequest.Of(page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToOk();
            })
            .WithName("ListLocations")
            .WithSummary("Lists locations in path order, optionally within one parent or one subtree.")
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
            .WithSummary("Deletes a location that has no children. A location with children is refused with 409.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
