namespace PaymentsService.Core.Models;

/*
 * API response model containing the issued JWT and its expiry metadata.
 */
public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
}
