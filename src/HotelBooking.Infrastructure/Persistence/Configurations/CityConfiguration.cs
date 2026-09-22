using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBooking.Infrastructure.Persistence.Configurations;

internal sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("Cities");

        builder.HasKey(city => city.Id);

        builder.Property(city => city.Id).ValueGeneratedNever();

        builder.Property(city => city.Name)
            .HasMaxLength(City.MaxNameLength)
            .IsRequired();

        builder.Property(city => city.Country)
            .HasConversion(country => country.Value, value => CountryCode.Create(value).Value)
            .HasMaxLength(CountryCode.Length)
            .IsFixedLength()
            .IsUnicode(false)
            .IsRequired();

        builder.Property(city => city.PostOffice)
            .HasMaxLength(City.MaxPostOfficeLength)
            .IsRequired();

        builder.Property(city => city.ThumbnailUrl)
            .HasMaxLength(City.MaxThumbnailUrlLength);

        builder.Property(city => city.CreatedAtUtc).IsRequired();

        builder.Property(city => city.IsDeleted).HasDefaultValue(false);

        builder.Property<byte[]>(RowVersionProperty.Name).IsRowVersion().IsRequired();

        builder.HasIndex(city => new { city.Country, city.Name })
            .IsUnique()
            .HasDatabaseName("IX_Cities_Country_Name")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(city => city.Name)
            .HasDatabaseName("IX_Cities_Name")
            .HasFilter("[IsDeleted] = 0");

        builder.Ignore(city => city.DomainEvents);

        builder.HasQueryFilter(city => !city.IsDeleted);
    }
}
