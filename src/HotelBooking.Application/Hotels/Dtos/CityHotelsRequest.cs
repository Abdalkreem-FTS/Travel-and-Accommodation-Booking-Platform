namespace HotelBooking.Application.Hotels.Dtos;

public sealed record CityHotelsRequest(string? Search = null, int? Page = null, int? PageSize = null);
