using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBooking.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    private const string Lines = "_lines";

    private const string Nights = "_nights";

    private const int PricePrecision = 18;

    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");

        builder.HasKey(booking => booking.Id);

        builder.Property(booking => booking.Id).ValueGeneratedNever();

        builder.Property(booking => booking.EarliestCheckIn)
            .HasColumnName("CheckIn")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(booking => booking.LatestCheckOut)
            .HasColumnName("CheckOut")
            .HasColumnType("date")
            .IsRequired();

        builder.ComplexProperty(booking => booking.TotalPrice, price =>
        {
            price.Property(money => money.Amount)
                .HasColumnName("TotalAmount")
                .HasPrecision(PricePrecision, Money.DecimalPlaces)
                .IsRequired();

            price.Property(money => money.Currency)
                .HasColumnName("TotalCurrency")
                .HasMaxLength(Money.CurrencyLength)
                .IsFixedLength()
                .IsUnicode(false)
                .IsRequired();
        });

        builder.Property(booking => booking.Confirmation)
            .HasConversion(
                confirmation => confirmation.Value,
                value => ConfirmationNumber.Create(value).Value)
            .HasColumnName("ConfirmationNumber")
            .HasMaxLength(ConfirmationNumber.Length)
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(booking => booking.Confirmation)
            .IsUnique()
            .HasDatabaseName("IX_Bookings_ConfirmationNumber");

        builder.Property(booking => booking.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(booking => booking.CreatedAtUtc).IsRequired();

        builder.Property(booking => booking.CancelledAtUtc);

        builder.Property(booking => booking.CancellationReason).HasConversion<int?>();

        builder.HasMany(booking => booking.Lines)
            .WithOne()
            .HasForeignKey(line => line.BookingId)
            .HasConstraintName("FK_BookingLines_Bookings")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(booking => booking.Lines)
            .HasField(Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(booking => booking.Nights)
            .WithOne()
            .HasForeignKey(night => night.BookingId)
            .HasConstraintName("FK_RoomNightInventory_Bookings")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(booking => booking.Nights)
            .HasField(Nights)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(booking => booking.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Hotel>()
            .WithMany()
            .HasForeignKey(booking => booking.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(booking => booking.DomainEvents);
    }
}
