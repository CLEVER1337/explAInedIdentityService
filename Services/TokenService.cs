using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

public class TokenService
{
    private readonly IConfiguration _configuration;
    private readonly CacheService _cacheService;

    public TokenService(IConfiguration configuration, CacheService cacheService)
    {
        _configuration = configuration;
        _cacheService = cacheService;
    }
    
    private string GenerateToken(List<Claim> claims, double durationMinutes)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(durationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateAccessToken(IdentityUser user)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Typ, "access")
        };

        return GenerateToken(claims, Convert.ToDouble(_configuration["Jwt:AccessDurationInMinutes"]));
    }

    public string GenerateRefreshToken(IdentityUser user)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Typ, "refresh")
        };

        return GenerateToken(claims, Convert.ToDouble(_configuration["Jwt:RefreshDurationInMinutes"]));
    }

    public async Task<string> GetTokenField(string token, string field)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var claim = jwtToken.Claims.FirstOrDefault(c => c.Type == field);

        return claim?.Value;
    }

    public async Task<(string accessToken, string refreshToken)> RefreshTokens(string refreshToken)
    {
        var type = await GetTokenField(refreshToken, JwtRegisteredClaimNames.Typ);

        if (type != "refresh")
        {
            throw new InvalidOperationException("Only refresh tokens can be blacklisted.");
        }

        var email = await GetTokenField(refreshToken, JwtRegisteredClaimNames.Email);
        var id = await GetTokenField(refreshToken, JwtRegisteredClaimNames.Sub);

        if (email == null)
            throw new SecurityTokenException("Invalid refresh token.");

        var newAccessToken = GenerateAccessToken(new IdentityUser { Id = id, Email = email });
        var newRefreshToken = GenerateRefreshToken(new IdentityUser { Id = id, Email = email });

        return (newAccessToken, newRefreshToken);
    }

    public async Task<bool> IsBlackListed(string token)
    {
        var blackListedToken = await _cacheService.GetValue(token);
        return blackListedToken != null;
    }

    public async Task BlackListToken(string token)
    {
        var type = await GetTokenField(token, JwtRegisteredClaimNames.Typ);

        if (type != "refresh")
        {
            throw new InvalidOperationException("Only refresh tokens can be blacklisted.");
        }

        var exp = await GetTokenField(token, "exp");
        var expSeconds = long.Parse(exp);
        var expirationTime = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
        var remainingTime = expirationTime - DateTimeOffset.UtcNow;

        await _cacheService.SetValue(token, "", remainingTime);
    }
}