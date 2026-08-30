using System.Text.Json.Serialization;

namespace Itms.Modules.Directory.Domain;

/// <summary>
/// What a node in the location tree represents. The five levels SPEC.md §5 names:
/// Organization → Site → Building → Floor/Area → Room.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Floor"/> and <see cref="Area"/> are two labels for one level, which is
/// what the spec's "Floor/Area" means — a plant has areas where an office has floors,
/// and neither contains the other.
/// </para>
/// <para>
/// Serialised as text, not as its number. A client reading <c>"Room"</c> is
/// self-describing where a <c>6</c> is not, the OpenAPI document WP-0.9 generates carries
/// the names, and renumbering the enum then cannot silently change what the wire means.
/// The converter is on the type rather than configured host-wide because that is a
/// decision for every module's enums at once, and it belongs with WP-0.9's client
/// generation rather than with this package.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<LocationKind>))]
public enum LocationKind
{
    /// <summary>The organisation itself. Only a root node may be one.</summary>
    Organization = 1,

    /// <summary>A campus, plant, remote facility, or pump station.</summary>
    Site = 2,

    /// <summary>A building on a site.</summary>
    Building = 3,

    /// <summary>A floor of a building. Shares its level with <see cref="Area"/>.</summary>
    Floor = 4,

    /// <summary>A named area of a site or building. Shares its level with <see cref="Floor"/>.</summary>
    Area = 5,

    /// <summary>A room, office, or cabinet — where a person sits and a device lives.</summary>
    Room = 6,
}
