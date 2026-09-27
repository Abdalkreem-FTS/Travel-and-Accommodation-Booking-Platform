using HotelBooking.Domain.RefreshTokens;
using HotelBooking.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBooking.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    private const int HashLength = 64;

    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id).ValueGeneratedNever();

        builder.Property(token => token.TokenHash)
            .HasMaxLength(HashLength)
            .IsFixedLength()
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_TokenHash");

        builder.HasIndex(token => token.FamilyId)
            .HasDatabaseName("IX_RefreshTokens_FamilyId")
            .HasFilter("[RevokedAtUtc] IS NULL");

        builder.HasIndex(token => token.UserId)
            .HasDatabaseName("IX_RefreshTokens_UserId")
            .HasFilter("[RevokedAtUtc] IS NULL")
            .IncludeProperties(token => token.FamilyId);

        builder.Property<byte[]>(RowVersionProperty.Name).IsRowVersion().IsRequired();

        builder.Property(token => token.FamilyId).IsRequired();
        builder.Property(token => token.CreatedAtUtc).IsRequired();
        builder.Property(token => token.ExpiresAtUtc).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(token => token.DomainEvents);
    }
}
