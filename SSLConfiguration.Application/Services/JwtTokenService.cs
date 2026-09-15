using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace SSLConfiguration.Application;

public class JwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public string CreateToken(string pin, int? productId = null, int? storeId = null)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        var claims = new List<Claim>
        {
            new Claim("pin", pin ?? string.Empty)
        };

        if (productId.HasValue)
            claims.Add(new Claim("productId", productId.Value.ToString()));
        if (storeId.HasValue)
            claims.Add(new Claim("storeId", storeId.Value.ToString()));

        var minutes = int.TryParse(_config["Jwt:ExpireMinutes"], out var m) ? m : 120;

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Admin JWT for Refund APIs (replaces old Session["AdminUserName"]).
    /// </summary>
    public string CreateAdminToken(string email)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Email, email ?? string.Empty),
            new Claim("adminUserName", email ?? string.Empty)
        };

        var minutes = int.TryParse(_config["Jwt:ExpireMinutes"], out var m) ? m : 120;

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}