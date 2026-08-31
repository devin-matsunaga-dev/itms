using System.Text.Json;
using Itms.Contracts.Auditing;
using Npgsql;
using NpgsqlTypes;

namespace Itms.IntegrationTests.AuditModule;

/// <summary>One row of <c>audit.audit_entries</c>, as the suite reads it.</summary>
/// <param name="Id">The row id.</param>
/// <param name="OccurredAt">When the audited thing happened.</param>
/// <param name="ActorId">Who did it, or null.</param>
/// <param name="ActorName">Their display name at the time, or null.</param>
/// <param name="Action">The action identifier.</param>
/// <param name="EntityType">The kind of entity.</param>
/// <param name="EntityId">The entity's id, as text.</param>
/// <param name="SourceIp">The caller's address, or null.</param>
/// <param name="Changes">The field diff.</param>
public sealed record AuditRow(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid? ActorId,
    string? ActorName,
    string Action,
    string EntityType,
    string EntityId,
    string? SourceIp,
    IReadOnlyDictionary<string, AuditFieldChange> Changes)
{
    /// <summary>Compares two rows column by column, including the diff's contents.</summary>
    /// <param name="other">The row to compare against.</param>
    /// <returns><see langword="true"/> when every column matches.</returns>
    /// <remarks>
    /// The generated equality would compare <see cref="Changes"/> by reference, because
    /// a dictionary is not a value type — so two reads of the same unchanged row would
    /// come back unequal. The append-only test asserts a refused UPDATE left the row
    /// exactly as it was, and that assertion is worthless if it can only ever fail.
    /// </remarks>
    public bool Equals(AuditRow? other) =>
        other is not null
        && Id == other.Id
        && OccurredAt == other.OccurredAt
        && ActorId == other.ActorId
        && ActorName == other.ActorName
        && Action == other.Action
        && EntityType == other.EntityType
        && EntityId == other.EntityId
        && SourceIp == other.SourceIp
        && Changes.Count == other.Changes.Count
        && Changes.All(change =>
            other.Changes.TryGetValue(change.Key, out var theirs) && change.Value == theirs);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, OccurredAt, Action, EntityType, EntityId);
}

/// <summary>
/// Reads and attacks the audit table with plain SQL rather than through the module.
/// </summary>
/// <remarks>
/// Through the module would prove nothing about either claim being tested here: an
/// assertion could be satisfied by the change tracker instead of by the database, and
/// "no code path can update this row" cannot be demonstrated by code that has no such
/// path to begin with. The point is to try it the way an operator with a psql prompt
/// would.
/// </remarks>
internal static class AuditQueries
{
    private const string SelectColumns =
        "SELECT id, occurred_at, actor_id, actor_name, action, entity_type, entity_id, source_ip, changes FROM audit.audit_entries";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Every entry, oldest first.</summary>
    /// <param name="dataSource">The database the host runs against.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows.</returns>
    public static Task<IReadOnlyList<AuditRow>> AllAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken) =>
        ReadAsync(dataSource, $"{SelectColumns} ORDER BY occurred_at, id", cancellationToken);

    /// <summary>Every entry for one action, oldest first.</summary>
    /// <param name="dataSource">The database the host runs against.</param>
    /// <param name="action">The action identifier to filter on.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The matching rows.</returns>
    public static Task<IReadOnlyList<AuditRow>> ByActionAsync(
        NpgsqlDataSource dataSource,
        string action,
        CancellationToken cancellationToken) =>
        ReadAsync(
            dataSource,
            $"{SelectColumns} WHERE action = @action ORDER BY occurred_at, id",
            cancellationToken,
            ("action", action));

    /// <summary>Every entry about one entity, oldest first.</summary>
    /// <param name="dataSource">The database the host runs against.</param>
    /// <param name="entityType">The entity kind.</param>
    /// <param name="entityId">The entity's id, as text.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The matching rows.</returns>
    public static Task<IReadOnlyList<AuditRow>> ByEntityAsync(
        NpgsqlDataSource dataSource,
        string entityType,
        string entityId,
        CancellationToken cancellationToken) =>
        ReadAsync(
            dataSource,
            $"{SelectColumns} WHERE entity_type = @type AND entity_id = @id ORDER BY occurred_at, id",
            cancellationToken,
            ("type", entityType),
            ("id", entityId));

    /// <summary>Runs a statement that is expected to be refused, and returns what happened.</summary>
    /// <param name="dataSource">The database the host runs against.</param>
    /// <param name="sql">The statement to attempt.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>The exception PostgreSQL raised, or null if the statement succeeded.</returns>
    public static async Task<PostgresException?> AttemptAsync(
        NpgsqlDataSource dataSource,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            return null;
        }
        catch (PostgresException exception)
        {
            return exception;
        }
    }

    private static async Task<IReadOnlyList<AuditRow>> ReadAsync(
        NpgsqlDataSource dataSource,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, string Value)[] parameters)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        foreach (var (name, value) in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter<string>(name, NpgsqlDbType.Text) { TypedValue = value });
        }

        var rows = new List<AuditRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var changes = reader.IsDBNull(8)
                ? []
                : JsonSerializer.Deserialize<Dictionary<string, AuditFieldChange>>(reader.GetString(8), Json)
                  ?? [];

            rows.Add(new AuditRow(
                reader.GetGuid(0),
                reader.GetFieldValue<DateTimeOffset>(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                changes));
        }

        return rows;
    }
}
