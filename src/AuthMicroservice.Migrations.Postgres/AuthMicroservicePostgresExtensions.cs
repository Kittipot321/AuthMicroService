using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Data;
using AuthMicroservice.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Migrations.Postgres;

public static class AuthMicroservicePostgresExtensions
{
    /// <summary>
    /// Registers <see cref="AuthDbContext"/> with the Npgsql (PostgreSQL) provider and points migrations at this assembly.
    /// Uses the connection string from <c>AuthMicroservice:Database:ConnectionString</c> unless overridden.
    /// </summary>
    public static IAuthMicroserviceBuilder UsePostgres(
        this IAuthMicroserviceBuilder builder,
        string? connectionString = null,
        Action<Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder>? npgsqlOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var migrationsAssembly = typeof(PostgresMigrationsMarker).Assembly.GetName().Name!;

        builder.Services.AddDbContext<AuthDbContext>((sp, opts) =>
        {
            var authOpts = sp.GetRequiredService<IOptions<AuthMicroserviceOptions>>().Value;
            var cs = connectionString ?? authOpts.Database.ConnectionString
                ?? throw new InvalidOperationException(
                    "PostgreSQL connection string is required. Set AuthMicroservice:Database:ConnectionString or pass it to UsePostgres(...).");

            opts.UseNpgsql(cs, npg =>
            {
                npg.MigrationsAssembly(migrationsAssembly);
                npgsqlOptions?.Invoke(npg);
            });
        });

        return builder;
    }
}
