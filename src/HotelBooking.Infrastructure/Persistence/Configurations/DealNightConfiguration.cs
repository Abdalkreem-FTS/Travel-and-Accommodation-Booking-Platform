using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Rooms;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBooking.Infrastructure.Persistence.Configurations;

internal sealed class DealNightConfiguration : IEntityTypeConfiguration<DealNight>
{
    public void Configure(EntityTypeBuilder<DealNight> builder)
    {
        builder.ToTable("DealNights");

        builder.HasKey(night => new { night.RoomId, night.StayDate })
            .HasName("PK_DealNights");

        builder.Property(night => night.StayDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(night => night.DealId).IsRequired();

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(night => night.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
