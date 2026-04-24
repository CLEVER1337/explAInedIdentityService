using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

public class CacheServiceTests
{
    private static (CacheService svc, Mock<IDistributedCache> cache) Build()
    {
        var cache = new Mock<IDistributedCache>();
        var svc = new CacheService(cache.Object, NullLogger<CacheService>.Instance);
        return (svc, cache);
    }

    [Fact]
    public async Task SetValue_WritesStringWithExpiration()
    {
        var (svc, cache) = Build();
        var ttl = TimeSpan.FromMinutes(5);

        await svc.SetValue("k", "v", ttl);

        cache.Verify(c => c.SetAsync(
                "k",
                It.IsAny<byte[]>(),
                It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == ttl),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetValue_WithoutExpiration_PassesNull()
    {
        var (svc, cache) = Build();

        await svc.SetValue("k", "v");

        cache.Verify(c => c.SetAsync(
                "k",
                It.IsAny<byte[]>(),
                It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetValue_ReturnsStoredString()
    {
        var (svc, cache) = Build();
        cache.Setup(c => c.GetAsync("k", It.IsAny<CancellationToken>()))
             .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes("v"));

        var value = await svc.GetValue("k");

        Assert.Equal("v", value);
    }

    [Fact]
    public async Task GetValue_ReturnsNullWhenMissing()
    {
        var (svc, cache) = Build();
        cache.Setup(c => c.GetAsync("k", It.IsAny<CancellationToken>()))
             .ReturnsAsync((byte[]?)null);

        var value = await svc.GetValue("k");

        Assert.Null(value);
    }

    [Fact]
    public async Task RemoveValue_DelegatesToCache()
    {
        var (svc, cache) = Build();

        await svc.RemoveValue("k");

        cache.Verify(c => c.RemoveAsync("k", It.IsAny<CancellationToken>()), Times.Once);
    }
}
