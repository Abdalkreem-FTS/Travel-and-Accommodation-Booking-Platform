using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Payments;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBooking.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    private const int ProviderIdLength = 255;

    private const int PricePrecision = 18;

    private const int CheckoutUrlLength = 2048;

    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Id).ValueGeneratedNever();

        builder.ComplexProperty(payment => payment.Amount, amount =>
        {
            amount.Property(money => money.Amount)
                .HasColumnName("Amount")
                .HasPrecision(PricePrecision, Money.DecimalPlaces)
                .IsRequired();

            amount.Property(money => money.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(Money.CurrencyLength)
                .IsFixedLength()
                .IsUnicode(false)
                .IsRequired();
        });

        builder.Property(payment => payment.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(payment => payment.ProviderCheckoutId)
            .HasMaxLength(ProviderIdLength)
            .IsUnicode(false);

        builder.Property(payment => payment.CheckoutUrl)
            .HasMaxLength(CheckoutUrlLength)
            .IsUnicode(false);

        builder.Property(payment => payment.ProviderPaymentId)
            .HasMaxLength(ProviderIdLength)
            .IsUnicode(false);

        builder.Property(payment => payment.ExpiresAtUtc).IsRequired();

        builder.Property(payment => payment.ResolvedAtUtc);

        builder.Property(payment => payment.ProviderRefundId)
            .HasMaxLength(ProviderIdLength)
            .IsUnicode(false);

        builder.Property(payment => payment.RefundRequestedAtUtc);

        builder.Property(payment => payment.RefundResolvedAtUtc);

        builder.HasIndex(payment => payment.ProviderCheckoutId)
            .IsUnique()
            .HasFilter("[ProviderCheckoutId] IS NOT NULL")
            .HasDatabaseName("IX_Payments_ProviderCheckoutId");

        builder.HasIndex(payment => payment.ExpiresAtUtc, "IX_Payments_Pending_ExpiresAtUtc")
            .HasFilter($"[Status] = {(int)PaymentStatus.Pending}");

        builder.HasIndex(payment => payment.RefundRequestedAtUtc, "IX_Payments_Refunding_RefundRequestedAtUtc")
            .HasFilter($"[Status] = {(int)PaymentStatus.Refunding}");

        builder.Property<byte[]>(RowVersionProperty.Name).IsRowVersion().IsRequired();

        builder.HasIndex(payment => payment.BookingId)
            .IsUnique()
            .HasDatabaseName("IX_Payments_BookingId");

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(payment => payment.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(payment => payment.DomainEvents);
    }
}
