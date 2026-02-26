using PaymentsService.Core.Enums;

namespace PaymentsService.Core.Entities;

/*
 * Represents a payment record persisted to storage and used across the domain
 * and infrastructure layers.
 */
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Client-supplied idempotency key.</summary>
    public string ReferenceId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency code, e.g. "USD".</summary>
    public string Currency { get; set; } = string.Empty;

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}
