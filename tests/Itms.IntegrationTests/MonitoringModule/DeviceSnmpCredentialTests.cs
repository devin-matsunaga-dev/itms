using System.Net;
using System.Text.Json;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.AssetsModule;
using Itms.IntegrationTests.Identity;
using Npgsql;

namespace Itms.IntegrationTests.MonitoringModule;

/// <summary>
/// The SNMP community string is write-only, and this suite is what says so.
/// </summary>
/// <remarks>
/// <para>
/// The design's promise is narrow and worth stating exactly: the plaintext reaches the
/// column and nothing else. It is not in any response body, not in the audit trail, and
/// not reachable through the ordinary edit. Each of those is asserted below against the
/// real wire and the real tables rather than against the shapes that were written to
/// exclude it — a response record with no field for a secret proves nothing, because
/// deserialisation would drop one silently.
/// </para>
/// <para>
/// What it deliberately does <em>not</em> promise is encryption at rest: the value is
/// plaintext in <c>monitoring.devices.snmp_community</c>, which
/// <see cref="The_credential_is_stored_on_the_device_row"/> asserts, and WP-6.3 owns.
/// That is only a defensible place for it because none of the guarantees above depend on
/// the value being unreadable — they depend on there being no application path to it.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class DeviceSnmpCredentialTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private const string Community = "s3cret-community-ro";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Asserted against the raw JSON rather than a deserialised shape: a record with no
    /// field for the community string would ignore one silently, which is exactly the
    /// regression this test exists to catch.
    /// </summary>
    [Fact]
    public async Task No_read_returns_the_community_string()
    {
        using var admin = await SignedInAsync("admin");
        var device = await ADeviceAsync(admin, "SRV-3200", Community);

        var detail = await admin.GetStringAsync(
            new Uri($"{DevicesClient.Devices}/{device.Id}", UriKind.Relative),
            Token);
        var list = await admin.GetStringAsync(new Uri(DevicesClient.Devices, UriKind.Relative), Token);

        detail.ShouldNotContain(Community);
        list.ShouldNotContain(Community);

        // Not merely absent from the payload: absent as a field name too, so nothing can
        // start carrying it later without this failing.
        detail.ShouldNotContain("snmpCommunity", Case.Insensitive);
        list.ShouldNotContain("snmpCommunity", Case.Insensitive);
    }

    /// <summary>The 201 is a read too, and it is the one that has just been handed the secret.</summary>
    [Fact]
    public async Task The_registration_response_does_not_echo_the_community_string()
    {
        using var admin = await SignedInAsync("admin");
        var asset = await AnAssetAsync(admin, "SRV-3201");

        var response = await DevicesClient.PostDeviceAsync(
            admin,
            new { assetId = asset.Id, hostname = "sw-core-01", snmpEnabled = true, snmpCommunity = Community },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.Content.ReadAsStringAsync(Token)).ShouldNotContain(Community);
    }

    /// <summary>A screen needs to know whether one is configured, and that is all it gets.</summary>
    [Fact]
    public async Task A_read_says_whether_a_credential_is_configured()
    {
        using var admin = await SignedInAsync("admin");
        var withOne = await ADeviceAsync(admin, "SRV-3202", Community);
        var withoutOne = await ADeviceAsync(admin, "SRV-3203", community: null);

        (await DevicesClient.GetAsync(admin, withOne.Id, Token)).SnmpCredentialSet.ShouldBeTrue();
        (await DevicesClient.GetAsync(admin, withoutOne.Id, Token)).SnmpCredentialSet.ShouldBeFalse();
    }

    /// <summary>
    /// The trap the dedicated route exists to avoid. <c>PUT</c> is a full replacement, so
    /// an edit form that never received the credential must not be able to clear it.
    /// </summary>
    [Fact]
    public async Task The_ordinary_edit_cannot_clear_the_credential()
    {
        using var admin = await SignedInAsync("admin");
        var device = await ADeviceAsync(admin, "SRV-3204", Community);

        var response = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{DevicesClient.Devices}/{device.Id}",
            new { hostname = "sw-core-02", pollIntervalSeconds = 120 },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await DevicesClient.GetAsync(admin, device.Id, Token);
        after.Hostname.ShouldBe("sw-core-02");
        after.SnmpCredentialSet.ShouldBeTrue();
        (await ReadStoredCommunityAsync(device.Id)).ShouldBe(Community);
    }

    [Fact]
    public async Task Setting_a_credential_makes_it_configured()
    {
        using var admin = await SignedInAsync("admin");
        var device = await ADeviceAsync(admin, "SRV-3205", community: null);

        var response = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{DevicesClient.Devices}/{device.Id}/snmp-credential",
            new { community = Community },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DevicesClient.GetAsync(admin, device.Id, Token)).SnmpCredentialSet.ShouldBeTrue();
    }

    /// <summary>
    /// Its own verb, so that clearing a credential and a client sending a blank field by
    /// mistake cannot be the same request.
    /// </summary>
    [Fact]
    public async Task Clearing_a_credential_removes_it_and_a_blank_one_is_refused()
    {
        using var admin = await SignedInAsync("admin");
        var device = await ADeviceAsync(admin, "SRV-3206", Community);

        var blank = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{DevicesClient.Devices}/{device.Id}/snmp-credential",
            new { community = "  " },
            Token);

        blank.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await DevicesClient.GetAsync(admin, device.Id, Token)).SnmpCredentialSet.ShouldBeTrue();

        var cleared = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Delete,
            $"{DevicesClient.Devices}/{device.Id}/snmp-credential",
            body: null,
            Token);

        cleared.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DevicesClient.GetAsync(admin, device.Id, Token)).SnmpCredentialSet.ShouldBeFalse();
        (await ReadStoredCommunityAsync(device.Id)).ShouldBeNull();
    }

    /// <summary>
    /// ARCHITECTURE.md §8 makes configuration changes mandatory audit coverage — and
    /// CONVENTIONS.md's security floor means the entry must not be a second copy of the
    /// secret. Both halves are asserted: the actions are there, and neither the diff nor
    /// any other column carries the value.
    /// </summary>
    [Fact]
    public async Task The_audit_trail_records_that_a_credential_moved_and_never_what_it_is()
    {
        using var admin = await SignedInAsync("admin");
        var device = await ADeviceAsync(admin, "SRV-3207", Community);

        await DevicesClient.SendAsync(
            admin,
            HttpMethod.Delete,
            $"{DevicesClient.Devices}/{device.Id}/snmp-credential",
            body: null,
            Token);

        var entries = await ReadAuditAsync(device.Id);

        entries.Select(entry => entry.Action).ShouldContain("monitoring.device_registered");
        entries.Select(entry => entry.Action).ShouldContain("monitoring.device_snmp_credential_cleared");

        foreach (var entry in entries)
        {
            (entry.Changes ?? string.Empty).ShouldNotContain(Community);
        }

        // The registration entry says a credential was configured, without saying what.
        var registration = entries.Single(entry => entry.Action == "monitoring.device_registered");
        registration.Changes.ShouldNotBeNull().ShouldContain("snmpCommunity");
        registration.Changes.ShouldContain("(set)");
    }

    /// <summary>
    /// Where the plaintext genuinely is, stated out loud. WP-6.3 owns encryption at rest;
    /// this test is what a future package changing that will have to come and edit, which
    /// is the point of writing it down as an assertion rather than only as a comment.
    /// </summary>
    [Fact]
    public async Task The_credential_is_stored_on_the_device_row()
    {
        using var admin = await SignedInAsync("admin");
        var device = await ADeviceAsync(admin, "SRV-3208", Community);

        (await ReadStoredCommunityAsync(device.Id)).ShouldBe(Community);
    }

    private async Task<string?> ReadStoredCommunityAsync(Guid deviceId)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "select snmp_community from monitoring.devices where id = @id";
        command.Parameters.Add(new NpgsqlParameter("id", deviceId));

        var value = await command.ExecuteScalarAsync(Token);
        return value is DBNull or null ? null : (string)value;
    }

    private async Task<IReadOnlyList<AuditRowDto>> ReadAuditAsync(Guid deviceId)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "select action, changes::text from audit.audit_entries "
            + "where entity_type = 'MonitoredDevice' and entity_id = @id";
        command.Parameters.Add(new NpgsqlParameter("id", deviceId.ToString()));

        var rows = new List<AuditRowDto>();
        await using var reader = await command.ExecuteReaderAsync(Token);

        while (await reader.ReadAsync(Token))
        {
            rows.Add(new AuditRowDto(
                reader.GetString(0),
                await reader.IsDBNullAsync(1, Token) ? null : reader.GetString(1)));
        }

        return rows;
    }

    private static async Task<DeviceDto> ADeviceAsync(HttpClient admin, string assetTag, string? community)
    {
        var asset = await AnAssetAsync(admin, assetTag);

        var response = await DevicesClient.PostDeviceAsync(
            admin,
            new
            {
                assetId = asset.Id,
                hostname = "sw-core-01",
                snmpEnabled = community is not null,
                snmpCommunity = community,
            },
            Token);

        response.EnsureSuccessStatusCode();
        return await ApiClient.ReadAsync<DeviceDto>(response, Token);
    }

    private static async Task<AssetDto> AnAssetAsync(HttpClient admin, string assetTag)
    {
        var typeId = await AssetsClient.AnyTypeIdAsync(admin, Token);
        return await AssetsClient.CreateAssetAsync(admin, assetTag, typeId, Token);
    }

    private async Task<HttpClient> SignedInAsync(string userName) =>
        await AuthClient.SignedInAsync(fixture, userName, Token);

    private sealed record AuditRowDto(string Action, string? Changes);
}
