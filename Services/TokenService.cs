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

    public async Task BlackListToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var type = jwtToken.Claims.FirstOrDefault(c => c.Type == "typ")?.Value;

        if (type != "refresh")
        {
            throw new InvalidOperationException("Only refresh tokens can be blacklisted.");
        }

        var exp = jwtToken.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
        var expSeconds = long.Parse(exp);
        var expirationTime = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
        var remainingTime = expirationTime - DateTimeOffset.UtcNow;

        await _cacheService.SetValue(token, "", remainingTime);
    }
}