using System.Text.Json;
using EFCore.Outbox;

namespace EFCore.Outbox.Tests;

public class OutboxTests
{
    private readonly IOutbox _outbox = new Outbox();

    // ── Add serializes message ─────────────────────────────────────────────

    [Fact]
    public void Add_SerializesMessageType()
    {
        _outbox.Add(new OrderPlaced(Guid.NewGuid(), 99.99m));

        _outbox.PendingMessages.Should().ContainSingle();
        _outbox.PendingMessages[0].MessageType
            .Should().Contain(nameof(OrderPlaced));
    }

    [Fact]
    public void Add_SerializesPayload()
    {
        var orderId = Guid.NewGuid();
        _outbox.Add(new OrderPlaced(orderId, 42.00m));

        var json = _outbox.PendingMessages[0].Payload;
        json.Should().Contain(orderId.ToString());
    }

    [Fact]
    public void Add_SetsCreatedAt()
    {
        var before = DateTimeOffset.UtcNow;
        _outbox.Add(new OrderPlaced(Guid.NewGuid(), 10m));

        _outbox.PendingMessages[0].CreatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Add_ProcessedAtIsNull()
    {
        _outbox.Add(new OrderPlaced(Guid.NewGuid(), 10m));

        _outbox.PendingMessages[0].ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void Add_MultipleMessages_AllEnqueued()
    {
        _outbox.Add(new OrderPlaced(Guid.NewGuid(), 10m));
        _outbox.Add(new OrderCancelled(Guid.NewGuid(), "OOS"));

        _outbox.PendingMessages.Should().HaveCount(2);
    }

    [Fact]
    public void Add_UsesActualRuntimeType()
    {
        object msg = new OrderPlaced(Guid.NewGuid(), 10m);
        _outbox.Add(msg);

        _outbox.PendingMessages[0].MessageType.Should().Contain(nameof(OrderPlaced));
    }

    // ── Clear ──────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_RemovesAllPendingMessages()
    {
        _outbox.Add(new OrderPlaced(Guid.NewGuid(), 10m));
        _outbox.Clear();

        _outbox.PendingMessages.Should().BeEmpty();
    }

    [Fact]
    public void Clear_OnEmptyOutbox_DoesNotThrow()
    {
        var act = () => _outbox.Clear();
        act.Should().NotThrow();
    }

    // ── Payload is valid JSON ──────────────────────────────────────────────

    [Fact]
    public void Add_PayloadIsValidJson()
    {
        _outbox.Add(new OrderPlaced(Guid.NewGuid(), 99m));

        var act = () => JsonDocument.Parse(_outbox.PendingMessages[0].Payload);
        act.Should().NotThrow();
    }

    [Fact]
    public void Add_PayloadRoundtrips()
    {
        var original = new OrderPlaced(Guid.NewGuid(), 55.50m);
        _outbox.Add(original);

        var msg = _outbox.PendingMessages[0];
        var type = Type.GetType(msg.MessageType)!;
        var deserialized = (OrderPlaced)JsonSerializer.Deserialize(msg.Payload, type)!;

        deserialized.OrderId.Should().Be(original.OrderId);
        deserialized.Amount.Should().Be(original.Amount);
    }
}
