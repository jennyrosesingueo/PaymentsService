using Microsoft.Extensions.Logging;
using PaymentsService.Core.Entities;
using PaymentsService.Core.Enums;
using PaymentsService.Core.Exceptions;
using PaymentsService.Core.Interfaces;
using PaymentsService.Core.Models;

namespace PaymentsService.Services;

/*
 * Handles payment processing rules, idempotency checks, and response mapping
 * for the core payment workflow used by the API layer.
 */
public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _repository;
    private readonly ILogger<PaymentService> _logger;

    // Simulated high-risk currencies / amounts that will be rejected
    private static readonly HashSet<string> _rejectedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "XTS" // ISO 4217 reserved "for testing" — always reject
    };

    public PaymentService(IPaymentRepository repository, ILogger<PaymentService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new payment or returns the existing result if the ReferenceId was already processed
    /// (idempotency guarantee).
    /// </summary>
    public async Task<PaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing payment request. ReferenceId={ReferenceId}, Amount={Amount}, Currency={Currency}",
            request.ReferenceId, request.Amount, request.Currency);

        // --- Idempotency check ---
        var existing = await _repository.GetByReferenceIdAsync(request.ReferenceId, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Duplicate payment detected. ReferenceId={ReferenceId}, Status={Status}",
                request.ReferenceId, existing.Status);

            // Return the stored result instead of re-processing.
            // This satisfies idempotency: same request → same response.
            return MapToResponse(existing);
        }

        // --- Simulate payment processing ---
        var status = DeterminePaymentStatus(request);
        string? failureReason = status == PaymentStatus.Rejected
            ? $"Currency '{request.Currency}' is not accepted."
            : null;

        var payment = new Payment
        {
            ReferenceId = request.ReferenceId,
            Amount = request.Amount,
            Currency = request.Currency.ToUpperInvariant(),
            Status = status,
            FailureReason = failureReason
        };

        await _repository.AddAsync(payment, cancellationToken);

        _logger.LogInformation(
            "Payment created. PaymentId={PaymentId}, Status={Status}",
            payment.Id, payment.Status);

        return MapToResponse(payment);
    }

    /// <summary>
    /// Retrieves a payment by its idempotency key (ReferenceId).
    /// </summary>
    public async Task<PaymentResponse?> GetPaymentByReferenceIdAsync(
        string referenceId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _repository.GetByReferenceIdAsync(referenceId, cancellationToken);
        return payment is null ? null : MapToResponse(payment);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static PaymentStatus DeterminePaymentStatus(CreatePaymentRequest request)
    {
        // Reject known bad currencies
        if (_rejectedCurrencies.Contains(request.Currency))
            return PaymentStatus.Rejected;

        // Simulate large amounts as "processing" (async gateway simulation)
        if (request.Amount > 50_000m)
            return PaymentStatus.Processing;

        // All other payments succeed
        return PaymentStatus.Completed;
    }

    private static PaymentResponse MapToResponse(Payment payment) => new()
    {
        Id = payment.Id,
        ReferenceId = payment.ReferenceId,
        Amount = payment.Amount,
        Currency = payment.Currency,
        Status = payment.Status.ToString(),
        FailureReason = payment.FailureReason,
        CreatedAt = payment.CreatedAt,
        UpdatedAt = payment.UpdatedAt
    };
}
