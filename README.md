# Swevo.EFCore.Outbox

[![NuGet](https://img.shields.io/nuget/v/Swevo.EFCore.Outbox
[![NuGet Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.Outbox.svg)](https://www.nuget.org/packages/Swevo.EFCore.Outbox).svg)](https://www.nuget.org/packages/Swevo.EFCore.Outbox/)
[![Build](https://github.com/Swevo/Swevo.EFCore.Outbox/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/Swevo.EFCore.Outbox/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Transactional outbox pattern for EF Core + [AutoBus](https://github.com/Swevo/AutoBus). Enqueue domain events inside your existing `SaveChanges` transaction and publish them reliably via a background processor — zero message loss even if the bus is temporarily unavailable.

> **v2.0.0 breaking change:** MassTransit's `IPublishEndpoint` has been replaced with [Swevo.AutoBus](https://github.com/Swevo/AutoBus)'s `IMessageBus` — a free, MIT-licensed alternative now that MassTransit v9 is commercial-only. See [Migrating from MassTransit](#migrating-from-1x-masstransit) below.

---

## How It Works

```
┌─────────────────────────────────────────────────┐
│  Your service                                   │
│                                                 │
│  1. outbox.Add(new OrderPlaced(...))            │
│  2. dbContext.SaveChangesAsync()                │
│                                                 │
│  ┌──────────────────┐  atomic  ┌────────────┐  │
│  │  domain changes  │──────────│  OutboxMsg │  │
│  └──────────────────┘          └────────────┘  │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│  OutboxProcessor (BackgroundService)            │
│                                                 │
│  3. SELECT * FROM OutboxMessages WHERE          │
│        ProcessedAt IS NULL ORDER BY CreatedAt   │
│  4. bus.PublishAsync(message)                   │
│  5. UPDATE ProcessedAt = NOW()                  │
└─────────────────────────────────────────────────┘
```

---

## Installation

```bash
dotnet add package Swevo.EFCore.Outbox
```

Requires EF Core 8+ and [Swevo.AutoBus](https://github.com/Swevo/AutoBus) 1.x. AutoBus is added automatically as a transitive dependency, but you must still call `services.AddAutoBus(...)` (with or without a transport, e.g. `AddAutoBusRabbitMq`) so `IMessageBus` is available in DI — see [AutoBus's README](https://github.com/Swevo/AutoBus) for configuration.

---

## Quick Start

### 1. Configure your DbContext

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddOutboxMessages(); // registers the OutboxMessage entity
    }
}
```

### 2. Register services

```csharp
// Program.cs
builder.Services.AddOutbox<AppDbContext>(options =>
{
    options.PollingInterval = TimeSpan.FromSeconds(5); // default: 10s
    options.BatchSize = 50;                            // default: 100
});

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString);
    options.AddOutboxInterceptor(sp); // wires the scoped interceptor
});
```

### 3. Use in your services

```csharp
public class OrderService(IOutbox outbox, AppDbContext db)
{
    public async Task PlaceOrder(PlaceOrderCommand cmd)
    {
        db.Orders.Add(new Order { Id = cmd.OrderId, Total = cmd.Total });

        // Enqueued atomically — written in the same SaveChanges transaction
        outbox.Add(new OrderPlaced(cmd.OrderId, cmd.Total));

        await db.SaveChangesAsync();
        // ✓ Order row saved
        // ✓ OutboxMessage row saved  } in one DB transaction
        // ✗ Bus not involved yet
    }
}
```

### 4. Add EF migration

```bash
dotnet ef migrations add AddOutboxMessages
dotnet ef database update
```

---

## Architecture

### `IOutbox` (scoped)

Collects messages before `SaveChanges`. Injected into your service classes.

```csharp
outbox.Add(new OrderPlaced(orderId, total));     // enqueue
outbox.Add(new PaymentCharged(paymentId, total)); // multiple per save cycle
```

### `OutboxInterceptor` (scoped SaveChangesInterceptor)

Automatically runs during `SaveChanges` — no extra code needed after registration. Writes all pending `IOutbox` messages to the `OutboxMessages` table within the same database transaction.

### `OutboxProcessor<TContext>` (BackgroundService)

Polls the `OutboxMessages` table, publishes via `IMessageBus`, and marks messages as processed. Handles errors per-message — a single failing publish doesn't block the rest of the batch.

### `OutboxOptions`

| Property | Default | Description |
|---|---|---|
| `PollingInterval` | 10 seconds | How often to check for pending messages |
| `BatchSize` | 100 | Max messages processed per poll cycle |

---

## Integration with AutoAudit

Use alongside [AutoAudit](https://github.com/Swevo/AutoAudit) to get both audit fields and reliable messaging:

```csharp
[Auditable]
public partial class Order { ... }

// In your service:
db.Orders.Add(order);
outbox.Add(new OrderPlaced(order.Id));
await db.SaveChangesAsync();
// CreatedAt/UpdatedAt set by AuditInterceptor
// OrderPlaced written to OutboxMessages by OutboxInterceptor
// Both in one transaction
```

---

## Compatibility

| Dependency | Version |
|---|---|
| EF Core | 8.0+ |
| Swevo.AutoBus | 1.x |
| .NET | net8.0+ |

---

## Migrating from 1.x (MassTransit)

`OutboxProcessor<TContext>` now resolves `AutoBus.IMessageBus` from DI instead of MassTransit's `IPublishEndpoint`. The call site is a drop-in replacement — both expose `Publish(object message, Type messageType, CancellationToken)` with the same semantics — so the migration is:

1. Remove your MassTransit bus registration (or keep it running side-by-side if you still need it for consumers elsewhere).
2. Add [Swevo.AutoBus](https://github.com/Swevo/AutoBus): `dotnet add package Swevo.AutoBus` (add `Swevo.AutoBus.RabbitMQ` too if you need a real transport instead of in-memory).
3. Register it: `builder.Services.AddAutoBus(cfg => { /* register any AutoBus consumers */ });`
4. Upgrade `Swevo.EFCore.Outbox` to `2.0.0`. No changes required to `IOutbox`, `OutboxInterceptor`, or your domain event types.

If you still need to publish onto an existing MassTransit-based system, keep MassTransit registered in your app and write a small `IConsumer<T>` in AutoBus that forwards to MassTransit's `IPublishEndpoint` — this keeps EFCore.Outbox's own dependency footprint free of MassTransit while still supporting a gradual migration.

---


## Also by the same author

> 🌐 Full suite overview: **[swevo.github.io](https://swevo.github.io/)**

| Package | Description |
|---|---|
| [**AutoLog.Generator**](https://github.com/Swevo/AutoLog.Generator) | Compile-time high-performance logging — `[Log(Level, Message)]` generates `LoggerMessage.Define`. AOT-safe. |
| [**AutoHttpClient.Generator**](https://github.com/Swevo/AutoHttpClient.Generator) | Compile-time typed HTTP client — `[HttpClient]` on an interface generates a strongly-typed client. AOT-safe Refit alternative. |
| [**AutoDispatch.Generator**](https://github.com/Swevo/AutoDispatch.Generator) | Compile-time CQRS dispatcher — `[Handler]` generates a strongly-typed `IDispatcher`. No MediatR, no reflection. |
| [**AutoWire**](https://github.com/Swevo/AutoWire) | Compile-time DI auto-registration — `[Scoped]`/`[Singleton]`/`[Transient]` generates `IServiceCollection` registration code. |
| [**AutoMap.Generator**](https://github.com/Swevo/AutoMap.Generator) | Compile-time object mapping with generated extension methods. AOT-safe AutoMapper alternative. |
## License

MIT © 2025 Justin Bannister
