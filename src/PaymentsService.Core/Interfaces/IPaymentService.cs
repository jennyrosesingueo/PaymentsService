using PaymentsService.Core.Models;

namespace PaymentsService.Core.Interfaces;

/*
 * Contract for payment operations used by the API and other consumers.
 */
public interface IPaymentService
{
    Task<PaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentResponse?> GetPaymentByReferenceIdAsync(string referenceId, CancellationToken cancellationToken = default);
}
