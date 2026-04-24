using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

public class TokenServiceTests
{
    private const string JwtKey = "JwtSecretPlaceHolder_explAIned32";
    private const string Issuer = "http://localhost:5125/";
    private const string Audience = "http://localhost:5125/";

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = JwtKey,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:AccessDurationInMinutes"] = "15",
                ["Jwt:RefreshDurationInMinutes"] = "10080",
            })
            .Build();

    private static (TokenService svc, Mock<IDistributedCache> cache) Build()
    {
        var cache = new Mock<IDistributedCache>();
        var cacheSvc = new CacheService(cache.Object, NullLogger<CacheService>.Instance);
        var svc = new TokenService(BuildConfig(), cacheSvc);
        return (svc, cache);
    }

    private static IdentityUser SampleUser() => new()
    {
        Id = "user-123",
        Email = "user@example.com",
    };

    [Fact]
    public void GenerateAccessToken_ContainsExpectedClaims()
    {
        var (svc, _) = Build();

        var token = svc.GenerateAccessToken(SampleUser());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(Issuer, jwt.Issuer);
        Assert.Contains(Audience, jwt.Audiences);
        Assert.Equal("user-123", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("user@example.com", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("access", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Typ).Value);
        Assert.False(string.IsNullOrEmpty(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value));
    }

    [Fact]
    public void GenerateRefreshToken_MarkedAsRefresh()
    {
        var (svc, _) = Build();

        var token = svc.GenerateRefreshToken(SampleUser());

        var typ = new JwtSecurityTokenHandler()
            .ReadJwtToken(token)
            .Claims.First(c => c.Type == JwtRegisteredClaimNames.Typ).Value;
        Assert.Equal("refresh", typ);
    }

    [Fact]
    public void GenerateAccessToken_RespectsAccessDuration()
    {
        var (svc, _) = Build();
        var before = DateTime.UtcNow;

        var token = svc.GenerateAccessToken(SampleUser());

        var exp = new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;
        var diff = exp - before;
        Assert.InRange(diff.TotalMinutes, 14, 16);
    }

    [Fact]
    public async Task GetTokenField_ReturnsClaimValue()
    {
        var (svc, _) = Build();
        var token = svc.GenerateAccessToken(SampleUser());

        var email = await svc.GetTokenField(token, JwtRegisteredClaimNames.Email);

        Assert.Equal("user@example.com", email);
    }

    [Fact]
    public async Task GetTokenField_ReturnsNullForMissingClaim()
    {
        var (svc, _) = Build();
        var token = svc.GenerateAccessToken(SampleUser());

        var value = await svc.GetTokenField(token, "nonexistent");

        Assert.Null(value);
    }

    [Fact]
    public async Task RefreshTokens_RejectsAccessToken()
    {
        var (svc, _) = Build();
        var access = svc.GenerateAccessToken(SampleUser());

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RefreshTokens(access));
    }

    [Fact]
    public async Task RefreshTokens_ReturnsValidPair()
    {
        var (svc, _) = Build();
        var refresh = svc.GenerateRefreshToken(SampleUser());

        var (access, newRefresh) = await svc.RefreshTokens(refresh);

        var handler = new JwtSecurityTokenHandler();
        Assert.Equal("access", handler.ReadJwtToken(access).Claims.First(c => c.Type == JwtRegisteredClaimNames.Typ).Value);
        Assert.Equal("refresh", handler.ReadJwtToken(newRefresh).Claims.First(c => c.Type == JwtRegisteredClaimNames.Typ).Value);
        Assert.Equal("user-123", handler.ReadJwtToken(access).Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
    }

    [Fact]
    public async Task BlackListToken_RejectsAccessToken()
    {
        var (svc, _) = Build();
        var access = svc.GenerateAccessToken(SampleUser());

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.BlackListToken(access));
    }

    [Fact]
    public async Task BlackListToken_StoresInCacheWithRemainingTtl()
    {
        var (svc, cache) = Build();
        var refresh = svc.GenerateRefreshToken(SampleUser());

        await svc.BlackListToken(refresh);

        cache.Verify(c => c.SetAsync(
                refresh,
                It.IsAny<byte[]>(),
                It.Is<DistributedCacheEntryOptions>(o =>
                    o.AbsoluteExpirationRelativeToNow.HasValue &&
                    o.AbsoluteExpirationRelativeToNow.Value.TotalMinutes > 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IsBlackListed_TrueWhenCacheHit()
    {
        var (svc, cache) = Build();
        cache.Setup(c => c.GetAsync("some-token", It.IsAny<CancellationToken>()))
             .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(""));

        var result = await svc.IsBlackListed("some-token");

        Assert.True(result);
    }

    [Fact]
    public async Task IsBlackListed_FalseWhenCacheMiss()
    {
        var (svc, cache) = Build();
        cache.Setup(c => c.GetAsync("some-token", It.IsAny<CancellationToken>()))
             .ReturnsAsync((byte[]?)null);

        var result = await svc.IsBlackListed("some-token");

        Assert.False(result);
    }
}
