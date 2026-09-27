using HotelBooking.Domain.Idempotency;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBooking.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");

        builder.HasKey(record => new { record.UserId, record.Key })
            .HasName("PK_IdempotencyRecords");

        builder.Property(record => record.Key)
            .HasMaxLength(IdempotencyRecord.MaxKeyLength)
            .IsRequired();

        builder.Property(record => record.Endpoint)
            .HasMaxLength(IdempotencyRecord.MaxEndpointLength)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(record => record.BookingId);

        builder.Property(record => record.CreatedAtUtc).IsRequired();
    }
}
