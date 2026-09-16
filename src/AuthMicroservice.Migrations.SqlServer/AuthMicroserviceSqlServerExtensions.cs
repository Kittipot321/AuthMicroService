using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Data;
using AuthMicroservice.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Migrations.SqlServer;

public static class AuthMicroserviceSqlServerExtensions
{
    /// <summary>
    /// Registers <see cref="AuthDbContext"/> with the SQL Server provider and points migrations at this assembly.
    /// Uses the connection string from <c>AuthMicroservice:Database:ConnectionString</c> unless overridden.
    /// </summary>
    public static IAuthMicroserviceBuilder UseSqlServer(
        this IAuthMicroserviceBuilder builder,
        string? connectionString = null,
        Action<Microsoft.EntityFrameworkCore.Infrastructure.SqlServerDbContextOptionsBuilder>? sqlOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var migrationsAssembly = typeof(SqlServerMigrationsMarker).Assembly.GetName().Name!;

        builder.Services.AddDbContext<AuthDbContext>((sp, opts) =>
        {
            var authOpts = sp.GetRequiredService<IOptions<AuthMicroserviceOptions>>().Value;
            var cs = connectionString ?? authOpts.Database.ConnectionString
                ?? throw new InvalidOperationException(
                    "SQL Server connection string is required. Set AuthMicroservice:Database:ConnectionString or pass it to UseSqlServer(...).");

            opts.UseSqlServer(cs, sql =>
            {
                sql.MigrationsAssembly(migrationsAssembly);
                sqlOptions?.Invoke(sql);
            });
        });

        return builder;
    }
}
