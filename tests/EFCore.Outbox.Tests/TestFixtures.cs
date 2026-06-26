using EFCore.Outbox;
using Microsoft.EntityFrameworkCore;

namespace EFCore.Outbox.Tests;

// ── Test domain messages ───────────────────────────────────────────────────

public record OrderPlaced(Guid OrderId, decimal Amount);
public record OrderCancelled(Guid OrderId, string Reason);

// ── Test entity ────────────────────────────────────────────────────────────

public class Order
{
    public int Id { get; set; }
    public string? Description { get; set; }
}

// ── Test DbContext ─────────────────────────────────────────────────────────

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddOutboxMessages();
    }
}
