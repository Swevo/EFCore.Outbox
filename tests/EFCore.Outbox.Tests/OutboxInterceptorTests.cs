using EFCore.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EFCore.Outbox.Tests;

public class OutboxInterceptorTests
{
    private static TestDbContext BuildContext(string dbName, IOutbox outbox)
    {
        var interceptor = new OutboxInterceptor(outbox);
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptor)
            .Options;
        return new TestDbContext(options);
    }

    // ── Interceptor writes messages atomically ────────────────────────────

    [Fact]
    public async Task SavingChanges_WritesPendingMessagesToDb()
    {
        var outbox = new Outbox();
        await using var ctx = BuildContext(nameof(SavingChanges_WritesPendingMessagesToDb), outbox);

        outbox.Add(new OrderPlaced(Guid.NewGuid(), 99m));
        ctx.Orders.Add(new Order { Id = 1 });
        await ctx.SaveChangesAsync();

        var stored = await ctx.OutboxMessages.ToListAsync();
        stored.Should().ContainSingle();
    }

    [Fact]
    public async Task SavingChanges_StoresCorrectMessageType()
    {
        var outbox = new Outbox();
        await using var ctx = BuildContext(nameof(SavingChanges_StoresCorrectMessageType), outbox);

        outbox.Add(new OrderPlaced(Guid.NewGuid(), 10m));
        await ctx.SaveChangesAsync();

        var msg = await ctx.OutboxMessages.SingleAsync();
        msg.MessageType.Should().Contain(nameof(OrderPlaced));
    }

    [Fact]
    public async Task SavingChanges_StoresPayload()
    {
        var outbox = new Outbox();
        var orderId = Guid.NewGuid();
        await using var ctx = BuildContext(nameof(SavingChanges_StoresPayload), outbox);

        outbox.Add(new OrderPlaced(orderId, 10m));
        await ctx.SaveChangesAsync();

        var msg = await ctx.OutboxMessages.SingleAsync();
        msg.Payload.Should().Contain(orderId.ToString());
    }

    [Fact]
    public async Task SavingChanges_ProcessedAtIsNull()
    {
        var outbox = new Outbox();
        await using var ctx = BuildContext(nameof(SavingChanges_ProcessedAtIsNull), outbox);

        outbox.Add(new OrderPlaced(Guid.NewGuid(), 10m));
        await ctx.SaveChangesAsync();

        var msg = await ctx.OutboxMessages.SingleAsync();
        msg.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task SavingChanges_MultipleMessages_AllWritten()
    {
        var outbox = new Outbox();
        await using var ctx = BuildContext(nameof(SavingChanges_MultipleMessages_AllWritten), outbox);

        outbox.Add(new OrderPlaced(Guid.NewGuid(), 10m));
        outbox.Add(new OrderCancelled(Guid.NewGuid(), "OOS"));
        await ctx.SaveChangesAsync();

        var stored = await ctx.OutboxMessages.ToListAsync();
        stored.Should().HaveCount(2);
    }

    [Fact]
    public async Task SavingChanges_ClearsPendingMessagesAfterWrite()
    {
        var outbox = new Outbox();
        await using var ctx = BuildContext(nameof(SavingChanges_ClearsPendingMessagesAfterWrite), outbox);

        outbox.Add(new OrderPlaced(Guid.NewGuid(), 10m));
        await ctx.SaveChangesAsync();

        outbox.PendingMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task SavingChanges_NoPendingMessages_NoOutboxRowsWritten()
    {
        var outbox = new Outbox();
        await using var ctx = BuildContext(nameof(SavingChanges_NoPendingMessages_NoOutboxRowsWritten), outbox);

        ctx.Orders.Add(new Order { Id = 1 });
        await ctx.SaveChangesAsync();

        (await ctx.OutboxMessages.CountAsync()).Should().Be(0);
    }
}
