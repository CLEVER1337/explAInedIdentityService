using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

public class SessionEndpointsTests : IClassFixture<IdentityWebApplicationFactory>
{
    private readonly IdentityWebApplicationFactory _factory;

    public SessionEndpointsTests(IdentityWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed record TokenPair(
        [property: JsonPropertyName("accessToken")] string AccessToken,
        [property: JsonPropertyName("refreshToken")] string RefreshToken);

    private async Task<(HttpClient client, string email, string password)> CreateUserAsync()
    {
        var client = _factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var password = "Password1";

        var reg = await client.PostAsJsonAsync("/user", new
        {
            Email = email,
            Password = password,
            ConfirmPassword = password,
            Nickname = "nick",
        });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        return (client, email, password);
    }

    private static async Task<TokenPair> LoginAsync(HttpClient client, string email, string password)
    {
        var resp = await client.PostAsJsonAsync("/session", new
        {
            Email = email,
            Password = password,
            RememberMe = false,
        });
        resp.EnsureSuccessStatusCode();
        var pair = await resp.Content.ReadFromJsonAsync<TokenPair>();
        Assert.NotNull(pair);
        Assert.False(string.IsNullOrWhiteSpace(pair!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(pair.RefreshToken));
        return pair;
    }

    [Fact]
    public async Task Login_WithValidCreds_ReturnsTokens()
    {
        var (client, email, password) = await CreateUserAsync();

        var pair = await LoginAsync(client, email, password);

        Assert.NotEqual(pair.AccessToken, pair.RefreshToken);
    }

    [Fact]
    public async Task Login_WithBadPassword_Returns401()
    {
        var (client, email, _) = await CreateUserAsync();

        var resp = await client.PostAsJsonAsync("/session", new
        {
            Email = email,
            Password = "WrongPassword1",
            RememberMe = false,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithRefreshToken_ReturnsNewPair()
    {
        var (client, email, password) = await CreateUserAsync();
        var pair = await LoginAsync(client, email, password);

        var req = new HttpRequestMessage(HttpMethod.Post, "/session/refresh");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pair.RefreshToken);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var newPair = await resp.Content.ReadFromJsonAsync<TokenPair>();
        Assert.NotNull(newPair);
        Assert.NotEqual(pair.AccessToken, newPair!.AccessToken);
        Assert.NotEqual(pair.RefreshToken, newPair.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsync("/session/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithBlacklistedToken_Returns400()
    {
        var (client, email, password) = await CreateUserAsync();
        var pair = await LoginAsync(client, email, password);

        // First refresh blacklists the original refresh token.
        var first = new HttpRequestMessage(HttpMethod.Post, "/session/refresh");
        first.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pair.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(first)).StatusCode);

        // Reusing the same refresh token is rejected.
        var second = new HttpRequestMessage(HttpMethod.Post, "/session/refresh");
        second.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pair.RefreshToken);
        var resp = await client.SendAsync(second);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Logout_WithRefreshToken_Returns200AndBlocksReuse()
    {
        var (client, email, password) = await CreateUserAsync();
        var pair = await LoginAsync(client, email, password);

        var logout = new HttpRequestMessage(HttpMethod.Delete, "/session");
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pair.RefreshToken);
        var logoutResp = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.OK, logoutResp.StatusCode);

        var refresh = new HttpRequestMessage(HttpMethod.Post, "/session/refresh");
        refresh.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pair.RefreshToken);
        var refreshResp = await client.SendAsync(refresh);
        Assert.Equal(HttpStatusCode.BadRequest, refreshResp.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var resp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/session"));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
