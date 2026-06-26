namespace EFCore.Outbox;

/// <summary>Represents a pending or processed outbox message stored alongside domain changes.</summary>
public sealed class OutboxMessage
{
    /// <summary>Unique identifier for this outbox entry.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Assembly-qualified name of the message type used for deserialization.</summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>JSON-serialized message payload.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the message was enqueued.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp when the message was successfully published. <c>null</c> if pending.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }
}
