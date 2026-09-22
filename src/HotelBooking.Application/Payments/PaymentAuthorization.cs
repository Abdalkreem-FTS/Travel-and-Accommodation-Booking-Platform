using HotelBooking.Domain.Common;

namespace HotelBooking.Application.Payments;

public sealed record PaymentAuthorization(string Id, Money Amount);
