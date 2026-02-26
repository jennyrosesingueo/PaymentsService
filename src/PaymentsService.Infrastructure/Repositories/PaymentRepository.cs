using Microsoft.EntityFrameworkCore;
using PaymentsService.Core.Entities;
using PaymentsService.Core.Interfaces;
using PaymentsService.Infrastructure.Data;

namespace PaymentsService.Infrastructure.Repositories;

/*
 * Provides data access for payments, encapsulating database queries and
 * persistence operations for the payments aggregate.
 */
public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentsDbContext _context;

    public PaymentRepository(PaymentsDbContext context)
    {
        _context = context;
    }

    // Retrieves a payment by its external reference identifier.
    public async Task<Payment?> GetByReferenceIdAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ReferenceId == referenceId, cancellationToken);
    }

    // Adds a new payment record to the data store.
    public async Task<Payment> AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);
        return payment;
    }

    // Updates an existing payment record in the data store.
    public async Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        payment.UpdatedAt = DateTimeOffset.UtcNow;
        _context.Payments.Update(payment);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
