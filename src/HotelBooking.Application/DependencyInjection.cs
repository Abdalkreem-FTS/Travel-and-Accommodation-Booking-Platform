using FluentValidation;

using HotelBooking.Application.Amenities;
using HotelBooking.Application.Authentication;
using HotelBooking.Application.Bookings;
using HotelBooking.Application.Carts;
using HotelBooking.Application.Cities;
using HotelBooking.Application.Deals;
using HotelBooking.Application.Hotels;
using HotelBooking.Application.Rooms;
using HotelBooking.Application.Users;
using HotelBooking.Application.Visits;

using Microsoft.Extensions.DependencyInjection;

namespace HotelBooking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: false);

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IBookingCancellationService, BookingCancellationService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ICityService, CityService>();
        services.AddScoped<IHotelService, HotelService>();
        services.AddScoped<IHotelSearchService, HotelSearchService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IRoomAvailabilityService, RoomAvailabilityService>();
        services.AddScoped<IAmenityService, AmenityService>();
        services.AddScoped<IDealService, DealService>();
        services.AddScoped<IVisitService, VisitService>();

        return services;
    }
}
