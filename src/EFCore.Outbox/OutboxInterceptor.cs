using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EFCore.Outbox;

/// <summary>
/// EF Core interceptor that persists pending <see cref="IOutbox"/> messages to the outbox table
/// atomically within the same <c>SaveChanges</c> transaction.
/// Register as scoped and inject via <see cref="DbContextOptionsBuilderExtensions.AddOutboxInterceptor"/>.
/// </summary>
public sealed class OutboxInterceptor(IOutbox outbox) : SaveChangesInterceptor
{
    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        WritePendingMessages(eventData.Context!);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        WritePendingMessages(eventData.Context!);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void WritePendingMessages(DbContext context)
    {
        foreach (var message in outbox.PendingMessages)
            context.Set<OutboxMessage>().Add(message);

        outbox.Clear();
    }
}
