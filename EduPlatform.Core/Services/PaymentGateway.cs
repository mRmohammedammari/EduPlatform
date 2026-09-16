namespace EduPlatform.Core.Services;

public interface IPaymentGateway
{
    PaymentResult CreateCheckoutSession(PaymentRequest request);
    PaymentResult ValidatePayment(string paymentId, string? providerReference = null);
}

public class PaymentRequest
{
    public Guid CourseId { get; set; }
    public Guid UserId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "DZD";
    public string? RedirectUrl { get; set; }
    public string? CancelUrl { get; set; }
}

public class PaymentResult
{
    public bool IsSuccess { get; set; }
    public string? PaymentId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "DZD";
    public string Status { get; set; } = "Pending";
}

public class DemoPaymentGateway : IPaymentGateway
{
    public PaymentResult CreateCheckoutSession(PaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            return new PaymentResult
            {
                IsSuccess = false,
                ErrorMessage = "Le montant du paiement est invalide.",
                Amount = request.Amount,
                Currency = request.Currency
            };
        }

        var paymentId = $"demo_{Guid.NewGuid():N}";

        return new PaymentResult
        {
            IsSuccess = true,
            PaymentId = paymentId,
            CheckoutUrl = $"/checkout/success?payment={paymentId}",
            Amount = request.Amount,
            Currency = request.Currency,
            Status = "Created"
        };
    }

    public PaymentResult ValidatePayment(string paymentId, string? providerReference = null)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
        {
            return new PaymentResult
            {
                IsSuccess = false,
                ErrorMessage = "Identifiant de paiement manquant.",
                Status = "Failed"
            };
        }

        return new PaymentResult
        {
            IsSuccess = true,
            PaymentId = paymentId,
            Status = "Paid",
            Currency = "DZD",
            Amount = 0m
        };
    }
}
