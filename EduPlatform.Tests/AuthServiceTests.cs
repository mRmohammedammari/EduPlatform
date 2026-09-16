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
