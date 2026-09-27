using HotelBooking.Domain.Amenities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBooking.Infrastructure.Persistence.Configurations;

internal sealed class AmenityConfiguration : IEntityTypeConfiguration<Amenity>
{
    public void Configure(EntityTypeBuilder<Amenity> builder)
    {
        builder.ToTable("Amenities");

        builder.HasKey(amenity => amenity.Id);

        builder.Property(amenity => amenity.Id).ValueGeneratedNever();

        builder.Property(amenity => amenity.Slug)
            .HasMaxLength(Amenity.MaxSlugLength)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(amenity => amenity.Name)
            .HasMaxLength(Amenity.MaxNameLength)
            .IsRequired();

        builder.Property(amenity => amenity.Description)
            .HasMaxLength(Amenity.MaxDescriptionLength)
            .IsRequired();

        builder.Property(amenity => amenity.CreatedAtUtc).IsRequired();

        builder.Property(amenity => amenity.IsDeleted).HasDefaultValue(false);

        builder.HasIndex(amenity => amenity.Slug)
            .IsUnique()
            .HasDatabaseName("IX_Amenities_Slug")
            .HasFilter("[IsDeleted] = 0");

        builder.Ignore(amenity => amenity.DomainEvents);

        builder.HasQueryFilter(amenity => !amenity.IsDeleted);

        builder.HasData(AmenityCatalogue.Build());
    }
}
