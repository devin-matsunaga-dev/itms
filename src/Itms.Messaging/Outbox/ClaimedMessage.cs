namespace Itms.Messaging.Outbox;

/// <summary>A message a dispatcher pass has leased, with only the columns dispatch needs.</summary>
/// <param name="Id">The event id.</param>
/// <param name="EventType">The stored type name.</param>
/// <param name="Payload">The serialised event.</param>
/// <param name="Attempts">How many attempts have now been made, including this one.</param>
internal sealed record ClaimedMessage(Guid Id, string EventType, string Payload, int Attempts);
