using System.ComponentModel.DataAnnotations;
 
namespace PaymentsService.Core.Models;

/*
 * API request model for client credential authentication when requesting a JWT.
 */
public class TokenRequest
{
    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;
}
