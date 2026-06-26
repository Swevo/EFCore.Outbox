using Microsoft.EntityFrameworkCore;

namespace EFCore.Outbox;

/// <summary>Extension methods for configuring the outbox schema in EF Core.</summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="OutboxMessage"/> entity and configures its table schema.
    /// Call this inside <c>OnModelCreating</c>.
    /// </summary>
    public static ModelBuilder AddOutboxMessages(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.MessageType).IsRequired().HasMaxLength(512);
            entity.Property(m => m.Payload).IsRequired();
            entity.Property(m => m.CreatedAt).IsRequired();
            entity.HasIndex(m => m.ProcessedAt);
        });

        return modelBuilder;
    }
}
