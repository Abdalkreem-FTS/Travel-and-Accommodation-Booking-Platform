using HotelBooking.Domain.Common;
using HotelBooking.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBooking.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.Email)
            .HasConversion(email => email.Value, value => Email.Create(value).Value)
            .HasMaxLength(Email.MaxLength)
            .IsRequired();

        builder.HasIndex(user => user.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email")
            .HasFilter("[IsDeleted] = 0");

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(256)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(user => user.FirstName)
            .HasMaxLength(User.MaxNameLength)
            .IsRequired();

        builder.Property(user => user.LastName)
            .HasMaxLength(User.MaxNameLength)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc).IsRequired();

        builder.Property(user => user.IsDeleted).HasDefaultValue(false);

        builder.OwnsMany(user => user.Roles, grant =>
        {
            grant.ToTable("UserRoles");

            grant.WithOwner().HasForeignKey("UserId");

            grant.HasKey("UserId", nameof(UserRoleGrant.Role));

            grant.Property(held => held.Role)
                .HasConversion<int>()
                .ValueGeneratedNever();

            grant.Property(held => held.GrantedAtUtc).IsRequired();
        });

        builder.Ignore(user => user.DomainEvents);

        builder.HasQueryFilter(user => !user.IsDeleted);
    }
}
