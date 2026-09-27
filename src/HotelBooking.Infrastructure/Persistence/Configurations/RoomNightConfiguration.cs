using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Rooms;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBooking.Infrastructure.Persistence.Configurations;

internal sealed class RoomNightConfiguration : IEntityTypeConfiguration<RoomNight>
{
    public void Configure(EntityTypeBuilder<RoomNight> builder)
    {
        builder.ToTable("RoomNightInventory");

        builder.HasKey(night => new { night.RoomId, night.StayDate })
            .HasName("PK_RoomNightInventory");

        builder.Property(night => night.StayDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(night => night.BookingId).IsRequired();

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(night => night.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
