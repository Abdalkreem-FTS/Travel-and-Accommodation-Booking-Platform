using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using HotelBooking.Api.IntegrationTests.Infrastructure;
using HotelBooking.Application.Users.Dtos;
using HotelBooking.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelBooking.Api.IntegrationTests.Users;

public sealed class RegisterUserEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Post_WithAValidBody_Returns201WithTheUserAndItsLocation()
    {
        var response = await RegisterAsync("abdalkreem@example.com");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var user = await response.Content.ReadFromJsonAsync<UserDto>(
            TestContext.Current.CancellationToken);

        user.ShouldNotBeNull();
        user.Email.ShouldBe("abdalkreem@example.com");
        user.Roles.ShouldBe(["User"]);
        response.Headers.Location!.ToString().ShouldBe($"/api/users/{user.Id}");
    }

    [Fact]
    public async Task Post_TwentyTimesInParallelWithOneEmail_CreatesExactlyOneUserAndNeverA500()
    {
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => RegisterAsync("race@example.com")));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(19);
        responses.ShouldNotContain(response => (int)response.StatusCode >= 500);

        (await LoginAsync("race@example.com")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();

        (await scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>().Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM OutboxMessages WHERE Type LIKE '%UserRegistered%'")
                .SingleAsync(TestContext.Current.CancellationToken))
            .ShouldBe(1, "the nineteen refused sign-ups rolled back, welcome email and all");
    }

    internal sealed record ProblemBody(
        string? Type,
        string? Title,
        int? Status,
        string? Detail,
        string? TraceId,
        string? ErrorCode,
        Dictionary<string, JsonElement>? Errors);
}
