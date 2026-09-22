using HotelBooking.Domain.Common;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Rooms;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBooking.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    private const int PricePrecision = 18;

    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms");

        builder.HasKey(room => room.Id);

        builder.Property(room => room.Id).ValueGeneratedNever();

        builder.Property(room => room.Number)
            .HasMaxLength(Room.MaxNumberLength)
            .IsRequired();

        builder.Property(room => room.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.ComplexProperty(room => room.Capacity, capacity =>
        {
            capacity.Property(occupancy => occupancy.Adults)
                .HasColumnName("AdultCapacity")
                .IsRequired();

            capacity.Property(occupancy => occupancy.Children)
                .HasColumnName("ChildrenCapacity")
                .IsRequired();
        });

        builder.ComplexProperty(room => room.BasePrice, price =>
        {
            price.Property(money => money.Amount)
                .HasColumnName("BasePrice")
                .HasPrecision(PricePrecision, Money.DecimalPlaces)
                .IsRequired();

            price.Property(money => money.Currency)
                .HasColumnName("BasePriceCurrency")
                .HasMaxLength(Money.CurrencyLength)
                .IsFixedLength()
                .IsUnicode(false)
                .IsRequired();
        });

        builder.Property(room => room.CreatedAtUtc).IsRequired();

        builder.Property(room => room.IsDeleted).HasDefaultValue(false);

        builder.Property<byte[]>(RowVersionProperty.Name).IsRowVersion().IsRequired();

        builder.HasIndex(room => new { room.HotelId, room.Number })
            .IsUnique()
            .HasDatabaseName("IX_Rooms_HotelId_Number")
            .HasFilter("[IsDeleted] = 0");

        // The three search indexes cover Capacity and BasePrice, which are ComplexProperty members
        // EF cannot put those in a HasIndex - neither the lambda nor the string path works - so they
        // live in raw SQL: see RoomSearchIndexes, which every migration that rebuilds the schema
        // must call

        builder.HasOne<Hotel>()
            .WithMany()
            .HasForeignKey(room => room.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(room => room.DomainEvents);

        builder.HasQueryFilter(room => !room.IsDeleted);
    }
}
