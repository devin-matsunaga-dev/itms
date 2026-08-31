using System.Text.Json.Serialization;

namespace Itms.Modules.Helpdesk.Features.Tickets.ListTickets;

/// <summary>Which way a sorted list runs.</summary>
/// <remarks>
/// Declared here rather than in <c>Itms.Platform</c> because this is the first list in the
/// system that sorts by anything but a fixed order. The moment a second module wants it,
/// it belongs beside <c>PageRequest</c> in the shared kernel — the same "third copy"
/// trigger STATUS.md records against Directory's search pattern.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<SortDirection>))]
public enum SortDirection
{
    /// <summary>Smallest, earliest, or lowest first.</summary>
    Ascending,

    /// <summary>Largest, latest, or highest first.</summary>
    Descending,
}
