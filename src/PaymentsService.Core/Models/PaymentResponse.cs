using PaymentsService.Core.Enums;

namespace PaymentsService.Core.Models;

/*
 * API response model describing the current state of a payment.
 */
public class PaymentResponse
{
    public Guid Id { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
