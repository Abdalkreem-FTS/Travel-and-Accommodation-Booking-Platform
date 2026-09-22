using System.Net.Http.Headers;
using System.Net.Http.Json;

using HotelBooking.Application.Authentication.Dtos;
using HotelBooking.Application.Users.Dtos;

namespace HotelBooking.Api.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}

[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public abstract class IntegrationTestBase(ApiFactory factory) : IAsyncLifetime
{
    private const string Password = "abdalkreem-mahmoud-bzoor";

    protected ApiFactory Factory { get; } = factory;

    protected HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
        await Factory.ResetCacheAsync();

        Factory.Spans.Clear();

        Client = Factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    protected Task<HttpResponseMessage> RegisterAsync(
        string email,
        string password = Password,
        string firstName = "Abdalkreem",
        string lastName = "Bzoor",
        HttpClient? client = null) =>
        (client ?? Client).PostAsJsonAsync(
            "/api/users",
            new RegisterUserRequest(email, password, firstName, lastName),
            TestContext.Current.CancellationToken);

    protected Task<HttpResponseMessage> LoginAsync(
        string email,
        string password = Password,
        HttpClient? client = null) =>
        (client ?? Client).PostAsJsonAsync(
            "/api/sessions",
            new LoginRequest(email, password),
            TestContext.Current.CancellationToken);

    protected Task<HttpResponseMessage> LogoutAsync(string accessToken, HttpClient? client = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/sessions/current");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return (client ?? Client).SendAsync(request, TestContext.Current.CancellationToken);
    }

    protected Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        Client.PutAsJsonAsync(
            "/api/sessions/current",
            new RefreshSessionRequest(refreshToken),
            TestContext.Current.CancellationToken);

    protected async Task<SessionDto> SignUpAndLogInAsync(string email)
    {
        (await RegisterAsync(email)).EnsureSuccessStatusCode();

        var response = await LoginAsync(email);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<SessionDto>(
            TestContext.Current.CancellationToken))!;
    }

    protected async Task<SessionDto> SignUpAndLogInAsAdminAsync(string email)
    {
        (await RegisterAsync(email)).EnsureSuccessStatusCode();

        await Factory.PromoteToAdminAsync(email);

        var response = await LoginAsync(email);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<SessionDto>(
            TestContext.Current.CancellationToken))!;
    }

    protected async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string route,
        string? accessToken = null,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, route);
        request.Content = content;

        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
