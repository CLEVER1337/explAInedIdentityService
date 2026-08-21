using System.Net;
using System.Net.Http.Json;

public class AccountEndpointsTests : IClassFixture<IdentityWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AccountEndpointsTests(IdentityWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidUser_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/user", new
        {
            Email = $"ok-{Guid.NewGuid():N}@example.com",
            Password = "Password1",
            ConfirmPassword = "Password1",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_WeakPassword_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/user", new
        {
            Email = $"weak-{Guid.NewGuid():N}@example.com",
            Password = "abc",
            ConfirmPassword = "abc",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        var payload = new
        {
            Email = email,
            Password = "Password1",
            ConfirmPassword = "Password1",
        };

        var first = await _client.PostAsJsonAsync("/user", payload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/user", payload);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }
}
