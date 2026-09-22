using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBooking.Infrastructure.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id).ValueGeneratedNever();

        builder.Property(message => message.Type)
            .HasMaxLength(OutboxMessage.MaxTypeLength)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(message => message.Content).IsRequired();

        builder.Property(message => message.OccurredOnUtc).IsRequired();

        builder.Property(message => message.TraceParent)
            .HasMaxLength(OutboxMessage.TraceParentLength)
            .IsUnicode(false);

        builder.Property(message => message.Attempts).IsRequired();

        builder.HasIndex(message => message.OccurredOnUtc)
            .HasDatabaseName("IX_OutboxMessages_Unprocessed")
            .IncludeProperties(message => new
            {
                message.Type,
                message.Content,
                message.Attempts,
                message.ProcessingAtUtc,
                message.TraceParent
            })
            .HasFilter("[ProcessedOnUtc] IS NULL");
    }
}
