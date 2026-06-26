namespace EFCore.Outbox;

/// <summary>
/// Collects domain messages to be written atomically to the outbox during <c>SaveChanges</c>.
/// Resolve this from DI (scoped) and call <see cref="Add{T}"/> before <c>SaveChangesAsync</c>.
/// </summary>
public interface IOutbox
{
    /// <summary>Enqueues <paramref name="message"/> for atomic persistence in the outbox.</summary>
    void Add<T>(T message) where T : class;

    /// <summary>All messages enqueued since the last <c>SaveChanges</c>.</summary>
    IReadOnlyList<OutboxMessage> PendingMessages { get; }

    /// <summary>Clears pending messages. Called automatically by the interceptor after writing.</summary>
    void Clear();
}
