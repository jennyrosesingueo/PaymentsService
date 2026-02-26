namespace PaymentsService.Core.Enums;

/*
 * Defines the lifecycle states a payment can move through in the system.
 */
public enum PaymentStatus
{
    Pending,
    Processing,
    Completed,
    Rejected,
    Failed
}
