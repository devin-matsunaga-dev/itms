using Itms.Modules.Helpdesk.Domain;
using Itms.Platform.Data;

namespace Itms.Modules.Helpdesk.Persistence;

/// <summary>
/// Issues the next ticket number, safely, under any amount of concurrency.
/// </summary>
/// <remarks>
/// <para>
/// One statement does the whole job. It inserts the counter row if this installation has
/// never issued a number, and otherwise increments the existing one, returning the value
/// it claimed. Two callers racing on a fresh database do not both insert: the second
/// blocks on the primary key, then takes the <c>DO UPDATE</c> branch against the row the
/// first committed.
/// </para>
/// <para>
/// It runs on the caller's connection and inside the caller's transaction, which is what
/// makes the numbering gap-free: a creation that fails after claiming a number rolls the
/// claim back with it, and the next ticket takes the number the failed one would have had.
/// Claiming outside a transaction would give that number away permanently, so it is
/// refused — the same call <c>IEventPublisher</c> makes for the same reason.
/// </para>
/// <para>
/// Self-initialising by design. Nothing seeds the counter row, so a Respawn truncate
/// between tests and a first-run production database behave identically, and the
/// deployment step that has to run <c>HelpdeskReferenceDataSeeder</c> has one less thing
/// it can forget.
/// </para>
/// </remarks>
/// <param name="session">The ambient unit of work, whose connection and transaction the claim runs on.</param>
public sealed class TicketNumberGenerator(IModuleDbSession session)
{
    private const string SequenceParameter = "sequence";

    // A compile-time constant, composed from constants: nothing a caller supplies reaches
    // the text, and the one value that varies is bound as a parameter.
    private const string ClaimSql = $"""
        INSERT INTO {TicketNumberSequence.QualifiedTableName} (name, next_value)
        VALUES (@{SequenceParameter}, {TicketNumber.FirstValueSql})
        ON CONFLICT (name) DO UPDATE
            SET next_value = {TicketNumberSequence.TableName}.next_value + 1
        RETURNING next_value
        """;

    /// <summary>Claims the next number.</summary>
    /// <param name="cancellationToken">Cancels the claim.</param>
    /// <returns>The number, such as <c>TKT-0042</c>, already formatted.</returns>
    /// <exception cref="InvalidOperationException">
    /// There is no ambient transaction. A number claimed outside one cannot be given
    /// back, so it is a programming error rather than a failure the caller can act on.
    /// </exception>
    public async Task<string> ClaimAsync(CancellationToken cancellationToken = default)
    {
        var transaction = session.CurrentTransaction
            ?? throw new InvalidOperationException(
                "A ticket number must be claimed inside a transaction, or a creation that fails leaves a gap in the numbering.");

        var connection = await session.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ClaimSql;

        var parameter = command.CreateParameter();
        parameter.ParameterName = SequenceParameter;
        parameter.Value = TicketNumberSequence.TicketSequence;
        command.Parameters.Add(parameter);

        var claimed = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return TicketNumber.Format((long)claimed!);
    }
}
