using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Idempotency;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;
using HotelBooking.Domain.Users;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence;

internal static class SqlErrorTranslator
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private static readonly (string ConstraintName, Func<Error> ToError)[] KnownConstraints =
    [
        ("IX_Users_Email", () => UserErrors.EmailAlreadyRegistered),
        ("PK_UserRoles", () => UserErrors.RoleGrantRaced),
        ("IX_Cities_Country_Name", () => CityErrors.NameAlreadyUsedInCountry),
        ("IX_Hotels_CityId_Name", () => HotelErrors.NameAlreadyUsedInCity),
        ("IX_Rooms_HotelId_Number", () => RoomErrors.NumberAlreadyUsedInHotel),
        ("PK_RoomNightInventory", () => BookingErrors.RoomUnavailable),
        ("PK_DealNights", () => DealErrors.OverlapsExisting),
        ("PK_IdempotencyRecords", () => IdempotencyErrors.RequestInProgress),
        ("IX_Bookings_ConfirmationNumber", () => BookingErrors.ConfirmationNumberCollision),
        ("IX_Bookings_UserId_Pending", () => BookingErrors.PaymentPending)
    ];

    public static Error? Translate(DbUpdateException exception)
    {
        if (exception.InnerException is not SqlException sqlException)
        {
            return null;
        }

        if (sqlException.Number is not (UniqueIndexViolation or UniqueConstraintViolation))
        {
            return null;
        }

        foreach (var (constraintName, toError) in KnownConstraints)
        {
            if (sqlException.Message.Contains(constraintName, StringComparison.Ordinal))
            {
                return toError();
            }
        }

        return null;
    }
}
