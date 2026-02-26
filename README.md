# PaymentsService

A production-quality Payments API built with **ASP.NET Core 9 (.NET 9)**, following clean-architecture principles.

Developed in **Visual Studio 2022**.
---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Project Structure](#project-structure)
4. [Assumptions](#assumptions)
5. [Architecture Decisions](#architecture-decisions)
6. [Trade-offs](#trade-offs)
7. [Getting Started](#getting-started)
8. [How to Run](#how-to-run)
9. [API Reference](#api-reference)
10. [Security Notes](#security-notes)
11. [Running Tests](#running-tests)

---

## Overview

PaymentsService is a **RESTful Payments API** that allows authenticated clients to submit and query payment transactions.

### What it does

- **Accepts payment requests** — clients submit an amount, currency, and a caller-supplied `ReferenceId` to create a new payment.
- **Processes payments** — the service evaluates each request and assigns a status:
  - `Completed` — payment succeeded (amount ≤ 50,000)
  - `Processing` — payment is pending an asynchronous gateway response (amount > 50,000)
  - `Rejected` — payment was declined (e.g., unsupported currency such as `XTS`)
- **Guarantees idempotency** — submitting the same `ReferenceId` multiple times always returns the original result, preventing duplicate charges.
- **Retrieves payment status** — clients can look up a payment at any time using its `ReferenceId`.
- **Secures all endpoints** — every payment operation requires a valid **JWT bearer token**, obtained by authenticating with a `clientId` and `clientSecret`.
- **Persists data** — all payments are stored in a SQLite database via Entity Framework Core, with a unique index on `ReferenceId` to enforce idempotency at the database level.

### Tech stack

| Layer | Technology |
|---|---|
| API framework | ASP.NET Core 9 Web API |
| Language | C# 13 |
| ORM / Database | EF Core 9 + SQLite |
| Authentication | JWT Bearer tokens |
| IDE | Visual Studio 2022 |
| Testing | xUnit |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  PaymentsService.API  (Presentation)                        │
│  ─ AuthController     POST /api/auth/token                  │
│  ─ PaymentsController POST /api/payments                    │
│                       GET  /api/payments/{referenceId}      │
│  ─ ExceptionHandlingMiddleware                              │
└───────────────────────┬─────────────────────────────────────┘
                        │ DI (IPaymentService)
┌───────────────────────▼─────────────────────────────────────┐
│  PaymentsService.Services  (Business Logic)                 │
│  ─ PaymentService    idempotency, validation, processing    │
└───────────────────────┬─────────────────────────────────────┘
                        │ DI (IPaymentRepository)
┌───────────────────────▼─────────────────────────────────────┐
│  PaymentsService.Infrastructure  (Data Access)              │
│  ─ PaymentsDbContext (EF Core + SQLite)                     │
│  ─ PaymentRepository                                        │
└───────────────────────┬─────────────────────────────────────┘
                        │ Entities / Interfaces / Models
┌───────────────────────▼─────────────────────────────────────┐
│  PaymentsService.Core  (Domain)                             │
│  ─ Entities, Enums, Interfaces, Models, Exceptions          │
└─────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
PaymentsService/
├── src/
│   ├── PaymentsService.Core/          # Domain — entities, interfaces, DTOs
│   │   ├── Entities/Payment.cs
│   │   ├── Enums/PaymentStatus.cs
│   │   ├── Exceptions/DuplicatePaymentException.cs
│   │   ├── Interfaces/IPaymentRepository.cs
│   │   ├── Interfaces/IPaymentService.cs
│   │   └── Models/                    # DTOs
│   ├── PaymentsService.Infrastructure/ # EF Core + SQLite
│   │   ├── Data/PaymentsDbContext.cs
│   │   └── Repositories/PaymentRepository.cs
│   ├── PaymentsService.Services/      # Business logic
│   │   └── PaymentService.cs
│   └── PaymentsService.API/           # ASP.NET Core Web API
│       ├── Controllers/AuthController.cs
│       ├── Controllers/PaymentsController.cs
│       ├── Middleware/ExceptionHandlingMiddleware.cs
│       ├── Program.cs
│       ├── appsettings.json
│       └── appsettings.Development.json
└── tests/
    └── PaymentsService.Tests/
        └── PaymentServiceTests.cs
```

---

## Assumptions

- The service runs as an **internal/external API** for authenticated clients only.
- **JWT-based machine-to-machine authentication** is used (client_id + client_secret → token).
- The `ReferenceId` supplied by the caller acts as the **idempotency key**. The same `ReferenceId` always returns the same result regardless of how many times the request is replayed.
- Payment processing is **simulated**:
  - Amount ≤ 50,000 → `Completed`
  - Amount > 50,000 → `Processing` (represents an async external gateway)
  - Currency `XTS` → `Rejected` (ISO 4217 reserved "for testing")
- **SQLite** is used for persistence to keep the service self-contained and runnable without any external dependencies. In production, PostgreSQL or SQL Server would be used.
- Secrets (`Jwt:Key`, `Auth:ClientId`, `Auth:ClientSecret`) are stored in `appsettings.Development.json` **for development only**. In production they must come from environment variables or a secrets manager (e.g., Azure Key Vault, AWS Secrets Manager).

---

## Architecture Decisions

### Clean / Layered Architecture
Separating Core, Infrastructure, Services, and API enforces the **Dependency Inversion Principle** — inner layers never depend on outer layers. The API and Infrastructure depend on `Core` abstractions, not concrete types.

### Idempotency via Unique Index
The `ReferenceId` column has a **unique database index**. This provides a second line of defence against duplicates beyond the application-level check, protecting against concurrent race conditions.

### JWT Authentication
- Tokens expire (configurable via `Jwt:ExpiryMinutes`).
- `ClockSkew = TimeSpan.Zero` — no leniency for expired tokens.
- The `X-Token-Expired: true` header is added so clients can detect token expiry without parsing the error body.

### Global Exception Middleware
A single `ExceptionHandlingMiddleware` converts exceptions to structured RFC 7807-style JSON error responses. Controllers remain clean from try/catch boilerplate.

### EF Core with Explicit Unique Index
The `Payment` table is configured in `OnModelCreating` with:
- Precision on `Amount` (18, 4)
- `Status` stored as a string (readable in the database)
- Unique index on `ReferenceId` (`IX_Payments_ReferenceId`)

---

## Trade-offs

| Decision | Trade-off |
|---|---|
| SQLite | Simplicity over production-grade scalability. Swap to SQL Server/PostgreSQL via connection string + EF provider. |
| In-memory idempotency check before DB insert | Reduces DB round-trips on happy path; race condition possible with multiple pods — mitigated by the unique DB index. |
| Simulated payment processing | No real gateway integration. Status determination is deterministic (amount/currency rules). |
| JWT credentials in appsettings.Development.json | Acceptable for local dev; never deployed to production. |
| No refresh tokens | Out of scope; machine-to-machine auth re-issues short-lived tokens. |

---

## Getting Started

### Prerequisites
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (Community, Professional, or Enterprise) with the **ASP.NET and web development** workload installed
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

> **Note:** This project was created and developed in **Visual Studio 2022**. Opening `PaymentsService.sln` directly in Visual Studio 2022 is the recommended way to work with this solution.

### Configure Secrets

For local development the defaults in `appsettings.Development.json` are pre-filled.

For other environments, set these **environment variables** (never edit `appsettings.json` with real secrets):

```bash
Jwt__Key=<min-32-char-secret>
Auth__ClientId=<your-client-id>
Auth__ClientSecret=<your-client-secret>
```

---

## How to Run

### Option 1 — Visual Studio 2022 (Recommended)

1. Open **`PaymentsService.sln`** in Visual Studio 2022.
2. In the **Solution Explorer**, right-click `PaymentsService.API` → **Set as Startup Project**.
3. Select the desired launch profile (`https` or `http`) from the run dropdown.
4. Press **F5** (debug) or **Ctrl+F5** (run without debug).

Visual Studio will restore NuGet packages, build the solution, apply EF Core migrations automatically, and launch the API. The Swagger UI will open in your browser at `https://localhost:5001/swagger`.

### Option 2 — .NET CLI

```bash
# From the repository root
cd src/PaymentsService.API
dotnet run
```

The API listens on `https://localhost:5001` and `http://localhost:5000` by default.

### Option 3 — Visual Studio 2022 Terminal (Package Manager Console or Developer PowerShell)

```powershell
cd src\PaymentsService.API
dotnet run
```

### Verify the API is running

Open your browser and navigate to:
```
https://localhost:5001/swagger
```
The Swagger UI lists all available endpoints and lets you test them interactively.

---

## API Reference

### `POST /api/auth/token`
Obtain a JWT.

**Request body:**
```json
{
  "clientId": "dev-client",
  "clientSecret": "dev-secret-replace-via-env-in-production"
}
```

**Response `200 OK`:**
```json
{
  "accessToken": "<jwt>",
  "tokenType": "Bearer",
  "expiresIn": 3600
}
```

---

### `POST /api/payments`
Initiate a payment. **Requires `Authorization: Bearer <token>` header.**

**Request body:**
```json
{
  "amount": 250.00,
  "currency": "USD",
  "referenceId": "order-abc-123"
}
```

**Response `201 Created`** (new payment):
```json
{
  "id": "3fa85f64-...",
  "referenceId": "order-abc-123",
  "amount": 250.00,
  "currency": "USD",
  "status": "Completed",
  "failureReason": null,
  "createdAt": "2026-02-26T00:00:00Z",
  "updatedAt": null
}
```

**Response `200 OK`** (idempotency replay — same `referenceId`):
Returns the stored payment result unchanged.

| Status value | Meaning |
|---|---|
| `Completed` | Payment succeeded |
| `Processing` | Pending async gateway response (amount > 50,000) |
| `Rejected` | Payment declined (e.g., unsupported currency) |

**Validation errors `400 Bad Request`:**
```json
{
  "code": "VALIDATION_ERROR",
  "message": "One or more validation errors occurred.",
  "details": ["Amount must be greater than zero."]
}
```

---

### `GET /api/payments/{referenceId}`
Retrieve a payment by its idempotency key. **Requires JWT.**

**Response `200 OK`:** (see above)  
**Response `404 Not Found`:**
```json
{
  "code": "PAYMENT_NOT_FOUND",
  "message": "No payment found with ReferenceId 'order-abc-123'."
}
```

---

## Security Notes

- **No secrets in source code.** `appsettings.json` has empty strings for all secrets; values must be injected via environment variables in production.
- **Expired tokens** return `401` with `X-Token-Expired: true` header.
- **Invalid credentials** return `401` — no detail leakage.
- **Replay / duplicate requests** handled by idempotency: replaying a request is safe and returns the original result.
- **Unique DB constraint** on `ReferenceId` prevents concurrent duplicate insertions.

---

## Running Tests

```bash
dotnet test
```

Tests cover:
- Successful payment creation
- Large-amount → `Processing` status
- Rejected currency (`XTS`)
- Idempotency: duplicate `ReferenceId` returns stored result without re-inserting
- GET by reference ID (found / not found)

---

## Developer

**Jenny Rose Singueo**
+63956-1509151
jennyrosesingueo@gmail.com
