using HttpResult = Microsoft.AspNetCore.Http.IResult;

namespace HotelBooking.Api.Endpoints;

internal static class VersionedResults
{
    public static HttpResult Ok<TDto>(HttpResponse response, TDto dto, string version)
    {
        response.Headers.ETag = Quote(version);

        return Results.Ok(dto);
    }

    public static HttpResult Created<TDto>(HttpResponse response, string location, TDto dto, string version)
    {
        response.Headers.ETag = Quote(version);

        return Results.Created(location, dto);
    }

    private static string Quote(string version) => $"\"{version}\"";
}
