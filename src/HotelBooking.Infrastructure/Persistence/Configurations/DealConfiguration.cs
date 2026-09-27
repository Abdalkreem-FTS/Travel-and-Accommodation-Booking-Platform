using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Rooms;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBooking.Infrastructure.Persistence.Configurations;

internal sealed class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    private const string Nights = "_nights";

    public void Configure(EntityTypeBuilder<Deal> builder)
    {
        builder.ToTable("Deals");

        builder.HasKey(deal => deal.Id);

        builder.Property(deal => deal.Id).ValueGeneratedNever();

        builder.ComplexProperty(deal => deal.Discount, discount =>
            discount.Property(percentage => percentage.Value)
                .HasColumnName("DiscountPercentage")
                .IsRequired());

        builder.Property(deal => deal.StartsOn).IsRequired();

        builder.Property(deal => deal.EndsOn).IsRequired();

        builder.Property(deal => deal.IsFeatured).IsRequired();

        builder.Property(deal => deal.CreatedAtUtc).IsRequired();

        builder.Property(deal => deal.IsDeleted).HasDefaultValue(false);

        builder.Property<byte[]>(RowVersionProperty.Name).IsRowVersion().IsRequired();

        builder.HasIndex(deal => new { deal.StartsOn, deal.EndsOn })
            .HasDatabaseName("IX_Deals_Live")
            .HasFilter("[IsFeatured] = 1 AND [IsDeleted] = 0")
            .IncludeProperties(deal => new { deal.RoomId, deal.HotelId });

        builder.HasIndex(deal => new { deal.RoomId, deal.StartsOn, deal.EndsOn })
            .HasDatabaseName("IX_Deals_RoomId_StartsOn_EndsOn")
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne<Hotel>()
            .WithMany()
            .HasForeignKey(deal => deal.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(deal => deal.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(deal => deal.Nights)
            .WithOne()
            .HasForeignKey(night => night.DealId)
            .HasConstraintName("FK_DealNights_Deals")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(deal => deal.Nights)
            .HasField(Nights)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(deal => deal.DomainEvents);

        builder.HasQueryFilter(deal => !deal.IsDeleted);
    }
}
