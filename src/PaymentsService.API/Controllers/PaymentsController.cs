using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentsService.Core.Interfaces;
using PaymentsService.Core.Models;

namespace PaymentsService.API.Controllers;
 
/*
 * Exposes payment management endpoints, coordinating request validation and
 * service calls for the public API.
 */
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// Initiates a payment. Idempotent — repeating the same ReferenceId returns the stored result.
    /// </summary>
    /// <response code="200">Payment already exists (idempotency hit); returns stored result.</response>
    /// <response code="201">Payment accepted and processed.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreatePayment(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);

            return BadRequest(new ErrorResponse
            {
                Code = "VALIDATION_ERROR",
                Message = "One or more validation errors occurred.",
                Details = errors
            });
        }

        // Check idempotency: did this ReferenceId already get processed?
        var existing = await _paymentService.GetPaymentByReferenceIdAsync(request.ReferenceId, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Idempotency hit for ReferenceId={ReferenceId}", request.ReferenceId);
            // 200 OK signals "already processed" to the caller
            return Ok(existing);
        }

        var result = await _paymentService.CreatePaymentAsync(request, cancellationToken);

        // 201 Created for a freshly processed payment, with Location header
        return CreatedAtAction(
            nameof(GetPayment),
            new { referenceId = result.ReferenceId },
            result);
    }

    /// <summary>
    /// Retrieves the current state of a payment by its ReferenceId.
    /// </summary>
    /// <response code="200">Payment found.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="404">No payment with that ReferenceId.</response>
    [HttpGet("{referenceId}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPayment(
        [FromRoute] string referenceId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(referenceId))
            return BadRequest(new ErrorResponse
            {
                Code = "INVALID_ARGUMENT",
                Message = "referenceId must not be empty."
            });

        var payment = await _paymentService.GetPaymentByReferenceIdAsync(referenceId, cancellationToken);

        if (payment is null)
            return NotFound(new ErrorResponse
            {
                Code = "PAYMENT_NOT_FOUND",
                Message = $"No payment found with ReferenceId '{referenceId}'."
            });

        return Ok(payment);
    }
}
