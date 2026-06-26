namespace EFCore.Outbox;

/// <summary>Configuration for the outbox background processor.</summary>
public sealed class OutboxOptions
{
    /// <summary>How often the processor polls for unprocessed messages. Default: 10 seconds.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Maximum messages processed per poll cycle. Default: 100.</summary>
    public int BatchSize { get; set; } = 100;
}
