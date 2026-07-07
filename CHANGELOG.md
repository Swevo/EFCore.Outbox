# Changelog

## [2.0.0] - 2026-07-07

### Changed
- **BREAKING**: `OutboxProcessor<TContext>` now resolves and publishes via `AutoBus.IMessageBus` (from the free, MIT-licensed [Swevo.AutoBus](https://github.com/Swevo/AutoBus)) instead of MassTransit's `IPublishEndpoint`. The `MassTransit.Abstractions` package reference has been removed entirely.
- Call-site semantics are unchanged (`Publish(object message, Type messageType, CancellationToken)`), so the migration for existing consumers is registration-only — see README's "Migrating from 1.x (MassTransit)" section.

## [1.0.0] - 2025-01-01

### Added
- `OutboxMessage` entity — `Id`, `MessageType` (assembly-qualified), `Payload` (JSON), `CreatedAt`, `ProcessedAt`
- `IOutbox` — scoped service for enqueuing domain events before `SaveChanges`
- `OutboxInterceptor` — scoped `SaveChangesInterceptor` that writes pending outbox messages atomically within the same transaction
- `OutboxProcessor<TContext>` — `BackgroundService` that polls for pending messages, publishes via `IPublishEndpoint`, and marks them processed
- `OutboxOptions` — `PollingInterval` (default 10s) and `BatchSize` (default 100)
- `ModelBuilderExtensions.AddOutboxMessages()` — configures the `OutboxMessage` entity in `OnModelCreating`
- `ServiceCollectionExtensions.AddOutbox<TContext>()` — registers all services in one call
- `DbContextOptionsBuilderExtensions.AddOutboxInterceptor(sp)` — wires the scoped interceptor
