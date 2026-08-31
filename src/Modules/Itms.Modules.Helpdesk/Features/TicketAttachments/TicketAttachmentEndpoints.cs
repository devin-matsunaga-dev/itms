using Itms.Modules.Helpdesk.Configuration;
using Itms.Modules.Helpdesk.Features.TicketAttachments.DownloadTicketAttachment;
using Itms.Modules.Helpdesk.Features.TicketAttachments.ListTicketAttachments;
using Itms.Modules.Helpdesk.Features.TicketAttachments.UploadTicketAttachment;
using Itms.Modules.Helpdesk.Features.Tickets;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MinimalApi = Microsoft.AspNetCore.Http.Results;

namespace Itms.Modules.Helpdesk.Features.TicketAttachments;

/// <summary>The attachment endpoints, under <c>/api/v1/tickets/{ticketId}/attachments</c>.</summary>
/// <remarks>
/// <para>
/// <b>Authenticated, not Technician.</b> A requester attaching the screenshot that explains
/// their problem is most of what self-service ticketing is for, so the same policy and the
/// same two filters as the comment thread apply — public files only, on their own ticket.
/// </para>
/// <para>
/// <b>The upload's own size limit comes from configuration</b>, attached as endpoint
/// metadata at map time, so a body larger than the deployment allows is refused by the
/// server before it is buffered rather than after. Kestrel answers that with a 413, which
/// <c>MalformedRequestExceptionHandler</c> renders as <c>ProblemDetails</c> like everything
/// else. The margin over the configured cap is the multipart envelope: boundaries, part
/// headers, and the <c>isInternal</c> field.
/// </para>
/// <para>
/// <b>Antiforgery is checked by this module's own filter, not by the middleware.</b> A
/// form-bound minimal API carries antiforgery metadata that expects
/// <c>UseAntiforgery()</c> in the pipeline; this host validates explicitly through
/// <see cref="AntiforgeryFilter"/> on every write instead, and changing the middleware
/// pipeline is auth configuration WP-1.7 has no leave to touch. So the metadata is
/// disabled and the filter does the work — the same check, in the same place as every
/// other write in the system.
/// </para>
/// </remarks>
internal static class TicketAttachmentEndpoints
{
    /// <summary>The route these endpoints hang off, under the ticket they belong to.</summary>
    public const string RoutePrefix = TicketEndpoints.RoutePrefix + "/{ticketId:guid}/attachments";

    /// <summary>
    /// Head-room over the configured cap for the multipart envelope. Generous enough for
    /// long file names and part headers, far too small to smuggle a second file through.
    /// </summary>
    private const long MultipartOverheadAllowance = 64 * 1024;

    /// <summary>Maps the attachment endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    public static void MapTicketAttachments(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var settings = endpoints.ServiceProvider.GetRequiredService<IOptions<HelpdeskAttachmentOptions>>().Value;

        var group = endpoints
            .MapGroup(RoutePrefix)
            .WithTags("Tickets")
            .RequireAuthorization(ItmsPolicies.Authenticated);

        group
            .MapGet("/", async (
                Guid ticketId,
                ListTicketAttachmentsHandler handler,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(ticketId, PageRequest.Of(page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToOk();
            })
            .WithName("ListTicketAttachments")
            .WithSummary("Reads a ticket's attachments, newest first.")
            .WithDescription(
                "Metadata only. Internal attachments are absent for anybody but a Technician or an "
                + "Admin, and the total counts only what the caller can see.")
            .Produces<PagedResult<TicketAttachmentResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group
            .MapGet("/{attachmentId:guid}", async (
                Guid ticketId,
                Guid attachmentId,
                DownloadTicketAttachmentHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(ticketId, attachmentId, cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsFailure)
                {
                    return ProblemDetailsMapper.ToProblem(result.Error!);
                }

                var download = result.Value;

                // The browser must never render an attachment inline. nosniff stops it
                // second-guessing the declared type, and the disposition stops it treating
                // the response as a document in the application's own origin — which is
                // what would turn an uploaded .txt into stored cross-site scripting.
                context.Response.Headers.XContentTypeOptions = "nosniff";

                return MinimalApi.Stream(
                    download.Content,
                    download.ContentType,
                    download.FileName,
                    enableRangeProcessing: true);
            })
            .WithName("DownloadTicketAttachment")
            .WithSummary("Fetches an attachment's contents.")
            .WithDescription(
                "Re-checks the ticket and the audience on every request: an attachment on a ticket the "
                + "caller cannot read, or an internal one they are not staff for, is 404 either way. "
                + "Always served as a download, never inline.")
            .Produces<IResult>(StatusCodes.Status200OK, "application/octet-stream")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group
            .MapPost("/", async (
                Guid ticketId,
                IFormFile file,
                [FromForm] bool? isInternal,
                UploadTicketAttachmentHandler handler,
                CancellationToken cancellationToken) =>
            {
                var upload = new UploadedFile(file.FileName, file.Length, file.OpenReadStream);

                var result = await handler
                    .HandleAsync(ticketId, upload, isInternal ?? false, cancellationToken)
                    .ConfigureAwait(false);

                return result.IsFailure
                    ? ProblemDetailsMapper.ToProblem(result.Error!)
                    : MinimalApi.Created(
                        $"{TicketEndpoints.RoutePrefix}/{ticketId}/attachments/{result.Value.Id}",
                        result.Value);
            })
            .AddEndpointFilter<AntiforgeryFilter>()
            // See the class remarks: the check happens, it happens in the filter above.
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(settings.MaxBytes + MultipartOverheadAllowance))
            .WithName("UploadTicketAttachment")
            .WithSummary("Attaches a file to a ticket.")
            .WithDescription(
                "Multipart form with a 'file' part and an optional 'isInternal' field. The extension "
                + "must be on the allowlist and the file's leading bytes must agree with it; the "
                + "declared media type is ignored. Only a Technician or an Admin may attach an "
                + "internal file.")
            .Produces<TicketAttachmentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);
    }
}
