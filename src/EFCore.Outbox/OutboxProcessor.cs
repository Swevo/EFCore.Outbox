using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EFCore.Outbox;

/// <summary>
/// Background service that polls the outbox table, publishes pending messages via
/// <see cref="IPublishEndpoint"/>, and marks them as processed.
/// </summary>
public sealed class OutboxProcessor<TContext>(
    IServiceProvider serviceProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox processor encountered an unexpected error.");
            }

            await Task.Delay(options.Value.PollingInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Processes one batch of pending outbox messages.
    /// Internal for testability — call directly in unit tests via <c>InternalsVisibleTo</c>.
    /// </summary>
    internal async Task ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var messages = await context.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return;

        foreach (var message in messages)
        {
            try
            {
                var type = Type.GetType(message.MessageType);
                if (type is null)
                {
                    logger.LogWarning(
                        "Outbox message {Id}: cannot resolve type '{MessageType}'. Skipping.",
                        message.Id, message.MessageType);
                    continue;
                }

                var payload = JsonSerializer.Deserialize(message.Payload, type);
                if (payload is null)
                {
                    logger.LogWarning(
                        "Outbox message {Id}: payload deserialized to null. Skipping.", message.Id);
                    continue;
                }

                await publisher.Publish(payload, type, cancellationToken);
                message.ProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox message {Id}. Will retry on next cycle.", message.Id);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
