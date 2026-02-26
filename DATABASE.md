# Database

## Engine

SQLite, accessed via **Entity Framework Core** (`Microsoft.EntityFrameworkCore.Sqlite`).

SQLite was chosen for simplicity — no server process, no installation required, and the single-file
database is easy to version, reset, or swap out for a server-based engine (SQL Server, PostgreSQL) by
changing one line in `Program.cs` and the connection string.

---

## Connection Strings

| Environment | Setting | Default value |
|---|---|---|
| Development | `ConnectionStrings:DefaultConnection` | `Data Source=payments-dev.db` |
| Production | `ConnectionStrings:DefaultConnection` | `Data Source=payments.db` |

The connection string is read from `appsettings.json` (or its environment override) at startup:

```csharp
builder.Services.AddDbContext<PaymentsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
```

In production, override via environment variable:

```
ConnectionStrings__DefaultConnection=Data Source=/var/data/payments.db
```

---

## Schema Initialization

The schema is created automatically on startup using `EnsureCreated()`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    db.Database.EnsureCreated();
}
```

`EnsureCreated()` creates all tables and indexes if the database file does not exist. It does **not**
run migrations — if the schema changes, delete the `.db` file and restart, or switch to
`Database.Migrate()` with EF Core migrations for production use.

---

## Tables

### `Payments`

Stores every payment record. One row per unique `ReferenceId`.

| Column | SQLite type | Nullable | Constraints | Notes |
|---|---|---|---|---|
| `Id` | TEXT (GUID) | NOT NULL | PRIMARY KEY | Generated as `Guid.NewGuid()` in C# |
| `ReferenceId` | TEXT | NOT NULL | UNIQUE, max 128 chars | Client-supplied idempotency key |
| `Amount` | NUMERIC | NOT NULL | precision 18, scale 4 | Stored as `decimal(18,4)` |
| `Currency` | TEXT | NOT NULL | max 3 chars | ISO 4217 code, e.g. `USD` |
| `Status` | TEXT | NOT NULL | — | Stored as string (see below) |
| `FailureReason` | TEXT | NULL | — | Populated only when `Status = Rejected` or `Failed` |
| `CreatedAt` | TEXT | NOT NULL | — | UTC timestamp, set on insert |
| `UpdatedAt` | TEXT | NULL | — | UTC timestamp, set on every update |

#### `Status` values

`Status` is stored as a string (not an integer) for readability when inspecting the database directly.

| Value | Meaning |
|---|---|
| `Pending` | Payment received, not yet evaluated |
| `Processing` | Payment is being processed downstream |
| `Completed` | Payment was accepted and completed |
| `Rejected` | Payment was rejected by business rules (e.g. unsupported currency) |
| `Failed` | Payment failed due to a system or downstream error |

---

## Indexes

| Name | Table | Columns | Unique | Purpose |
|---|---|---|---|---|
| `PK_Payments` | `Payments` | `Id` | Yes | Primary key lookup |
| `IX_Payments_ReferenceId` | `Payments` | `ReferenceId` | Yes | Idempotency enforcement; fast lookup by client key |

The unique index on `ReferenceId` is the last line of defence for idempotency. Even if two concurrent
requests both pass the application-level duplicate check, the database rejects the second `INSERT`,
preventing phantom duplicates.

---

## EF Core Configuration

Defined in `PaymentsDbContext.OnModelCreating`:

```csharp
modelBuilder.Entity<Payment>(entity =>
{
    entity.HasKey(p => p.Id);

    entity.HasIndex(p => p.ReferenceId)
          .IsUnique()
          .HasDatabaseName("IX_Payments_ReferenceId");

    entity.Property(p => p.ReferenceId)
          .IsRequired()
          .HasMaxLength(128);

    entity.Property(p => p.Currency)
          .IsRequired()
          .HasMaxLength(3);

    entity.Property(p => p.Amount)
          .HasPrecision(18, 4);

    entity.Property(p => p.Status)
          .HasConversion<string>();
});
```

---

## Query Patterns

| Operation | Method | Tracking |
|---|---|---|
| Lookup by `ReferenceId` | `FirstOrDefaultAsync` | `AsNoTracking()` — read-only, no change tracking overhead |
| Insert new payment | `Add` + `SaveChangesAsync` | Tracked — EF generates the `INSERT` |
| Update payment status | `Update` + `SaveChangesAsync` | Tracked — EF generates the `UPDATE` |

---

## Resetting the Database

```powershell
# Development
Remove-Item payments-dev.db -ErrorAction SilentlyContinue
dotnet run --project src/PaymentsService.API   # EnsureCreated() recreates it on next startup
```

---

## Production Considerations

| Concern | Recommendation |
|---|---|
| Schema migrations | Switch from `EnsureCreated()` to `Database.Migrate()` with versioned EF Core migrations |
| Engine | Replace SQLite with SQL Server or PostgreSQL for concurrent write workloads |
| Connection string secret | Supply via environment variable or a secrets manager; never commit credentials |
| Backups | SQLite is a single file — back it up with any file-copy strategy; a WAL-mode database can be copied live |
| Connection pooling | SQLite does not benefit from a large pool; keep the default EF Core pool size |
