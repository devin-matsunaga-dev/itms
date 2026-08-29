using Npgsql;
using NpgsqlTypes;

namespace Itms.IntegrationTests.Messaging;

/// <summary>
/// Reads the outbox tables with plain SQL rather than through the DbContext under
/// test, so an assertion cannot be satisfied by the change tracker instead of by the
/// database.
/// </summary>
internal sealed class OutboxQueries(NpgsqlDataSource dataSource)
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    public Task<long> CountMessagesAsync(CancellationToken ct) =>
        ScalarAsync("SELECT count(*) FROM messaging.outbox_messages", ct);

    public Task<long> CountProcessedAsync(CancellationToken ct) =>
        ScalarAsync("SELECT count(*) FROM messaging.outbox_messages WHERE processed_at IS NOT NULL", ct);

    public Task<long> CountFailedAsync(CancellationToken ct) =>
        ScalarAsync("SELECT count(*) FROM messaging.outbox_messages WHERE failed_at IS NOT NULL", ct);

    public Task<long> CountConsumptionsAsync(CancellationToken ct) =>
        ScalarAsync("SELECT count(*) FROM messaging.outbox_consumptions", ct);

    public Task<long> CountEffectsAsync(string consumer, CancellationToken ct) =>
        ScalarAsync(
            $"SELECT count(*) FROM {OutboxFixture.EffectsSchema}.effects WHERE consumer = @consumer",
            ct,
            ("consumer", consumer));

    public Task<long> CountTicketsAsync(CancellationToken ct) =>
        ScalarAsync($"SELECT count(*) FROM {OutboxFixture.EffectsSchema}.tickets", ct);

    public async Task<(int Attempts, DateTimeOffset AvailableAt, string? LastError)> MessageStateAsync(
        Guid id,
        CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(
            "SELECT attempts, available_at, last_error FROM messaging.outbox_messages WHERE id = @id",
            connection);
        command.Parameters.Add(new NpgsqlParameter<Guid>("id", NpgsqlDbType.Uuid) { TypedValue = id });

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new InvalidOperationException($"Outbox message {id} was not found.");
        }

        return (reader.GetInt32(0), reader.GetFieldValue<DateTimeOffset>(1), reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    public static async Task InsertTicketAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid id, string subject, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            $"INSERT INTO {OutboxFixture.EffectsSchema}.tickets (id, subject) VALUES (@id, @subject)",
            connection,
            transaction);
        command.Parameters.Add(new NpgsqlParameter<Guid>("id", NpgsqlDbType.Uuid) { TypedValue = id });
        command.Parameters.Add(new NpgsqlParameter<string>("subject", NpgsqlDbType.Text) { TypedValue = subject });

        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<long> ScalarAsync(string sql, CancellationToken ct, params (string Name, string Value)[] parameters)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter<string>(name, NpgsqlDbType.Text) { TypedValue = value });
        }

        return (long)(await command.ExecuteScalarAsync(ct))!;
    }
}
