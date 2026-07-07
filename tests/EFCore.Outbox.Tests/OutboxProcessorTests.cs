using EFCore.Outbox;
using AutoBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EFCore.Outbox.Tests;

public class OutboxProcessorTests
{
    private static (OutboxProcessor<TestDbContext> processor, IMessageBus publisher, IServiceProvider sp)
        BuildProcessor(string dbName, Action<OutboxOptions>? configureOptions = null)
    {
        var publisher = Substitute.For<IMessageBus>();
        var options = new OutboxOptions();
        configureOptions?.Invoke(options);

        var services = new ServiceCollection();
        services.AddSingleton<IMessageBus>(publisher);
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();

        var processor = new OutboxProcessor<TestDbContext>(
            sp,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<OutboxProcessor<TestDbContext>>.Instance);

        return (processor, publisher, sp);
    }

    private static async Task<TestDbContext> SeedOutboxMessage(
        IServiceProvider sp, object message, bool alreadyProcessed = false)
    {
        var ctx = sp.GetRequiredService<TestDbContext>();
        var outbox = new Outbox();
        outbox.Add(message);
        var msg = outbox.PendingMessages[0];
        if (alreadyProcessed) msg.ProcessedAt = DateTimeOffset.UtcNow;
        ctx.OutboxMessages.Add(msg);
        await ctx.SaveChangesAsync();
        return ctx;
    }

    // ── Processor publishes pending messages ──────────────────────────────

    [Fact]
    public async Task ProcessBatch_PublishesPendingMessage()
    {
        var (processor, publisher, sp) = BuildProcessor(nameof(ProcessBatch_PublishesPendingMessage));
        await SeedOutboxMessage(sp, new OrderPlaced(Guid.NewGuid(), 10m));

        await processor.ProcessBatchAsync();

        await publisher.Received(1).PublishAsync(
            Arg.Any<object>(), Arg.Is<Type>(t => t == typeof(OrderPlaced)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBatch_MarksMessageAsProcessed()
    {
        var (processor, _, sp) = BuildProcessor(nameof(ProcessBatch_MarksMessageAsProcessed));
        await SeedOutboxMessage(sp, new OrderPlaced(Guid.NewGuid(), 10m));

        await processor.ProcessBatchAsync();

        // Use a fresh context to avoid the identity-map cache
        using var scope = sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var msg = await ctx.OutboxMessages.SingleAsync();
        msg.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessBatch_ProcessedAtSetToApproximatelyNow()
    {
        var (processor, _, sp) = BuildProcessor(nameof(ProcessBatch_ProcessedAtSetToApproximatelyNow));
        var before = DateTimeOffset.UtcNow;
        await SeedOutboxMessage(sp, new OrderPlaced(Guid.NewGuid(), 10m));

        await processor.ProcessBatchAsync();

        using var scope = sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var msg = await ctx.OutboxMessages.SingleAsync();
        msg.ProcessedAt.Should().NotBeNull();
        msg.ProcessedAt!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task ProcessBatch_AlreadyProcessedMessage_NotPublishedAgain()
    {
        var (processor, publisher, sp) = BuildProcessor(nameof(ProcessBatch_AlreadyProcessedMessage_NotPublishedAgain));
        await SeedOutboxMessage(sp, new OrderPlaced(Guid.NewGuid(), 10m), alreadyProcessed: true);

        await processor.ProcessBatchAsync();

        await publisher.DidNotReceive().PublishAsync(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBatch_MultipleMessages_AllPublished()
    {
        var (processor, publisher, sp) = BuildProcessor(nameof(ProcessBatch_MultipleMessages_AllPublished));
        await SeedOutboxMessage(sp, new OrderPlaced(Guid.NewGuid(), 10m));
        await SeedOutboxMessage(sp, new OrderCancelled(Guid.NewGuid(), "OOS"));

        await processor.ProcessBatchAsync();

        await publisher.Received(2).PublishAsync(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBatch_OrdersMessagesByCreatedAt()
    {
        var (processor, publisher, sp) = BuildProcessor(nameof(ProcessBatch_OrdersMessagesByCreatedAt));
        var publishOrder = new List<object>();
        await publisher.PublishAsync(
            Arg.Do<object>(m => publishOrder.Add(m)),
            Arg.Any<Type>(),
            Arg.Any<CancellationToken>());

        var ctx = sp.GetRequiredService<TestDbContext>();
        var outbox = new Outbox();

        outbox.Add(new OrderPlaced(Guid.NewGuid(), 1m));
        var first = outbox.PendingMessages[0];
        outbox.Clear();

        await Task.Delay(2);

        outbox.Add(new OrderCancelled(Guid.NewGuid(), "late"));
        var second = outbox.PendingMessages[0];
        outbox.Clear();

        ctx.OutboxMessages.AddRange(first, second);
        await ctx.SaveChangesAsync();

        await processor.ProcessBatchAsync();

        publishOrder[0].Should().BeOfType<OrderPlaced>();
        publishOrder[1].Should().BeOfType<OrderCancelled>();
    }

    // ── Batch size ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessBatch_RespectsMaxBatchSize()
    {
        var (processor, publisher, sp) = BuildProcessor(
            nameof(ProcessBatch_RespectsMaxBatchSize),
            o => o.BatchSize = 2);

        for (var i = 0; i < 5; i++)
            await SeedOutboxMessage(sp, new OrderPlaced(Guid.NewGuid(), i));

        await processor.ProcessBatchAsync();

        await publisher.Received(2).PublishAsync(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>());
    }

    // ── Error handling ─────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessBatch_UnknownMessageType_SkipsWithoutThrowing()
    {
        var (processor, publisher, sp) = BuildProcessor(nameof(ProcessBatch_UnknownMessageType_SkipsWithoutThrowing));

        var ctx = sp.GetRequiredService<TestDbContext>();
        ctx.OutboxMessages.Add(new OutboxMessage
        {
            MessageType = "NonExistent.Type, NonExistentAssembly",
            Payload = "{}",
        });
        await ctx.SaveChangesAsync();

        var act = () => processor.ProcessBatchAsync();
        await act.Should().NotThrowAsync();

        await publisher.DidNotReceive().PublishAsync(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBatch_EmptyOutbox_DoesNotCallPublish()
    {
        var (processor, publisher, sp) = BuildProcessor(nameof(ProcessBatch_EmptyOutbox_DoesNotCallPublish));

        await processor.ProcessBatchAsync();

        await publisher.DidNotReceive().PublishAsync(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBatch_PublishThrows_OtherMessageStillProcessed()
    {
        var (processor, publisher, sp) = BuildProcessor(nameof(ProcessBatch_PublishThrows_OtherMessageStillProcessed));

        var callCount = 0;
        publisher.PublishAsync(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callCount++;
                if (callCount == 1) throw new InvalidOperationException("bus down");
                return Task.CompletedTask;
            });

        await SeedOutboxMessage(sp, new OrderPlaced(Guid.NewGuid(), 1m));
        await SeedOutboxMessage(sp, new OrderCancelled(Guid.NewGuid(), "x"));

        await processor.ProcessBatchAsync();

        // Second message should still have been attempted
        await publisher.Received(2).PublishAsync(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>());
    }
}
