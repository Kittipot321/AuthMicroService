using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Data;
using AuthMicroservice.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Migrations.Sqlite;

public static class AuthMicroserviceSqliteExtensions
{
    /// <summary>
    /// Registers <see cref="AuthDbContext"/> with the SQLite provider and points migrations at this assembly.
    /// Uses the connection string from <c>AuthMicroservice:Database:ConnectionString</c> unless overridden.
    /// </summary>
    public static IAuthMicroserviceBuilder UseSqlite(
        this IAuthMicroserviceBuilder builder,
        string? connectionString = null,
        Action<Microsoft.EntityFrameworkCore.Infrastructure.SqliteDbContextOptionsBuilder>? sqliteOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var migrationsAssembly = typeof(SqliteMigrationsMarker).Assembly.GetName().Name!;

        builder.Services.AddDbContext<AuthDbContext>((sp, opts) =>
        {
            var authOpts = sp.GetRequiredService<IOptions<AuthMicroserviceOptions>>().Value;
            var cs = connectionString ?? authOpts.Database.ConnectionString
                ?? throw new InvalidOperationException(
                    "SQLite connection string is required. Set AuthMicroservice:Database:ConnectionString or pass it to UseSqlite(...).");

            opts.UseSqlite(cs, sqlite =>
            {
                sqlite.MigrationsAssembly(migrationsAssembly);
                sqliteOptions?.Invoke(sqlite);
            });
        });

        return builder;
    }
}
