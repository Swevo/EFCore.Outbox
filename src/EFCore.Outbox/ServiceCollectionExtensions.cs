using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EFCore.Outbox;

/// <summary>Extension methods for registering outbox services in the DI container.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IOutbox"/>, <see cref="OutboxInterceptor"/>, and
    /// <see cref="OutboxProcessor{TContext}"/> into the service collection.
    /// </summary>
    /// <remarks>
    /// <para>After calling this, wire the interceptor into your <c>DbContext</c>:</para>
    /// <code>
    /// services.AddDbContext&lt;AppDbContext&gt;((sp, options) =>
    /// {
    ///     options.UseSqlServer(connectionString);
    ///     options.AddOutboxInterceptor(sp);
    /// });
    /// </code>
    /// <para>Also call <c>modelBuilder.AddOutboxMessages()</c> in <c>OnModelCreating</c>.</para>
    /// </remarks>
    public static IServiceCollection AddOutbox<TContext>(
        this IServiceCollection services,
        Action<OutboxOptions>? configureOptions = null)
        where TContext : DbContext
    {
        if (configureOptions is not null)
            services.Configure(configureOptions);
        else
            services.AddOptions<OutboxOptions>();

        services.AddScoped<IOutbox, Outbox>();
        services.AddScoped<OutboxInterceptor>();
        services.AddHostedService<OutboxProcessor<TContext>>();

        return services;
    }
}
