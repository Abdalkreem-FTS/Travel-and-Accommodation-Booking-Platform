using HotelBooking.Application.Common;
using HotelBooking.Domain.Users;

namespace HotelBooking.Application.Abstractions;

public interface IAccessTokenProvider
{
    AccessToken Issue(User user, Guid sessionId);
}
