using EduPlatform.Core.Models;
using EduPlatform.Core.Services;
using Xunit;

namespace EduPlatform.Tests;

public class PaymentGatewayTests
{
    [Fact]
    public void DemoPaymentGateway_AcceptsValidCourseEnrollment()
    {
        var gateway = new DemoPaymentGateway();

        var result = gateway.CreateCheckoutSession(new PaymentRequest
        {
            CourseId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CourseTitle = "C# Avancé",
            Plan = "Standard",
            Amount = 1490m,
            Currency = "DZD"
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.PaymentId);
        Assert.Equal("DZD", result.Currency);
        Assert.Equal(1490m, result.Amount);
    }

    [Fact]
    public void DemoPaymentGateway_RejectsInvalidAmount()
    {
        var gateway = new DemoPaymentGateway();

        var result = gateway.CreateCheckoutSession(new PaymentRequest
        {
            CourseId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CourseTitle = "C# Avancé",
            Plan = "Standard",
            Amount = -1m,
            Currency = "DZD"
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("montant", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
