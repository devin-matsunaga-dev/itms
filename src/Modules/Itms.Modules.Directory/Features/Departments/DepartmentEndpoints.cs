using Itms.Modules.Directory.Features.Departments.CreateDepartment;
using Itms.Modules.Directory.Features.Departments.GetDepartment;
using Itms.Modules.Directory.Features.Departments.GetDepartmentUsage;
using Itms.Modules.Directory.Features.Departments.ListDepartments;
using Itms.Modules.Directory.Features.Departments.SetDepartmentStatus;
using Itms.Modules.Directory.Features.Departments.UpdateDepartment;
using Itms.Modules.Directory.Features.Usage;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Security;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Directory.Features.Departments;

/// <summary>The department endpoints, under <c>/api/v1/departments</c>.</summary>
/// <remarks>
/// Reads are open to any signed-in account, because an end user filing their own ticket
/// has to pick a department. Writes are Admin only: SPEC.md §13 puts "manage users,
/// roles, departments, locations" under administration.
/// </remarks>
internal static class DepartmentEndpoints
{
    /// <summary>The route prefix these endpoints hang off.</summary>
    public const string RoutePrefix = "/api/v1/departments";

    /// <summary>Maps the department endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    public static void MapDepartments(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).WithTags("Departments");

        MapReads(group);
        MapAdminReads(group);
        MapWrites(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        var reads = group.MapGroup(string.Empty).RequireAuthorization(ItmsPolicies.Authenticated);

        reads
            .MapGet("/", async (
                ListDepartmentsHandler handler,
                string? search,
                bool? includeInactive,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(search, includeInactive ?? false, PageRequest.Of(page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToOk();
            })
            .WithName("ListDepartments")
            .WithSummary("Lists departments, filtered by search text and optionally including retired ones.")
            .Produces<PagedResult<DepartmentResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        reads
            .MapGet("/{id:guid}", async (
                Guid id,
                GetDepartmentHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithName("GetDepartment")
            .WithSummary("Reads one department.")
            .Produces<DepartmentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// The usage read, Admin only for the same reason as the location one: it counts
    /// people and equipment, not just names.
    /// </summary>
    private static void MapAdminReads(RouteGroupBuilder group)
    {
        group
            .MapGroup(string.Empty)
            .RequireAuthorization(ItmsPolicies.Admin)
            .MapGet("/{id:guid}/usage", async (
                Guid id,
                GetDepartmentUsageHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithName("GetDepartmentUsage")
            .WithSummary("Reports what a department still holds, before it is retired.")
            .WithDescription(
                "Informational only. A department is retired rather than deleted, and retirement is " +
                "never refused — a department with three hundred tickets against it is exactly the " +
                "one that must be retired rather than removed, so that every one of them keeps " +
                "resolving.")
            .Produces<DepartmentUsageResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
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
                CreateDepartmentRequest request,
                CreateDepartmentHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
                return result.ToCreated(department => $"{RoutePrefix}/{department.Id}");
            })
            .WithValidation<CreateDepartmentRequest>()
            .WithName("CreateDepartment")
            .WithSummary("Creates a department.")
            .Produces<DepartmentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        writes
            .MapPut("/{id:guid}", async (
                Guid id,
                UpdateDepartmentRequest request,
                UpdateDepartmentHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, request, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithValidation<UpdateDepartmentRequest>()
            .WithName("UpdateDepartment")
            .WithSummary("Replaces a department's name, code, and description.")
            .Produces<DepartmentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        writes
            .MapPost("/{id:guid}/deactivate", async (
                Guid id,
                SetDepartmentStatusHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, isActive: false, cancellationToken).ConfigureAwait(false);
                return result.ToNoContent();
            })
            .WithName("DeactivateDepartment")
            .WithSummary("Retires a department. Existing references keep resolving.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        writes
            .MapPost("/{id:guid}/reactivate", async (
                Guid id,
                SetDepartmentStatusHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, isActive: true, cancellationToken).ConfigureAwait(false);
                return result.ToNoContent();
            })
            .WithName("ReactivateDepartment")
            .WithSummary("Brings a retired department back into use.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
