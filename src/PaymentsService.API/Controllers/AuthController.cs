using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PaymentsService.Core.Models;

namespace PaymentsService.API.Controllers;

/*
 * Handles authentication requests and issues JWTs for clients accessing the
 * PaymentsService API.
 */
[ApiController]
[Route("api/[controller]")]   
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Issues a short-lived JWT for authenticated clients.
    /// </summary>
    /// <remarks>
    /// Credentials are validated against configuration. Secrets are stored in
    /// environment variables / secrets manager — never in source code.
    /// </remarks>
    /// <response code="200">JWT issued successfully.</response>
    /// <response code="401">Invalid client credentials.</response>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public IActionResult GetToken([FromBody] TokenRequest request)
    {
        // Validate client credentials from config (loaded from env / secrets, not hard-coded).
        var validClientId = _configuration["Auth:ClientId"];
        var validClientSecret = _configuration["Auth:ClientSecret"];

        if (string.IsNullOrEmpty(validClientId) || string.IsNullOrEmpty(validClientSecret))
        {
            _logger.LogError("Auth credentials are not configured.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse { Code = "CONFIG_ERROR", Message = "Authentication is not configured." });
        }

        if (!string.Equals(request.ClientId, validClientId, StringComparison.Ordinal) ||
            !string.Equals(request.ClientSecret, validClientSecret, StringComparison.Ordinal))
        {
            _logger.LogWarning("Failed login attempt. ClientId={ClientId}", request.ClientId);
            return Unauthorized(new ErrorResponse
            {
                Code = "INVALID_CREDENTIALS",
                Message = "The client credentials are invalid."
            });
        }

        var token = GenerateJwt(request.ClientId);
        return Ok(token);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private TokenResponse GenerateJwt(string clientId)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var secretKey = jwtSection["Key"]
            ?? throw new InvalidOperationException("JWT key is not configured.");

        var issuer = jwtSection["Issuer"] ?? "PaymentsService";
        var audience = jwtSection["Audience"] ?? "PaymentsServiceClients";
        var expiryMinutes = int.TryParse(jwtSection["ExpiryMinutes"], out var mins) ? mins : 60;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, clientId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var expiry = DateTime.UtcNow.AddMinutes(expiryMinutes);
        var token = new JwtSecurityToken(issuer, audience, claims,
            expires: expiry, signingCredentials: credentials);

        return new TokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresIn = expiryMinutes * 60
        };
    }
}
