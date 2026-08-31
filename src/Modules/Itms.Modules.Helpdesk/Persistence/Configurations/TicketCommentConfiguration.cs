using Itms.Modules.Helpdesk.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Helpdesk.Persistence.Configurations;

/// <summary>Maps <see cref="TicketComment"/> to <c>helpdesk.ticket_comments</c>.</summary>
internal sealed class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ticket_comments");
        builder.HasKey(comment => comment.Id).HasName("pk_ticket_comments");

        builder.Property(comment => comment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(comment => comment.TicketId).HasColumnName("ticket_id").IsRequired();

        builder
            .Property(comment => comment.Body)
            .HasColumnName("body")
            .HasMaxLength(TicketComment.BodyMaxLength)
            .IsRequired();

        builder.Property(comment => comment.IsInternal).HasColumnName("is_internal").IsRequired();
        builder.Property(comment => comment.AuthorId).HasColumnName("author_id").IsRequired();

        builder
            .Property(comment => comment.AuthorName)
            .HasColumnName("author_name")
            .HasMaxLength(TicketComment.AuthorNameMaxLength)
            .IsRequired();

        builder.Property(comment => comment.CreatedAt).HasColumnName("created_at").IsRequired();

        // A real foreign key with no navigation property, exactly as the history entry's is:
        // §3 rule 6 forbids one only across a module boundary, and a navigation would be a
        // way to load an aggregate to render a list, which CONVENTIONS.md rules out.
        //
        // RESTRICT rather than CASCADE, for the same reason: a ticket delete is soft, no
        // code path hard-deletes one, and a package that ever adds one has to decide what
        // happens to the thread rather than find that the database threw it away.
        builder
            .HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(comment => comment.TicketId)
            .HasConstraintName("fk_ticket_comments_ticket_id")
            .OnDelete(DeleteBehavior.Restrict);

        // The one query shape this table serves: one ticket's thread, newest first, with
        // the internal lines filtered out for a requester. is_internal is in the index
        // rather than only in the predicate because that filter is applied on every read a
        // User makes, which will be most of them.
        builder
            .HasIndex(comment => new { comment.TicketId, comment.IsInternal, comment.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_ticket_comments_ticket_id_created_at");
    }
}
