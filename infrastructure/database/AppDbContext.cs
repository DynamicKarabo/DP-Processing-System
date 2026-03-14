using DP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DP.Infrastructure.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<IdempotencyKey> IdempotencyKeys { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.Property(e => e.Status).HasConversion<string>().IsRequired();
        });

        modelBuilder.Entity<IdempotencyKey>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.RequestHash).IsRequired();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Payload).HasColumnType("jsonb");
            entity.HasIndex(e => e.PaymentId);
        });
    }
}
