# Swevo.EFCore.Outbox

[![NuGet](https://img.shields.io/nuget/v/Swevo.EFCore.Outbox
[![NuGet Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.Outbox.svg)](https://www.nuget.org/packages/Swevo.EFCore.Outbox).svg)](https://www.nuget.org/packages/Swevo.EFCore.Outbox/)
[![Build](https://github.com/Swevo/Swevo.EFCore.Outbox/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/Swevo.EFCore.Outbox/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Transactional outbox pattern for EF Core + MassTransit. Enqueue domain events inside your existing `SaveChanges` transaction and publish them reliably via a background processor — zero message loss even if the bus is temporarily unavailable.

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
│  4. publisher.Publish(message)                  │
│  5. UPDATE ProcessedAt = NOW()                  │
└─────────────────────────────────────────────────┘
```

---

## Installation

```bash
dotnet add package Swevo.EFCore.Outbox
```

Requires EF Core 8+ and MassTransit 9+.

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

Polls the `OutboxMessages` table, publishes via `IPublishEndpoint`, and marks messages as processed. Handles errors per-message — a single failing publish doesn't block the rest of the batch.

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
| MassTransit | 9.x |
| .NET | net8.0+ |

---

## License

MIT © 2025 Justin Bannister
