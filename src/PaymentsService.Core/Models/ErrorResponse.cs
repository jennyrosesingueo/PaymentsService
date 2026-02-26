namespace PaymentsService.Core.Models;
 
/*
 * Standardized API error payload returned for validation and processing errors.
 */
public class ErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public IEnumerable<string>? Details { get; set; }
}
