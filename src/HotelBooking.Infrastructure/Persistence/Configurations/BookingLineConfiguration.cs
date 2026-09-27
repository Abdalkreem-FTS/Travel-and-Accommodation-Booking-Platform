using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Rooms;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBooking.Infrastructure.Persistence.Configurations;

internal sealed class BookingLineConfiguration : IEntityTypeConfiguration<BookingLine>
{
    private const int PricePrecision = 18;

    public void Configure(EntityTypeBuilder<BookingLine> builder)
    {
        builder.ToTable("BookingLines");

        builder.HasKey(line => new { line.BookingId, line.LineNumber })
            .HasName("PK_BookingLines");

        builder.Property(line => line.LineNumber).ValueGeneratedNever();

        builder.ComplexProperty(line => line.Stay, stay =>
        {
            stay.Property(range => range.CheckIn).HasColumnName("CheckIn").HasColumnType("date").IsRequired();
            stay.Property(range => range.CheckOut).HasColumnName("CheckOut").HasColumnType("date").IsRequired();
        });

        builder.ComplexProperty(line => line.Guests, guests =>
        {
            guests.Property(occupancy => occupancy.Adults).HasColumnName("Adults").IsRequired();
            guests.Property(occupancy => occupancy.Children).HasColumnName("Children").IsRequired();
        });

        builder.ComplexProperty(line => line.NightlyRate, rate =>
        {
            rate.Property(money => money.Amount)
                .HasColumnName("NightlyAmount")
                .HasPrecision(PricePrecision, Money.DecimalPlaces)
                .IsRequired();

            rate.Property(money => money.Currency)
                .HasColumnName("NightlyCurrency")
                .HasMaxLength(Money.CurrencyLength)
                .IsFixedLength()
                .IsUnicode(false)
                .IsRequired();
        });

        builder.ComplexProperty(line => line.LineTotal, total =>
        {
            total.Property(money => money.Amount)
                .HasColumnName("LineAmount")
                .HasPrecision(PricePrecision, Money.DecimalPlaces)
                .IsRequired();

            total.Property(money => money.Currency)
                .HasColumnName("LineCurrency")
                .HasMaxLength(Money.CurrencyLength)
                .IsFixedLength()
                .IsUnicode(false)
                .IsRequired();
        });

        builder.ComplexProperty(line => line.DiscountTotal, discount =>
        {
            discount.Property(money => money.Amount)
                .HasColumnName("DiscountAmount")
                .HasPrecision(PricePrecision, Money.DecimalPlaces)
                .IsRequired();

            discount.Property(money => money.Currency)
                .HasColumnName("DiscountCurrency")
                .HasMaxLength(Money.CurrencyLength)
                .IsFixedLength()
                .IsUnicode(false)
                .IsRequired();
        });

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(line => line.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
