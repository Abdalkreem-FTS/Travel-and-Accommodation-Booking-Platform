using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HotelBooking.Infrastructure.Persistence;

internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HotelBookingDbContext>
{
    public HotelBookingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HotelBookingDbContext>()
            .UseSqlServer("Server=localhost;Database=HotelBooking;Trusted_Connection=False;")
            .Options;

        return new HotelBookingDbContext(options);
    }
}
