using Itms.Modules.Helpdesk.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Helpdesk.Persistence.Configurations;

/// <summary>Maps <see cref="Ticket"/> to <c>helpdesk.tickets</c>.</summary>
internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    /// <summary>
    /// The name of the shadow property carrying PostgreSQL's <c>xmin</c> row version.
    /// </summary>
    /// <remarks>
    /// Named here rather than spelled at each use, because WP-1.5 reads it back through
    /// <c>EF.Property&lt;uint&gt;</c> to build the ETag and a mistyped string would be a
    /// runtime failure rather than a compile error.
    /// </remarks>
    public const string VersionProperty = "Version";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("tickets");
        builder.HasKey(t => t.Id).HasName("pk_tickets");

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.Number).HasColumnName("number").HasMaxLength(TicketNumber.MaxLength).IsRequired();
        builder.Property(t => t.Subject).HasColumnName("subject").HasMaxLength(Ticket.SubjectMaxLength).IsRequired();
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(Ticket.DescriptionMaxLength).IsRequired();

        builder.Property(t => t.RequesterId).HasColumnName("requester_id").IsRequired();
        builder.Property(t => t.RequesterName).HasColumnName("requester_name").HasMaxLength(Ticket.DisplayNameMaxLength).IsRequired();
        builder.Property(t => t.DepartmentId).HasColumnName("department_id").IsRequired();
        builder.Property(t => t.DepartmentName).HasColumnName("department_name").HasMaxLength(Ticket.DisplayNameMaxLength).IsRequired();

        builder.Property(t => t.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(t => t.PriorityId).HasColumnName("priority_id").IsRequired();

        // Text, not an integer, for the reason TicketStatus documents: this column is read
        // at a psql prompt during an incident.
        builder
            .Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(t => t.AssigneeId).HasColumnName("assignee_id");
        builder.Property(t => t.AssigneeName).HasColumnName("assignee_name").HasMaxLength(Ticket.DisplayNameMaxLength);
        // The SLA columns. Every one of them is an instant or a span, never a flag: the
        // state a screen shows is computed from these against IClock on each read, so
        // nothing has to sweep the table to keep a boolean true. `due_at` is the resolution
        // clock's deadline and keeps the name WP-1.2 gave it — the queue already sorts on
        // it and the wire already carries it.
        builder.Property(t => t.DueAt).HasColumnName("due_at").IsRequired();
        builder.Property(t => t.ResponseTargetMinutes).HasColumnName("sla_response_target_minutes").IsRequired();
        builder.Property(t => t.ResolutionTargetMinutes).HasColumnName("sla_resolution_target_minutes").IsRequired();
        builder.Property(t => t.ResponseDueAt).HasColumnName("sla_response_due_at").IsRequired();
        builder.Property(t => t.ResponseWarnAt).HasColumnName("sla_response_warn_at").IsRequired();
        builder.Property(t => t.ResolutionWarnAt).HasColumnName("sla_resolution_warn_at").IsRequired();
        builder.Property(t => t.RespondedAt).HasColumnName("sla_responded_at");
        builder.Property(t => t.SlaPausedAt).HasColumnName("sla_paused_at");
        builder.Property(t => t.SlaPausedTotal).HasColumnName("sla_paused_total").IsRequired();

        builder.Property(t => t.RelatedAssetId).HasColumnName("related_asset_id");
        builder.Property(t => t.RelatedAlertId).HasColumnName("related_alert_id");
        builder.Property(t => t.ResolutionNotes).HasColumnName("resolution_notes").HasMaxLength(Ticket.ResolutionNotesMaxLength);
        builder.Property(t => t.HoldReason).HasColumnName("hold_reason").HasMaxLength(Ticket.HoldReasonMaxLength);
        builder.Property(t => t.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(t => t.ClosedAt).HasColumnName("closed_at");

        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");

        // ARCHITECTURE.md §6 wants optimistic concurrency on tickets. xmin is PostgreSQL's
        // own row version, so this costs no column and no write. WP-1.5 turned it into the
        // ETag the detail response carries and the If-Match the status change honours;
        // every write before that was already protected by the token itself.
        // Mapped by hand because Npgsql 10 no longer ships UseXminAsConcurrencyToken();
        // this is what that extension did.
        builder
            .Property<uint>(VersionProperty)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // The filter is inert today — nothing soft-deletes a ticket yet — and that is
        // precisely why it goes in now. Added later, it would silently change the meaning
        // of every list query written in the meantime. A screen that genuinely wants
        // deleted rows asks with IgnoreQueryFilters().
        builder.HasQueryFilter(t => t.DeletedAt == null);

        // Real foreign keys, with no navigation property on either side. §3 rule 6 forbids
        // one only across a module boundary; these two tables are Helpdesk's own, in one
        // schema, which is what makes renaming a category reach every ticket for free.
        // RESTRICT is WP-1.1's other half: it is what makes "deleting one in use is
        // blocked" structural rather than a matter of no route being mapped.
        builder
            .HasOne<TicketCategory>()
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .HasConstraintName("fk_tickets_category_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<TicketPriority>()
            .WithMany()
            .HasForeignKey(t => t.PriorityId)
            .HasConstraintName("fk_tickets_priority_id")
            .OnDelete(DeleteBehavior.Restrict);

        // People quote the number on the phone; two tickets answering to one would be the
        // worst kind of ambiguity this system could have.
        builder
            .HasIndex(t => t.Number)
            .IsUnique()
            .HasDatabaseName("ux_tickets_number");

        // The RESTRICT checks scan by these; without them, deleting reference data would
        // read every ticket.
        builder.HasIndex(t => t.CategoryId).HasDatabaseName("ix_tickets_category_id");
        builder.HasIndex(t => t.PriorityId).HasDatabaseName("ix_tickets_priority_id");

        // The three queue shapes WP-1.5's filters are built from: the whole queue newest
        // first, one technician's work, and one person's own tickets. The real index review
        // against the measured query set is WP-6.4's.
        builder.HasIndex(t => new { t.Status, t.CreatedAt }).HasDatabaseName("ix_tickets_status_created_at");
        builder.HasIndex(t => new { t.AssigneeId, t.Status }).HasDatabaseName("ix_tickets_assignee_status");
        builder.HasIndex(t => new { t.RequesterId, t.Status }).HasDatabaseName("ix_tickets_requester_status");
        builder.HasIndex(t => t.DepartmentId).HasDatabaseName("ix_tickets_department_id");

        // The queue's due-date sort and the overdue filter both range over this column,
        // and both are wanted with the ticket still open — which is why the status leads.
        // Like the three above, it is a considered guess rather than a measurement;
        // WP-6.4 owns the review against the query set that actually runs.
        builder.HasIndex(t => new { t.Status, t.DueAt }).HasDatabaseName("ix_tickets_status_due_at");
    }
}
