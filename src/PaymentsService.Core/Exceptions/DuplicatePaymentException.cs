namespace PaymentsService.Core.Exceptions;
 
/*
 * Exception raised when a payment with the same idempotency reference already exists.
 */
public class DuplicatePaymentException : Exception
{
    public string ReferenceId { get; }

    public DuplicatePaymentException(string referenceId)
        : base($"A payment with ReferenceId '{referenceId}' already exists.")
    {
        ReferenceId = referenceId;
    }
}
