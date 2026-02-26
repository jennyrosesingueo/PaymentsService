using PaymentsService.Core.Entities;

namespace PaymentsService.Core.Interfaces;

/*
 * Contract for persistence operations on payments in the data store.
 */
public interface IPaymentRepository
{
    Task<Payment?> GetByReferenceIdAsync(string referenceId, CancellationToken cancellationToken = default);
    Task<Payment> AddAsync(Payment payment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default);
}
