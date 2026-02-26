using Microsoft.EntityFrameworkCore;
using PaymentsService.Core.Entities;
namespace PaymentsService.Infrastructure.Data;

/*
 * EF Core database context for payments, defining entity mappings and
 * persistence configuration for the infrastructure layer.
 */
public class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
    }
}
