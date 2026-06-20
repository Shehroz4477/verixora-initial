using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Identity.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(Guid userId, string phoneNumber, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured")));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.MobilePhone, phoneNumber),
            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "Verixora",
            audience: _configuration["Jwt:Issuer"] ?? "Verixora",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
