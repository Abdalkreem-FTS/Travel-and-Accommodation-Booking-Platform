using HotelBooking.Domain.Amenities;
using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Hotels;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBooking.Infrastructure.Persistence.Configurations;

internal sealed class HotelConfiguration : IEntityTypeConfiguration<Hotel>
{
    private const int CoordinatePrecision = 9;

    public void Configure(EntityTypeBuilder<Hotel> builder)
    {
        builder.ToTable("Hotels");

        builder.HasKey(hotel => hotel.Id);

        builder.Property(hotel => hotel.Id).ValueGeneratedNever();

        builder.Property(hotel => hotel.Name)
            .HasMaxLength(Hotel.MaxNameLength)
            .IsRequired();

        builder.Property(hotel => hotel.Description)
            .HasMaxLength(Hotel.MaxDescriptionLength)
            .IsRequired();

        builder.Property(hotel => hotel.Owner)
            .HasMaxLength(Hotel.MaxOwnerLength)
            .IsRequired();

        builder.Property(hotel => hotel.StarRating)
            .HasConversion(rating => rating.Value, value => StarRating.Create(value).Value)
            .IsRequired();

        builder.ComplexProperty(hotel => hotel.Location, location =>
        {
            location.Property(point => point.Latitude)
                .HasColumnName("Latitude")
                .HasPrecision(CoordinatePrecision, GeoLocation.DecimalPlaces)
                .IsRequired();

            location.Property(point => point.Longitude)
                .HasColumnName("Longitude")
                .HasPrecision(CoordinatePrecision, GeoLocation.DecimalPlaces)
                .IsRequired();
        });

        builder.Property(hotel => hotel.ThumbnailUrl)
            .HasMaxLength(Hotel.MaxThumbnailUrlLength);

        builder.Property(hotel => hotel.CreatedAtUtc).IsRequired();

        builder.Property(hotel => hotel.IsDeleted).HasDefaultValue(false);

        builder.Property<byte[]>(RowVersionProperty.Name).IsRowVersion().IsRequired();

        builder.OwnsMany(hotel => hotel.Images, image =>
        {
            image.ToTable("HotelImages");

            image.WithOwner().HasForeignKey("HotelId");

            image.HasKey("HotelId", nameof(HotelImage.Position));

            image.Property(photo => photo.Position).ValueGeneratedNever();

            image.Property(photo => photo.Url)
                .HasMaxLength(HotelImage.MaxUrlLength)
                .IsRequired();

            image.Property(photo => photo.Caption)
                .HasMaxLength(HotelImage.MaxCaptionLength);
        });

        builder.OwnsMany(hotel => hotel.AmenityLinks, link =>
        {
            link.ToTable("HotelAmenities");

            link.WithOwner().HasForeignKey("HotelId");

            link.HasKey("HotelId", nameof(HotelAmenityLink.AmenityId));

            link.HasOne<Amenity>()
                .WithMany()
                .HasForeignKey(amenityLink => amenityLink.AmenityId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.HasIndex(hotel => new { hotel.CityId, hotel.Name })
            .IsUnique()
            .HasDatabaseName("IX_Hotels_CityId_Name")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(hotel => new { hotel.CityId, hotel.StarRating })
            .HasDatabaseName("IX_Hotels_CityId_StarRating")
            .IncludeProperties(hotel => new { hotel.Name, hotel.ThumbnailUrl })
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(hotel => hotel.Name)
            .HasDatabaseName("IX_Hotels_Name")
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(hotel => hotel.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(hotel => hotel.DomainEvents);

        builder.HasQueryFilter(hotel => !hotel.IsDeleted);
    }
}
