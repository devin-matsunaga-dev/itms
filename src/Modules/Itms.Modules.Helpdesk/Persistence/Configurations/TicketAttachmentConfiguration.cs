using Itms.Modules.Helpdesk.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Helpdesk.Persistence.Configurations;

/// <summary>Maps <see cref="TicketAttachment"/> to <c>helpdesk.ticket_attachments</c>.</summary>
internal sealed class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TicketAttachment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ticket_attachments");
        builder.HasKey(attachment => attachment.Id).HasName("pk_ticket_attachments");

        builder.Property(attachment => attachment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(attachment => attachment.TicketId).HasColumnName("ticket_id").IsRequired();

        builder
            .Property(attachment => attachment.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(TicketAttachment.FileNameMaxLength)
            .IsRequired();

        builder
            .Property(attachment => attachment.StoredName)
            .HasColumnName("stored_name")
            .HasMaxLength(TicketAttachment.StoredNameLength)
            .IsRequired();

        builder
            .Property(attachment => attachment.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(TicketAttachment.ContentTypeMaxLength)
            .IsRequired();

        builder.Property(attachment => attachment.ByteLength).HasColumnName("byte_length").IsRequired();
        builder.Property(attachment => attachment.IsInternal).HasColumnName("is_internal").IsRequired();
        builder.Property(attachment => attachment.UploadedById).HasColumnName("uploaded_by_id").IsRequired();

        builder
            .Property(attachment => attachment.UploadedByName)
            .HasColumnName("uploaded_by_name")
            .HasMaxLength(TicketAttachment.UploadedByNameMaxLength)
            .IsRequired();

        builder.Property(attachment => attachment.CreatedAt).HasColumnName("created_at").IsRequired();

        builder
            .HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(attachment => attachment.TicketId)
            .HasConstraintName("fk_ticket_attachments_ticket_id")
            .OnDelete(DeleteBehavior.Restrict);

        // Unique, because the stored name is what the bytes on disk are called and two rows
        // claiming one file would mean a delete of either taking the other's contents with
        // it. The generator makes a collision effectively impossible; this is what makes
        // "effectively" unnecessary to reason about.
        builder
            .HasIndex(attachment => attachment.StoredName)
            .IsUnique()
            .HasDatabaseName("ux_ticket_attachments_stored_name");

        // One ticket's attachments, newest first, filtered by audience — the same shape as
        // the comment thread, for the same reason.
        builder
            .HasIndex(attachment => new { attachment.TicketId, attachment.IsInternal, attachment.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_ticket_attachments_ticket_id_created_at");
    }
}
