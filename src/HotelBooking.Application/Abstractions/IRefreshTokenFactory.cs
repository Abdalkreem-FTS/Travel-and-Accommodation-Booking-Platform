namespace HotelBooking.Application.Abstractions;

public sealed record GeneratedRefreshToken(string RawValue, string Hash);

public interface IRefreshTokenFactory
{
    GeneratedRefreshToken Generate();

    string Hash(string rawValue);
}
