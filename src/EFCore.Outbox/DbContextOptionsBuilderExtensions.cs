using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EFCore.Outbox;

/// <summary>Extension methods for <see cref="DbContextOptionsBuilder"/>.</summary>
public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Wires the scoped <see cref="OutboxInterceptor"/> (resolved from <paramref name="serviceProvider"/>)
    /// into this <see cref="DbContextOptionsBuilder"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddDbContext&lt;AppDbContext&gt;((sp, options) =>
    /// {
    ///     options.UseSqlServer(connectionString);
    ///     options.AddOutboxInterceptor(sp);
    /// });
    /// </code>
    /// </example>
    public static DbContextOptionsBuilder AddOutboxInterceptor(
        this DbContextOptionsBuilder builder,
        IServiceProvider serviceProvider)
        => builder.AddInterceptors(serviceProvider.GetRequiredService<OutboxInterceptor>());
}
