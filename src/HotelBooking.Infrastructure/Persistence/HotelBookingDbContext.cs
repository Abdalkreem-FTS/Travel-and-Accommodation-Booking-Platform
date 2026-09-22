using HotelBooking.Domain.Amenities;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Idempotency;
using HotelBooking.Domain.RefreshTokens;
using HotelBooking.Domain.Rooms;
using HotelBooking.Domain.Users;
using HotelBooking.Infrastructure.Outbox;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence;

public sealed class HotelBookingDbContext(DbContextOptions<HotelBookingDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<City> Cities => Set<City>();

    public DbSet<Hotel> Hotels => Set<Hotel>();

    public DbSet<Amenity> Amenities => Set<Amenity>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Deal> Deals => Set<Deal>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<RoomNight> RoomNightInventory => Set<RoomNight>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
