# Architecture Overview

This service is a .NET 9 Web API for processing payments. It follows Clean Architecture principles to
keep business rules isolated from infrastructure concerns and to make each layer independently testable.

---

## Clean Architecture Layers

The solution is split into four projects, each with a clearly scoped responsibility:

### API Layer — `PaymentsService.API`
The entry point for all HTTP traffic. Contains:
- **Controllers** (`PaymentsController`, `AuthController`) — receive HTTP requests, validate `ModelState`,
  delegate to the service layer, and return appropriate HTTP status codes (`200 OK`, `201 Created`,
  `404 Not Found`, etc.).
- **Middleware** (`ExceptionHandlingMiddleware`) — catches unhandled exceptions from any layer and maps
  them to a consistent JSON error envelope (`ErrorResponse`) so callers always receive a structured body
  rather than a raw 500 stack trace.
- **JWT authentication** — all payment endpoints require a valid bearer token (`[Authorize]`).

The API layer depends on `Core` interfaces only; it never imports `Services` or `Infrastructure` directly.

### Application Layer — `PaymentsService.Core` (Interfaces & Models)
Defines the contracts the rest of the system is built around:
- **`IPaymentService`** — business operations: `CreatePaymentAsync`, `GetPaymentByReferenceIdAsync`.
- **`IPaymentRepository`** — data-access operations: `GetByReferenceIdAsync`, `AddAsync`, `UpdateAsync`.
- **Request / response models** (`CreatePaymentRequest`, `PaymentResponse`, `ErrorResponse`) — DTOs
  that cross layer boundaries without leaking EF Core entities to callers.

Nothing in `Core` references a database, an HTTP stack, or any framework detail.

### Domain Layer — `PaymentsService.Core` (Entities & Enums)
Contains the pure domain objects:
- **`Payment`** entity — `Id` (GUID), `ReferenceId`, `Amount`, `Currency` (ISO 4217), `Status`,
  `FailureReason`, `CreatedAt`, `UpdatedAt`.
- **`PaymentStatus`** enum — `Pending`, `Approved`, `Rejected`.
- **`DuplicatePaymentException`** — a typed domain exception raised when a duplicate key is detected.

These types carry no framework attributes and can be used in tests without standing up any infrastructure.

### Infrastructure Layer — `PaymentsService.Infrastructure`
Implements the repository interfaces using Entity Framework Core:
- **`PaymentsDbContext`** — configures the `Payments` table, column precision (`decimal(18,4)`), max
  lengths, and the unique index on `ReferenceId`. Stores `Status` as a string for readability.
- **`PaymentRepository`** — thin wrapper over `DbContext`. Read queries use `AsNoTracking()` for
  performance. Write operations call `SaveChangesAsync` immediately, keeping transactions short.

### Services Layer — `PaymentsService.Services`
Implements `IPaymentService`, containing all business logic:
- **`PaymentService`** — performs the idempotency check, decides the payment outcome (`DeterminePaymentStatus`),
  persists the new `Payment` through the repository, and maps the entity back to a `PaymentResponse`.
  This layer has no knowledge of HTTP or EF Core; it only depends on `Core` interfaces.

---

## Transaction Boundaries

Each payment write is wrapped in a single implicit EF Core transaction:
1. The service checks for an existing `ReferenceId` via a read query.
2. If not found, a new `Payment` is created and persisted with a single `SaveChangesAsync` call.
3. If the database unique constraint on `ReferenceId` is violated concurrently, EF Core throws a
   `DbUpdateException` which surfaces as a `409 Conflict` through the exception middleware.

Keeping transactions short (one read + one write) minimises lock contention under load.

---

## Idempotency Strategy

Idempotency is enforced at two independent layers so that no duplicate payment can slip through:

1. **Service-level check** — `PaymentService.CreatePaymentAsync` calls
   `_repository.GetByReferenceIdAsync` before inserting. If a record is found, it returns the stored
   result immediately without re-processing.
2. **Controller-level check** — `PaymentsController.CreatePayment` also calls
   `GetPaymentByReferenceIdAsync` before delegating to `CreatePaymentAsync`. A `200 OK` is returned
   (rather than `201 Created`) to signal to the caller that the response is a replay of a prior result.
3. **Database constraint** — `PaymentsDbContext` defines a unique index (`IX_Payments_ReferenceId`) on
   the `ReferenceId` column. Even if two concurrent requests pass both application-level checks
   simultaneously, the database rejects the second insert, preventing phantom duplicates.

---

## Reliability Strategy

| Concern | Mechanism |
|---|---|
| Invalid input | `[ApiController]` + `ModelState` validation returns `400 Bad Request` before any business logic runs |
| Domain rule violations | Typed exceptions (`DuplicatePaymentException`, `ArgumentException`) caught by `ExceptionHandlingMiddleware` |
| Concurrent duplicates | Database unique index causes `DbUpdateException` → mapped to `409 Conflict` |
| Unexpected failures | Global middleware catches all unhandled exceptions and returns `500 Internal Server Error` with a safe message |
| Sensitive data | Stack traces and internal messages are never exposed to the HTTP caller in production |
| Performance | Read queries use `AsNoTracking()` to skip EF change-tracking overhead |

---

## Future Improvements

- **Distributed cache (Redis) for idempotency** — store a short-lived idempotency key in Redis on first
  receipt so duplicate requests can be rejected before hitting the database at all, reducing DB load at
  high request rates.
- **Message queue for async processing** — decouple payment acceptance from fulfilment by publishing
  `PaymentCreated` events to a queue (e.g. Azure Service Bus, RabbitMQ). The API returns `202 Accepted`
  immediately while a background worker processes and updates status.
- **Circuit breaker pattern** — wrap downstream calls (payment processor, fraud service) with a circuit
  breaker (e.g. Polly) to stop cascading failures and provide fast-fail responses when a dependency is
  unhealthy.
- **Outbox pattern** — guarantee at-least-once event delivery by writing the event to an outbox table in
  the same transaction as the payment, then relaying it asynchronously.
- **ClientId scoping** — extend the unique index to `(ClientId, ReferenceId)` so each tenant has its own
  idempotency namespace, enabling multi-tenant support without key collisions.
