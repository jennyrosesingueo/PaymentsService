using System.ComponentModel.DataAnnotations;

namespace PaymentsService.Core.Models;

/*
 * API request model carrying payment creation input validated by data annotations.
 */
public class CreatePaymentRequest
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-letter ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be uppercase letters only, e.g. USD.")]
    public string Currency { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 1, ErrorMessage = "ReferenceId must be between 1 and 128 characters.")]
    public string ReferenceId { get; set; } = string.Empty;
}
