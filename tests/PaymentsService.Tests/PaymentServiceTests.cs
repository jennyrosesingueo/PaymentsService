using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaymentsService.Core.Entities;
using PaymentsService.Core.Enums;
using PaymentsService.Core.Interfaces;
using PaymentsService.Core.Models;
using PaymentsService.Services;

namespace PaymentsService.Tests;

/*
 * Verifies the payment service behavior across happy paths, idempotency, and
 * retrieval scenarios in the test suite.
 */
/// <summary>
/// Defines the test suite for <see cref="PaymentService"/>, validating payment creation,
/// idempotency, and retrieval behaviors.
/// </summary>
/// <remarks>
/// In Clean Architecture, this class belongs to the test boundary and verifies application
/// service behavior by isolating domain logic from infrastructure concerns. It depends on
/// <see cref="IPaymentRepository"/> (mocked via <c>Moq</c>) and <see cref="NullLogger{TCategoryName}"/>
/// to run deterministic unit tests. Idempotency is ensured by asserting that duplicate reference
/// requests return the existing payment without calling repository add, and transaction safety
/// is verified by checking that persistence is invoked only on valid, non-duplicate flows.
/// </remarks>
public class PaymentServiceTests
{
    private readonly Mock<IPaymentRepository> _repositoryMock;
    private readonly PaymentService _sut;

    public PaymentServiceTests()
    {
        _repositoryMock = new Mock<IPaymentRepository>();
        _sut = new PaymentService(_repositoryMock.Object, NullLogger<PaymentService>.Instance);
    }

    // -------------------------------------------------------------------------
    // CreatePaymentAsync — Happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreatePaymentAsync_ValidRequest_ReturnsCompletedPayment()
    {
        // Arrange
        var request = new CreatePaymentRequest
        {
            Amount = 100.00m,
            Currency = "USD",
            ReferenceId = "ref-001"
        };

        _repositoryMock
            .Setup(r => r.GetByReferenceIdAsync(request.ReferenceId, default))
            .ReturnsAsync((Payment?)null);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Payment>(), default))
            .ReturnsAsync((Payment p, CancellationToken _) => p);

        // Act
        var result = await _sut.CreatePaymentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.ReferenceId.Should().Be(request.ReferenceId);
        result.Amount.Should().Be(request.Amount);
        result.Currency.Should().Be(request.Currency);
        result.Status.Should().Be(PaymentStatus.Completed.ToString());
        result.FailureReason.Should().BeNull();
    }

    [Fact]
    public async Task CreatePaymentAsync_AmountOver50000_ReturnsProcessingStatus()
    {
        // Arrange
        var request = new CreatePaymentRequest
        {
            Amount = 100_000m,
            Currency = "USD",
            ReferenceId = "ref-large"
        };

        _repositoryMock
            .Setup(r => r.GetByReferenceIdAsync(request.ReferenceId, default))
            .ReturnsAsync((Payment?)null);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Payment>(), default))
            .ReturnsAsync((Payment p, CancellationToken _) => p);

        // Act
        var result = await _sut.CreatePaymentAsync(request);

        // Assert
        result.Status.Should().Be(PaymentStatus.Processing.ToString());
    }

    [Fact]
    public async Task CreatePaymentAsync_XtsCurrency_ReturnsRejectedStatus()
    {
        // Arrange
        var request = new CreatePaymentRequest
        {
            Amount = 50m,
            Currency = "XTS",
            ReferenceId = "ref-xts"
        };

        _repositoryMock
            .Setup(r => r.GetByReferenceIdAsync(request.ReferenceId, default))
            .ReturnsAsync((Payment?)null);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Payment>(), default))
            .ReturnsAsync((Payment p, CancellationToken _) => p);

        // Act
        var result = await _sut.CreatePaymentAsync(request);

        // Assert
        result.Status.Should().Be(PaymentStatus.Rejected.ToString());
        result.FailureReason.Should().Contain("XTS");
    }

    // -------------------------------------------------------------------------
    // Idempotency
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreatePaymentAsync_DuplicateReferenceId_ReturnsExistingPayment_WithoutAddingNew()
    {
        // Arrange
        var existing = new Payment
        {
            Id = Guid.NewGuid(),
            ReferenceId = "ref-duplicate",
            Amount = 250m,
            Currency = "EUR",
            Status = PaymentStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        _repositoryMock
            .Setup(r => r.GetByReferenceIdAsync(existing.ReferenceId, default))
            .ReturnsAsync(existing);

        var request = new CreatePaymentRequest
        {
            Amount = 999m,          // different amount — should be ignored
            Currency = "GBP",       // different currency — should be ignored
            ReferenceId = existing.ReferenceId
        };

        // Act
        var result = await _sut.CreatePaymentAsync(request);

        // Assert — the stored payment is returned, not the new request's data
        result.Id.Should().Be(existing.Id);
        result.Amount.Should().Be(existing.Amount);
        result.Currency.Should().Be(existing.Currency);
        result.Status.Should().Be(PaymentStatus.Completed.ToString());

        // Ensure AddAsync was never called (no duplicate insertion)
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // GetPaymentByReferenceIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPaymentByReferenceIdAsync_ExistingRef_ReturnsMappedResponse()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var existing = new Payment
        {
            Id = paymentId,
            ReferenceId = "ref-get",
            Amount = 75m,
            Currency = "USD",
            Status = PaymentStatus.Completed
        };

        _repositoryMock
            .Setup(r => r.GetByReferenceIdAsync("ref-get", default))
            .ReturnsAsync(existing);

        // Act
        var result = await _sut.GetPaymentByReferenceIdAsync("ref-get");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(paymentId);
        result.Status.Should().Be(PaymentStatus.Completed.ToString());
    }

    [Fact]
    public async Task GetPaymentByReferenceIdAsync_UnknownRef_ReturnsNull()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByReferenceIdAsync("unknown", default))
            .ReturnsAsync((Payment?)null);

        // Act
        var result = await _sut.GetPaymentByReferenceIdAsync("unknown");

        // Assert
        result.Should().BeNull();
    }
}
