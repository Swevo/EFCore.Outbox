using System.Text.Json;

namespace EFCore.Outbox;

internal sealed class Outbox : IOutbox
{
    private readonly List<OutboxMessage> _pending = [];

    /// <inheritdoc/>
    public void Add<T>(T message) where T : class
    {
        var type = message.GetType();
        _pending.Add(new OutboxMessage
        {
            MessageType = type.AssemblyQualifiedName ?? type.FullName ?? type.Name,
            Payload = JsonSerializer.Serialize(message, type),
        });
    }

    /// <inheritdoc/>
    public IReadOnlyList<OutboxMessage> PendingMessages => _pending;

    /// <inheritdoc/>
    public void Clear() => _pending.Clear();
}
