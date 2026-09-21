using EduPlatform.Core.Models;
using EduPlatform.Core.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EduPlatform.Tests;

public class AuthServiceTests
{
    private readonly AuthService _auth = new(BuildConfiguration());

    [Fact]
    public void HashPassword_CreatesVerifiableHash()
    {
        const string password = "Student123!";

        var hash = _auth.HashPassword(password);

        Assert.NotEqual(password, hash);
        Assert.True(_auth.VerifyPassword(password, hash));
        Assert.False(_auth.VerifyPassword("WrongPassword", hash));
    }

    [Fact]
    public void GenerateToken_ReturnsJwtForUser()
    {
        var user = new User
        {
            Email = "student@example.com",
            FirstName = "Student",
            Role = UserRole.Student
        };

        var token = _auth.GenerateToken(user);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public void GenerateToken_ContainsUserIdentityAndRoleClaims()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            FirstName = "Admin",
            Role = UserRole.Admin
        };

        var token = _auth.GenerateToken(user);
        var claims = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(token)
            .Claims
            .ToDictionary(claim => claim.Type, claim => claim.Value);

        Assert.Equal(user.Id.ToString(), claims[System.Security.Claims.ClaimTypes.NameIdentifier]);
        Assert.Equal(user.Email, claims[System.Security.Claims.ClaimTypes.Email]);
        Assert.Equal("Admin", claims[System.Security.Claims.ClaimTypes.Role]);
        Assert.Equal(user.FirstName, claims["firstName"]);
    }

    [Fact]
    public void RefreshTokenExpiration_UsesConfiguredDaysAndDefault()
    {
        var configured = new AuthService(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RefreshTokenExpirationDays"] = "45"
            })
            .Build());
        var defaulted = new AuthService(new ConfigurationBuilder().Build());

        Assert.Equal(45, configured.GetRefreshTokenExpirationDays());
        Assert.Equal(30, defaulted.GetRefreshTokenExpirationDays());
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "EduPlatformTestsSecretKey2026LongEnough!",
                ["Jwt:Issuer"] = "EduPlatform.Tests",
                ["Jwt:Audience"] = "EduPlatform.TestUsers",
                ["Jwt:ExpirationHours"] = "1"
            })
            .Build();
    }
}
