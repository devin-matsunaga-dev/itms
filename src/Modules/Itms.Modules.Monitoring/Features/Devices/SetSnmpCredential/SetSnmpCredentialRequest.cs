namespace Itms.Modules.Monitoring.Features.Devices.SetSnmpCredential;

/// <summary>
/// The read-only SNMP community string a device is polled with.
/// </summary>
/// <remarks>
/// <para>
/// <b>A shape of its own, for a route of its own, because it is the only secret the API
/// accepts.</b> Keeping it out of <c>UpdateDeviceRequest</c> is what makes the ordinary
/// edit safe: <c>PUT</c> is a full replacement, so a credential field on that shape would
/// mean an administrator correcting a hostname on a form that never received the community
/// string would silently wipe it. There is no field to forget here because the route does
/// exactly one thing.
/// </para>
/// <para>
/// <b>There is no read that gives it back.</b> It is written here and leaves the database
/// only through <c>WP-3.2</c>'s authenticated configuration pull. Removing it is
/// <c>DELETE</c> on the same route rather than a blank string, so "clear the credential"
/// and "the client sent an empty field by mistake" cannot be the same request.
/// </para>
/// <para>
/// <b>Read-only is the only kind there is.</b> ARCHITECTURE.md §9 says no SNMP write path
/// exists in this codebase and no write community string is ever accepted in configuration.
/// This shape has one field and it is not a writable community; a second field must never
/// be added.
/// </para>
/// </remarks>
/// <param name="Community">The read-only community string. Required and non-blank.</param>
public sealed record SetSnmpCredentialRequest(string Community);
