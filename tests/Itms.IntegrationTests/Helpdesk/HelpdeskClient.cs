using Itms.IntegrationTests.Api;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>A ticket category as the suite reads it off the wire.</summary>
/// <param name="Id">The category's id.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Description">What belongs in it.</param>
/// <param name="SortOrder">Its position in a picker.</param>
/// <param name="IsActive">False once retired.</param>
public sealed record TicketCategoryDto(Guid Id, string Name, string? Description, int SortOrder, bool IsActive);

/// <summary>A ticket priority as the suite reads it off the wire.</summary>
/// <param name="Id">The priority's id.</param>
/// <param name="Code">Its stable machine identifier.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Description">What it is for.</param>
/// <param name="Rank">Urgency order, lowest first.</param>
/// <param name="ResponseTargetMinutes">Minutes to respond.</param>
/// <param name="ResolutionTargetMinutes">Minutes to resolve.</param>
/// <param name="IsActive">False once retired.</param>
public sealed record TicketPriorityDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int Rank,
    int ResponseTargetMinutes,
    int ResolutionTargetMinutes,
    bool IsActive);

/// <summary>The helpdesk reference-data request shapes the suite needs, written once.</summary>
public static class HelpdeskClient
{
    /// <summary>The route the category endpoints hang off.</summary>
    public const string Categories = "/api/v1/ticket-categories";

    /// <summary>The route the priority endpoints hang off.</summary>
    public const string Priorities = "/api/v1/ticket-priorities";

    /// <summary>Creates a ticket category and returns it, failing loudly if the call did not succeed.</summary>
    /// <param name="client">The admin client.</param>
    /// <param name="name">The category name.</param>
    /// <param name="sortOrder">Its position in a picker.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The created category.</returns>
    public static async Task<TicketCategoryDto> CreateCategoryAsync(
        HttpClient client,
        string name,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        var response = await ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            Categories,
            new { name, description = (string?)null, sortOrder },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ApiClient.ReadAsync<TicketCategoryDto>(response, cancellationToken);
    }

    /// <summary>Creates a ticket priority and returns it, failing loudly if the call did not succeed.</summary>
    /// <param name="client">The admin client.</param>
    /// <param name="code">The stable machine identifier.</param>
    /// <param name="name">The display name.</param>
    /// <param name="rank">Urgency order.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The created priority.</returns>
    public static async Task<TicketPriorityDto> CreatePriorityAsync(
        HttpClient client,
        string code,
        string name,
        int rank,
        CancellationToken cancellationToken)
    {
        var response = await ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            Priorities,
            new
            {
                code,
                name,
                description = (string?)null,
                rank,
                responseTargetMinutes = 60,
                resolutionTargetMinutes = 480,
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ApiClient.ReadAsync<TicketPriorityDto>(response, cancellationToken);
    }
}
