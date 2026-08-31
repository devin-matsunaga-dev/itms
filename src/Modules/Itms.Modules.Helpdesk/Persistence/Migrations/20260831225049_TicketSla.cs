using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itms.Modules.Helpdesk.Persistence.Migrations
{
    /// <summary>
    /// WP-1.8. Gives every ticket two SLA clocks: the targets it was promised, the instants
    /// they expire, the 80% marks, and the pause accounting Waiting produces.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The five required columns are added nullable, backfilled, and only then made
    /// <c>NOT NULL</c>.</b> The scaffolded version set them <c>NOT NULL</c> in one step with
    /// a default of <c>0001-01-01</c>, which would have left every ticket already in the
    /// database claiming it was due two thousand years ago. Deadlines cannot be defaulted;
    /// they have to be computed from each ticket's own creation instant and its own
    /// priority's targets, which is what the backfill does.
    /// </para>
    /// <para>
    /// <b>The backfill reconstructs the clocks as though nothing had ever been paused.</b>
    /// A ticket that sat in Waiting before this migration ran keeps no record of how long —
    /// the pause accounting starts here — so its resolution deadline comes out earlier than
    /// it would have under the new rules. WP-1.4's history could in principle be replayed to
    /// recover those spans; that is a great deal of machinery for tickets raised before the
    /// feature existed, and the honest simple answer was preferred.
    /// </para>
    /// <para>
    /// <b>The 80% mark is <c>48 × target-in-minutes</c> seconds</b>, which is exact integer
    /// arithmetic here and in <c>SlaClock.WarnPoint</c> alike — 80% of any whole number of
    /// minutes is a whole number of seconds — so a backfilled row and a row written by the
    /// application agree to the microsecond.
    /// </para>
    /// </remarks>
    public partial class TicketSla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sla_response_target_minutes",
                schema: "helpdesk",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sla_resolution_target_minutes",
                schema: "helpdesk",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sla_response_due_at",
                schema: "helpdesk",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sla_response_warn_at",
                schema: "helpdesk",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sla_resolution_warn_at",
                schema: "helpdesk",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            // Nullable for good: null means "nobody has answered yet" and "the clock is
            // running", which is the state most tickets are in.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sla_responded_at",
                schema: "helpdesk",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sla_paused_at",
                schema: "helpdesk",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            // Zero is the truthful value for every existing row: nothing has been accounted
            // for a pause before now.
            migrationBuilder.AddColumn<TimeSpan>(
                name: "sla_paused_total",
                schema: "helpdesk",
                table: "tickets",
                type: "interval",
                nullable: false,
                defaultValue: TimeSpan.Zero);

            // Each ticket against its own priority's targets, as they read today. There is
            // no earlier record of what they read when the ticket was filed — that is
            // exactly the gap the snapshot columns close from here on.
            migrationBuilder.Sql(
                """
                UPDATE helpdesk.tickets AS t
                SET sla_response_target_minutes = p.response_target_minutes,
                    sla_resolution_target_minutes = p.resolution_target_minutes,
                    sla_response_due_at = t.created_at + make_interval(mins => p.response_target_minutes),
                    sla_response_warn_at = t.created_at + make_interval(secs => p.response_target_minutes * 48),
                    sla_resolution_warn_at = t.created_at + make_interval(secs => p.resolution_target_minutes * 48),
                    due_at = t.created_at + make_interval(mins => p.resolution_target_minutes)
                FROM helpdesk.ticket_priorities AS p
                WHERE p.id = t.priority_id;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "sla_response_target_minutes",
                schema: "helpdesk",
                table: "tickets",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "sla_resolution_target_minutes",
                schema: "helpdesk",
                table: "tickets",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "sla_response_due_at",
                schema: "helpdesk",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "sla_response_warn_at",
                schema: "helpdesk",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "sla_resolution_warn_at",
                schema: "helpdesk",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            // Held the column since WP-1.2 and empty until now. Required from here on: every
            // ticket has a priority, and every priority carries a resolution target.
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "due_at",
                schema: "helpdesk",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            // The queue's due-date sort and the overdue filter both range over this pair.
            migrationBuilder.CreateIndex(
                name: "ix_tickets_status_due_at",
                schema: "helpdesk",
                table: "tickets",
                columns: new[] { "status", "due_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_status_due_at",
                schema: "helpdesk",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_paused_at",
                schema: "helpdesk",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_paused_total",
                schema: "helpdesk",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_resolution_target_minutes",
                schema: "helpdesk",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_resolution_warn_at",
                schema: "helpdesk",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_responded_at",
                schema: "helpdesk",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_response_due_at",
                schema: "helpdesk",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_response_target_minutes",
                schema: "helpdesk",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_response_warn_at",
                schema: "helpdesk",
                table: "tickets");

            // Back to nullable, but the values computed above are left where they are: the
            // column held nothing before this migration and dropping the reconstruction
            // would lose more than it restores.
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "due_at",
                schema: "helpdesk",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");
        }
    }
}
